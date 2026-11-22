#!/usr/bin/env python3
"""Embed the compact design into the self-contained Windows dashboard.

Safe to run again: changes are idempotent. This is a presentation-only patch;
existing button handlers, task APIs, update flow and extension stay untouched.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent.parent
html_file = ROOT / "ui" / "index.html"
css_file = ROOT / "ui" / "compact-desktop.css"
html = html_file.read_text(encoding="utf-8-sig")
css = css_file.read_text(encoding="utf-8").rstrip()
start = "/* COMPACT-DESKTOP-CSS-BEGIN */"
end = "/* COMPACT-DESKTOP-CSS-END */"
block = "\n" + start + "\n" + css + "\n" + end + "\n"
if start in html:
    html, replaced = re.subn(
        r"\n/\* COMPACT-DESKTOP-CSS-BEGIN \*/.*?/\* COMPACT-DESKTOP-CSS-END \*/\n",
        lambda _m: block,
        html,
        count=1,
        flags=re.S,
    )
    assert replaced == 1
else:
    assert html.count("</style>") == 1
    html = html.replace("</style>", block + "  </style>", 1)

brand = """      <div class="compact-brand" aria-label="FastDownloader Pro">
        <span class="compact-brand-mark" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3v12m-5-5 5 5 5-5M5 19h14"/></svg>
        </span>
        <span class="compact-brand-name">FastDownloader <small>PRO</small></span>
      </div>
"""
if 'class="compact-brand"' not in html:
    needle = '      <div class="header-left-zone">\n'
    assert html.count(needle) == 1
    html = html.replace(needle, needle + brand, 1)

context_bar = """      <div class="compact-contextbar" aria-label="Download list summary">
        <div class="compact-heading">
          <h1 class="compact-view-title" id="compactViewTitle">All downloads</h1>
          <span class="compact-view-count" id="compactViewCount">0 shown</span>
        </div>
        <span class="compact-shortcuts" id="compactShortcutHint">Search <kbd>Ctrl</kbd> + <kbd>F</kbd> · Select a task for actions</span>
      </div>
"""
if 'class="compact-contextbar"' not in html:
    needle = '    <main>\n'
    assert html.count(needle) == 1
    html = html.replace(needle, needle + context_bar, 1)

heading_fn = """    // The compact header reflects the real task collection and active filters.
    // This is read-only UI state; no extra task fetch or download side effects.
    function updateCompactHeading(visibleCount) {
      const title = document.getElementById('compactViewTitle');
      const count = document.getElementById('compactViewCount');
      const hint = document.getElementById('compactShortcutHint');
      if (!title || !count) return;
      const labels = currentLang === 'ar' ? {
        all:'كل التنزيلات', active:'قيد التنزيل', paused:'المتوقفة والمنتظرة',
        completed:'المكتملة', videos:'الفيديوهات والبث', archives:'الملفات المضغوطة',
        torrents:'التورنت'
      } : {
        all:'All downloads', active:'Downloading', paused:'Paused / Waiting',
        completed:'Completed', videos:'Videos & Streams', archives:'Compressed & ISO',
        torrents:'Torrents'
      };
      title.textContent = currentQueueFilter
        ? (currentQueueFilter === 'NightQueue'
            ? (currentLang === 'ar' ? 'قائمة التنزيل الليلية' : 'Night Queue')
            : (currentLang === 'ar' ? 'قائمة التنزيل الرئيسية' : 'Main Queue'))
        : (labels[currentCategory] || labels.all);
      const total = Array.isArray(tasks) ? tasks.length : 0;
      count.textContent = currentLang === 'ar'
        ? visibleCount + ' ظاهر من ' + total
        : visibleCount + ' shown · ' + total + ' total';
      if (hint) {
        hint.style.display = currentLang === 'ar' ? 'none' : '';
      }
    }

"""
if 'function updateCompactHeading(' not in html:
    needle = '    function renderTable() {'
    assert html.count(needle) == 1
    html = html.replace(needle, heading_fn + needle, 1)

needle = '      const tableContainer = document.querySelector(\'.table-container\');\n      if (filtered.length === 0) {'
if 'updateCompactHeading(filtered.length);' not in html:
    assert html.count(needle) == 1
    html = html.replace(needle, "      const tableContainer = document.querySelector('.table-container');\n      updateCompactHeading(filtered.length);\n      if (filtered.length === 0) {", 1)

# Make the visible Ctrl+F search hint functional instead of advertising a dead shortcut.
if '/* COMPACT-SEARCH-SHORTCUT */' not in html:
    needle = "    // Keyboard Shortcuts\n    window.addEventListener('keydown', (e) => {\n"
    assert html.count(needle) == 1
    html = html.replace(needle, needle + """      /* COMPACT-SEARCH-SHORTCUT */
      if ((e.ctrlKey || e.metaKey) && (e.key === 'f' || e.key === 'F')) {
        const searchBox = document.getElementById('searchInput');
        if (searchBox && !document.querySelector('.modal-overlay.active')) {
          e.preventDefault();
          searchBox.focus();
          searchBox.select();
        }
      }
""", 1)

# New installations start in the approved light appearance; saved choices win.
html = html.replace(
    "let currentTheme = localStorage.getItem('fast_theme') || 'dark';",
    "let currentTheme = localStorage.getItem('fast_theme') || 'light';",
    1,
)
html_file.write_text(html, encoding="utf-8-sig")
print(f"Synced compact layout and CSS into {html_file}")
