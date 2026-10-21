// FastDownloader Pro - Floating Video Download Bar & In-Page Media Interceptor
(function () {
  'use strict';

  const FD_PORT = 6801;
  const processedElements = new WeakSet();
  const iconUrl = (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.getURL) 
    ? chrome.runtime.getURL('icons/icon32.png') 
    : '';

  // Sleek Bottom-Right Toast HUD
  function showToast(message, isError = false) {
    let toast = document.getElementById('fd-global-toast');
    if (!toast) {
      toast = document.createElement('div');
      toast.id = 'fd-global-toast';
      toast.className = 'fd-toast';
      document.body.appendChild(toast);
    }
    toast.innerHTML = `
      <img src="${iconUrl}" style="width:18px;height:18px;border-radius:4px;flex-shrink:0;">
      <span>${message}</span>
    `;
    toast.className = 'fd-toast' + (isError ? ' fd-toast-error' : '');
    toast.classList.add('fd-toast-show');
    clearTimeout(toast._timer);
    toast._timer = setTimeout(() => {
      toast.classList.remove('fd-toast-show');
    }, 3800);
  }

  // Detect Best Media / Stream URL (Sniffs HLS, DASH, or direct src)
  function detectBestMediaUrl(element) {
    if (!element) return window.location.href;

    // 1. Facebook & Instagram specific URL resolve
    if (window.location.hostname.includes('facebook.com') || window.location.hostname.includes('fb.watch')) {
      if (window.location.pathname.includes('/videos/') || window.location.pathname.includes('/reel/') || window.location.pathname.includes('/watch')) {
        return window.location.href;
      }
      const post = element.closest('[role="article"], [data-pagelet*="FeedUnit"], .x1y1aw1k');
      if (post) {
        const link = post.querySelector('a[href*="/videos/"], a[href*="/reel/"], a[href*="/watch/"]');
        if (link && link.href) return link.href;
      }
      return window.location.href;
    }

    if (window.location.hostname.includes('instagram.com')) {
      if (window.location.pathname.includes('/p/') || window.location.pathname.includes('/reel/')) {
        return window.location.href;
      }
      const post = element.closest('article, main');
      if (post) {
        const link = post.querySelector('a[href*="/p/"], a[href*="/reel/"]');
        if (link && link.href) return link.href;
      }
      return window.location.href;
    }

    let src = element.currentSrc || element.src;
    if (src && !src.startsWith('blob:') && !src.startsWith('data:')) {
      return src;
    }
    const sources = element.querySelectorAll('source');
    for (const s of sources) {
      if (s.src && !s.src.startsWith('blob:') && !s.src.startsWith('data:')) {
        return s.src;
      }
    }
    try {
      const entries = performance.getEntriesByType('resource');
      for (let i = entries.length - 1; i >= 0 && i >= entries.length - 35; i--) {
        const name = entries[i].name;
        if (/\.(m3u8|mpd)(\?.*)?$/i.test(name)) {
          return name;
        }
      }
    } catch (e) {}
    return window.location.href;
  }

  // Send Download Task to FastDownloader Desktop Backend (Via Extension Service Worker for Cookie Auth)
  async function sendToFastDownloader(url, title, type = 'video', quality = 'best', triggerPill = null) {
    if (triggerPill) {
      const textSpan = triggerPill.querySelector('.fd-pill-text');
      if (textSpan) {
        const origText = textSpan.textContent;
        textSpan.textContent = 'Connecting...';
        triggerPill.classList.add('fd-success');
        setTimeout(() => {
          textSpan.textContent = origText;
          triggerPill.classList.remove('fd-success');
        }, 3000);
      }
    }

    const payload = {
      type: 'FD_DOWNLOAD_REQUEST',
      url: url || window.location.href,
      title: title || document.title,
      mediaType: type,
      quality: quality,
      referer: window.location.href
    };

    // Try communicating via background worker first (with full cookie & session extraction)
    if (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.sendMessage) {
      try {
        const res = await new Promise((resolve) => {
          chrome.runtime.sendMessage(payload, (resp) => {
            if (chrome.runtime.lastError || !resp) {
              resolve(null);
            } else {
              resolve(resp);
            }
          });
        });

        if (res && res.ok) {
          const qualityName = quality === 'audio' ? 'MP3 Audio' : quality === '720p' ? '720p HD' : 'Best Quality (MP4)';
          showToast(`⚡ Sent to FastDownloader: <strong>${qualityName}</strong> (with Cookies & Auth)`);
          if (triggerPill) {
            const textSpan = triggerPill.querySelector('.fd-pill-text');
            if (textSpan) textSpan.textContent = '✓ Sent!';
          }
          return;
        }
      } catch (e) {}
    }

    // Direct fallback if extension channel was interrupted
    try {
      const response = await fetch(`http://127.0.0.1:${FD_PORT}/api/download`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          url: url || window.location.href,
          title: title || document.title,
          type: type,
          quality: quality,
          referer: window.location.href
        })
      });

      if (response.ok) {
        const qualityName = quality === 'audio' ? 'MP3 Audio' : quality === '720p' ? '720p HD' : 'Best Quality (MP4)';
        showToast(`⚡ Sent to FastDownloader: <strong>${qualityName}</strong>`);
        if (triggerPill) {
          const textSpan = triggerPill.querySelector('.fd-pill-text');
          if (textSpan) textSpan.textContent = '✓ Sent!';
        }
      } else {
        showToast('⚠️ FastDownloader is running, but failed to start task.', true);
      }
    } catch (err) {
      showToast('⚠️ FastDownloader app is not running. Launch FastDownloader.exe on port 6801.', true);
    }
  }

  // Handle Canvas Video Recording (canvas.captureStream)
  function recordCanvasStream(canvas, durationMs = 15000) {
    try {
      const stream = canvas.captureStream(30);
      const mediaRecorder = new MediaRecorder(stream, { mimeType: 'video/webm' });
      const chunks = [];

      mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) chunks.push(e.data);
      };

      mediaRecorder.onstop = () => {
        const blob = new Blob(chunks, { type: 'video/webm' });
        const blobUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = blobUrl;
        a.download = `canvas-capture-${Date.now()}.webm`;
        a.click();
        showToast('✅ Canvas recording saved successfully!');
      };

      mediaRecorder.start();
      showToast(`🔴 Recording Canvas (${durationMs / 1000}s)...`);
      setTimeout(() => {
        if (mediaRecorder.state === 'recording') {
          mediaRecorder.stop();
        }
      }, durationMs);
    } catch (e) {
      showToast('❌ Canvas recording not supported on this element.', true);
    }
  }

  // Inject Floating Button onto any Video or Media Player
  function injectDownloadButton(targetElement, isCanvas = false) {
    if (processedElements.has(targetElement)) return;
    processedElements.add(targetElement);

    const parent = targetElement.parentElement;
    if (!parent) return;

    // Ensure parent is positioning anchor
    const parentStyle = window.getComputedStyle(parent);
    if (parentStyle.position === 'static') {
      parent.style.position = 'relative';
    }

    // Hover helpers
    parent.addEventListener('mouseenter', () => parent.classList.add('fd-parent-hover'));
    parent.addEventListener('mouseleave', () => {
      parent.classList.remove('fd-parent-hover');
      container.classList.remove('fd-menu-open');
      menu.classList.remove('fd-show');
    });

    const container = document.createElement('div');
    container.className = 'fd-download-btn-container';

    // Main Pill Wrap
    const pill = document.createElement('div');
    pill.className = 'fd-download-pill';

    // Left Action Button
    const mainAction = document.createElement('button');
    mainAction.className = 'fd-pill-action';
    mainAction.title = isCanvas ? 'Record Canvas Animation' : 'Download with FastDownloader (Best Quality)';
    mainAction.innerHTML = `
      ${iconUrl ? `<img src="${iconUrl}" class="fd-pill-icon" alt="FD">` : `<span>⚡</span>`}
      <span class="fd-pill-text">${isCanvas ? 'Record' : 'Download'}</span>
    `;

    // Dropdown Arrow Button
    const arrowBtn = document.createElement('button');
    arrowBtn.className = 'fd-pill-arrow';
    arrowBtn.title = 'Select Quality / Formats';
    arrowBtn.innerHTML = `
      <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
        <polyline points="6 9 12 15 18 9"></polyline>
      </svg>
    `;

    pill.appendChild(mainAction);
    pill.appendChild(arrowBtn);

    // Dropdown Quality Menu
    const menu = document.createElement('div');
    menu.className = 'fd-download-menu';

    if (isCanvas) {
      menu.innerHTML = `
        <div class="fd-menu-header">
          <span>Record Canvas</span>
          <span class="fd-menu-status"><span class="fd-menu-status-dot"></span> WebGL</span>
        </div>
        <div class="fd-menu-item" data-action="rec-15">
          <div class="fd-menu-item-left">🔴 <span>Record 15s</span></div>
          <span class="fd-menu-badge fd-badge-best">WebM</span>
        </div>
        <div class="fd-menu-item" data-action="rec-30">
          <div class="fd-menu-item-left">🔴 <span>Record 30s</span></div>
          <span class="fd-menu-badge">WebM</span>
        </div>
        <div class="fd-menu-item" data-action="rec-60">
          <div class="fd-menu-item-left">🔴 <span>Record 60s</span></div>
          <span class="fd-menu-badge">WebM</span>
        </div>
      `;
    } else {
      menu.innerHTML = `
        <div class="fd-menu-header">
          <span>FastDownloader Pro</span>
          <span class="fd-menu-status"><span class="fd-menu-status-dot"></span> 16 Chunks</span>
        </div>
        <div class="fd-menu-item" data-quality="best">
          <div class="fd-menu-item-left">🎬 <span>Best Quality</span></div>
          <span class="fd-menu-badge fd-badge-best">4K/1080p MP4</span>
        </div>
        <div class="fd-menu-item" data-quality="720p">
          <div class="fd-menu-item-left">🎬 <span>Fast Standard</span></div>
          <span class="fd-menu-badge">720p MP4</span>
        </div>
        <div class="fd-menu-item" data-quality="audio">
          <div class="fd-menu-item-left">🎵 <span>Audio Only</span></div>
          <span class="fd-menu-badge">320k MP3</span>
        </div>
        <div class="fd-menu-divider"></div>
        <div class="fd-menu-item" data-action="copy">
          <div class="fd-menu-item-left">📋 <span>Copy Video Link</span></div>
        </div>
        <div class="fd-menu-item" data-action="dashboard">
          <div class="fd-menu-item-left">⚡ <span>Open App Dashboard</span></div>
        </div>
      `;
    }

    // 1-Click Direct Download on Pill click
    mainAction.addEventListener('click', (e) => {
      e.stopPropagation();
      e.preventDefault();
      if (isCanvas) {
        recordCanvasStream(targetElement, 15000);
      } else {
        const videoSrc = detectBestMediaUrl(targetElement);
        sendToFastDownloader(videoSrc, document.title, 'video', 'best', pill);
      }
    });

    // Toggle Dropdown Menu on Arrow click
    arrowBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      e.preventDefault();
      const isOpen = menu.classList.toggle('fd-show');
      container.classList.toggle('fd-menu-open', isOpen);
    });

    // Close menu when clicking outside
    document.addEventListener('click', () => {
      menu.classList.remove('fd-show');
      container.classList.remove('fd-menu-open');
    });

    // Menu Item Selection
    menu.querySelectorAll('.fd-menu-item').forEach((item) => {
      item.addEventListener('click', (e) => {
        e.stopPropagation();
        menu.classList.remove('fd-show');
        container.classList.remove('fd-menu-open');

        const action = item.getAttribute('data-action');
        if (action === 'copy') {
          const videoSrc = detectBestMediaUrl(targetElement);
          navigator.clipboard.writeText(videoSrc);
          showToast('📋 Video link copied to clipboard!');
          return;
        }

        if (action === 'dashboard') {
          window.open('http://127.0.0.1:6801/', '_blank');
          return;
        }

        if (isCanvas) {
          const sec = action === 'rec-60' ? 60000 : action === 'rec-30' ? 30000 : 15000;
          recordCanvasStream(targetElement, sec);
        } else {
          const quality = item.getAttribute('data-quality') || 'best';
          const videoSrc = detectBestMediaUrl(targetElement);
          sendToFastDownloader(videoSrc, document.title, 'video', quality, pill);
        }
      });
    });

    container.appendChild(pill);
    container.appendChild(menu);
    parent.appendChild(container);
  }

  // Inject Native Action Bar Button on YouTube Video Page
  function injectYouTubeActionBar() {
    if (!window.location.hostname.includes('youtube.com')) return;
    if (document.getElementById('fd-yt-action-container')) return;

    // YouTube actions toolbar container
    const actionsBar = document.querySelector('#actions #top-level-buttons-computed') || 
                       document.querySelector('#menu #top-level-buttons-computed') ||
                       document.querySelector('ytd-menu-renderer #top-level-buttons-computed');
    if (!actionsBar) return;

    const wrap = document.createElement('div');
    wrap.id = 'fd-yt-action-container';
    wrap.className = 'fd-yt-action-wrap';

    const btn = document.createElement('button');
    btn.className = 'fd-yt-action-btn';
    btn.title = 'Download this YouTube video with FastDownloader (16 Chunks Turbo)';
    btn.innerHTML = `
      <img src="${iconUrl}" alt="FD">
      <span>FastDownload</span>
      <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
        <polyline points="6 9 12 15 18 9"></polyline>
      </svg>
    `;

    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      e.preventDefault();
      sendToFastDownloader(window.location.href, document.title, 'video', 'best');
    });

    actionsBar.appendChild(wrap);
    wrap.appendChild(btn);
  }

  // Main Scan Function
  function scanMedia() {
    // 1. Scan Standard HTML5 Video elements
    document.querySelectorAll('video').forEach((v) => {
      if (v.offsetWidth >= 180 && v.offsetHeight >= 120) {
        injectDownloadButton(v, false);
      }
    });

    // 2. YouTube Main Player
    const ytPlayer = document.getElementById('movie_player') || document.querySelector('.html5-video-player');
    if (ytPlayer && !processedElements.has(ytPlayer)) {
      const v = ytPlayer.querySelector('video');
      if (v) injectDownloadButton(ytPlayer, false);
    }

    // 3. YouTube Action Bar Button below video
    injectYouTubeActionBar();

    // 4. Interactive / Game / WebGL Canvas elements
    document.querySelectorAll('canvas').forEach((c) => {
      if (c.offsetWidth >= 320 && c.offsetHeight >= 220 && !c.closest('.fd-download-btn-container')) {
        injectDownloadButton(c, true);
      }
    });
  }

  // Scan immediately and observe dynamic single-page-app DOM changes
  scanMedia();
  const observer = new MutationObserver(() => scanMedia());
  observer.observe(document.body, { childList: true, subtree: true });
})();
