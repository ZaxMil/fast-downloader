"use strict";
const {test} = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root,"ui","index.html"),"utf8");
const css = fs.readFileSync(path.join(root,"ui","compact-desktop.css"),"utf8").trim();

test("embedded compact stylesheet is in sync with the source CSS", () => {
  const match = html.match(/\/\* COMPACT-DESKTOP-CSS-BEGIN \*\/([\s\S]*?)\/\* COMPACT-DESKTOP-CSS-END \*\//);
  assert.ok(match, "CSS is embedded in the self-contained Windows UI");
  assert.equal(match[1].trim(), css);
});

test("approved compact structure is present and no legacy controls are removed", () => {
  for (const id of [
    "compactViewTitle","compactViewCount","compactShortcutHint",
    "appMenubar","actionToolbar","autoGrabBtn","speedSelect",
    "sidebarToggleBtn","searchInput","downloadsTable","tableBody",
    "selectAllCheckbox","bottomTotalSpeed","graphContainer","appTitlebar",
    "systemStatusPill","btnApplyAppUpdate"
  ]) {
    assert.ok(html.includes('id="'+id+'"'), "preserved or added "+id);
  }
  assert.match(html, /class="compact-brand"/);
  assert.match(html, /class="compact-contextbar"/);
  assert.match(html, /data-tb-item="add" onclick="showAddModal\(\)"/);
  assert.match(html, /data-tb-item="pause" onclick="pauseSelected\(\)"/);
  assert.match(html, /data-tb-item="resume" onclick="resumeSelected\(\)"/);
  assert.match(html, /id="speedSelect" onchange="changeSpeedLimit\(this.value\)"/);
  assert.match(html, /id="searchInput"[^>]*oninput="renderTable\(\)"/);
  assert.match(html, /updateCompactHeading\(filtered.length\)/);
});

test("the compact summary shows actual filtered task counts, including empty state", () => {
  const from = html.indexOf("    function updateCompactHeading(visibleCount) {");
  const to = html.indexOf("    function renderTable() {", from);
  assert.ok(from > 0 && to > from);
  const items = new Map();
  for (const id of ["compactViewTitle","compactViewCount","compactShortcutHint"]) {
    items.set(id, {textContent:"",style:{}});
  }
  const context = vm.createContext({
    document:{getElementById:id=>items.get(id)||null},
    currentLang:"en", currentCategory:"completed", currentQueueFilter:null,
    tasks:[{status:"complete"},{status:"active"},{status:"complete"}]
  });
  vm.runInContext(html.slice(from,to), context);
  context.updateCompactHeading(2);
  assert.equal(items.get("compactViewTitle").textContent,"Completed");
  assert.equal(items.get("compactViewCount").textContent,"2 shown · 3 total");
  context.currentLang = "ar";
  context.currentCategory = "all";
  context.updateCompactHeading(0);
  assert.equal(items.get("compactViewTitle").textContent,"كل التنزيلات");
  assert.equal(items.get("compactViewCount").textContent,"0 ظاهر من 3");
  assert.equal(items.get("compactShortcutHint").style.display,"none");
});

test("all inline JavaScript blocks parse, and existing theme preference is honored", () => {
  const blocks = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)];
  assert.ok(blocks.length >= 2, "initialization and app scripts exist");
  blocks.forEach(([_, code],i) => assert.doesNotThrow(()=>new vm.Script(code), "script "+i));
  assert.match(html,/localStorage.getItem\('fast_theme'\) \|\| 'light'/);
  assert.match(html,/function applyTheme\(theme\)/);
  assert.match(html,/localStorage.setItem\('fast_theme', theme\)/);
});
