// FastDownloader Pro - Floating Video Download Bar & In-Page Media Interceptor
(function () {
  'use strict';

  const FD_PORT = 6801;
  const processedElements = new WeakSet();
  const iconUrl = (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.getURL) 
    ? chrome.runtime.getURL('icons/icon32.png') 
    : '';

  // Intercepted Main-World Streams cache (HLS, DASH, Master playlists)
  const interceptedStreams = [];

  window.addEventListener('message', (e) => {
    if (e.source !== window || !e.data || e.data.source !== 'FD_STREAM_INTERCEPTOR') return;
    if (e.data.type === 'NEW_STREAM' && e.data.stream) {
      const s = e.data.stream;
      if (!interceptedStreams.some(x => x.url === s.url)) {
        if (s.isMaster) {
          interceptedStreams.unshift(s);
        } else {
          interceptedStreams.push(s);
        }
        if (interceptedStreams.length > 50) interceptedStreams.pop();
      }
    } else if (e.data.type === 'ALL_STREAMS' && Array.isArray(e.data.streams)) {
      e.data.streams.forEach(s => {
        if (!interceptedStreams.some(x => x.url === s.url)) {
          if (s.isMaster) interceptedStreams.unshift(s);
          else interceptedStreams.push(s);
        }
      });
    }
  });

  // Inject main-world script fallback
  try {
    const s = document.createElement('script');
    s.src = chrome.runtime.getURL('inject.js');
    s.onload = () => s.remove();
    (document.head || document.documentElement).appendChild(s);
    window.postMessage({ source: 'FD_CONTENT_QUERY', type: 'GET_STREAMS' }, '*');
  } catch (e) {}

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
      <div style="display:flex; align-items:center; justify-content:space-between; gap:10px; width:100%;">
        <div style="display:flex; align-items:center; gap:8px;">
          <img src="${iconUrl}" style="width:18px;height:18px;border-radius:4px;flex-shrink:0;">
          <span>${message}</span>
        </div>
        <button id="fd-toast-open-btn" style="background:#10b981; color:#fff; border:none; border-radius:4px; padding:3px 8px; font-size:10px; font-weight:700; cursor:pointer; flex-shrink:0; transition:background 0.15s;">Open App</button>
      </div>
    `;
    const openBtn = toast.querySelector('#fd-toast-open-btn');
    if (openBtn) {
      openBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        try {
          chrome.runtime.sendMessage({ type: 'FD_OPEN_APP' });
        } catch (err) {}
      });
    }
    toast.className = 'fd-toast' + (isError ? ' fd-toast-error' : '');
    toast.classList.add('fd-toast-show');
    clearTimeout(toast._timer);
    toast._timer = setTimeout(() => {
      toast.classList.remove('fd-toast-show');
    }, 4500);
  }

  // Social Post / Group Permalink Sniffer
  function findSocialPostPermalink(element) {
    if (!element) return null;
    const isFb = window.location.hostname.includes('facebook.com') || window.location.hostname.includes('fb.watch');
    if (isFb) {
      if (window.location.pathname.includes('/videos/') || window.location.pathname.includes('/reel/') || window.location.pathname.includes('/watch')) {
        return window.location.href;
      }
      const container = element.closest('article, [role="article"], [data-pagelet], div[data-virtualized="false"], main') || document.body;
      const links = container.querySelectorAll('a[href]');
      for (const a of links) {
        const href = a.href;
        if (!href) continue;
        if (href.includes('/posts/') || href.includes('/permalink/') || href.includes('story_fbid=') || href.includes('permalink.php') || href.includes('story.php') || href.includes('/videos/') || href.includes('/reel/') || href.includes('/watch') || href.includes('watch?v=')) {
          try {
            const u = new URL(href);
            u.searchParams.delete('__cft__[0]');
            u.searchParams.delete('__cft__');
            u.searchParams.delete('__tn__');
            u.searchParams.delete('fbclid');
            return u.toString();
          } catch (e) {
            return href;
          }
        }
      }
    }

    const isIg = window.location.hostname.includes('instagram.com');
    if (isIg) {
      if (window.location.pathname.includes('/p/') || window.location.pathname.includes('/reel/')) {
        return window.location.href;
      }
      const container = element.closest('article, main') || document.body;
      const link = container.querySelector('a[href*="/p/"], a[href*="/reel/"]');
      if (link && link.href) return link.href;
    }

    return null;
  }

  // Detect Best Media / Stream URL (Sniffs HLS, DASH, or direct src)
  function detectBestMediaUrl(element) {
    if (!element) return window.location.href;

    // 1. Direct video src check first!
    let src = element.currentSrc || element.src;
    if (src && !src.startsWith('blob:') && !src.startsWith('data:') && src.startsWith('http')) {
      return src;
    }
    const sources = element.querySelectorAll('source');
    for (const s of sources) {
      if (s.src && !s.src.startsWith('blob:') && !s.src.startsWith('data:') && s.src.startsWith('http')) {
        return s.src;
      }
    }

    // 2. Intercepted Main-World Master Stream (.m3u8, .mpd, fbcdn)
    const masterStream = interceptedStreams.find(s => s.isMaster);
    if (masterStream) {
      return masterStream.url;
    }

    // 3. Check network performance entries for active fbcdn or m3u8/mpd streams
    try {
      const entries = performance.getEntriesByType('resource');
      for (let i = entries.length - 1; i >= 0 && i >= entries.length - 60; i--) {
        const name = entries[i].name;
        if (!name || typeof name !== 'string') continue;
        if (name.includes('fbcdn.net') && (name.includes('/v/t') || name.includes('.mp4') || name.includes('.mpd') || name.includes('bytestart'))) {
          try {
            const u = new URL(name);
            u.searchParams.delete('bytestart');
            u.searchParams.delete('byteend');
            return u.toString();
          } catch (e) {
            return name;
          }
        }
        if (/\.(m3u8|mpd)(\?.*)?$/i.test(name)) {
          return name;
        }
      }
    } catch (e) {}

    // 4. Any recent intercepted stream
    if (interceptedStreams.length > 0) {
      return interceptedStreams[0].url;
    }

    // 5. Social post permalink
    const socialUrl = findSocialPostPermalink(element);
    if (socialUrl) {
      return socialUrl;
    }

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

    if (url === 'https://www.facebook.com/' || url === 'https://facebook.com/' || url === 'https://www.facebook.com' || (window.location.hostname.includes('facebook.com') && (!url || url.startsWith('blob:') || url === window.location.href))) {
      // Sniff performance entries again in case it just buffered
      let fbUrl = null;
      try {
        const entries = performance.getEntriesByType('resource');
        for (let i = entries.length - 1; i >= 0; i--) {
          const name = entries[i].name;
          if (name.includes('fbcdn.net') && (name.includes('/v/t') || name.includes('.mp4') || name.includes('.mpd') || name.includes('bytestart'))) {
            const u = new URL(name);
            u.searchParams.delete('bytestart');
            u.searchParams.delete('byteend');
            fbUrl = u.toString();
            break;
          }
        }
      } catch (e) {}

      if (fbUrl) {
        url = fbUrl;
      } else {
        const permalink = findSocialPostPermalink(triggerPill ? triggerPill.closest('article, [role="article"], [data-pagelet], div[data-virtualized="false"], main') : null);
        if (permalink) {
          url = permalink;
        } else {
          showToast('⚠️ Please play the video for 1 second first so Facebook loads the stream!', true);
          return;
        }
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

    let contextInvalidated = false;
    // Try communicating via background worker first (with full cookie & session extraction)
    if (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.sendMessage) {
      try {
        const res = await new Promise((resolve) => {
          chrome.runtime.sendMessage(payload, (resp) => {
            if (chrome.runtime.lastError) {
              const msg = chrome.runtime.lastError.message || '';
              if (msg.includes('invalidated') || msg.includes('disconnected')) {
                contextInvalidated = true;
              }
              resolve(null);
            } else if (!resp) {
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
      } catch (e) {
        if (e && e.message && e.message.includes('invalidated')) {
          contextInvalidated = true;
        }
      }
    }

    if (contextInvalidated) {
      showToast('⚠️ Extension updated! Please refresh this tab (F5) to re-sync.', true);
      return;
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

  // Find the exact visual player container that bounds the video element
  function findVideoPlayerAnchor(targetElement) {
    if (!targetElement || !targetElement.parentElement) return null;

    // 1. Embedded YouTube Player
    const ytPlayer = targetElement.closest('#movie_player, .html5-video-player');
    if (ytPlayer) return ytPlayer;

    // 2. Generic video players (walk UP to find the HIGHEST player wrapper, ABOVE any pause/resume backdrops)
    const videoRect = targetElement.getBoundingClientRect();
    let current = targetElement.parentElement;
    let highestPlayer = targetElement.parentElement;

    while (current && current !== document.body && current !== document.documentElement) {
      // Never attach to the full social post, feed, or article card
      const role = current.getAttribute('role');
      if (role === 'article' || role === 'feed' || role === 'main' || current.tagName === 'ARTICLE') {
        break;
      }

      const rect = current.getBoundingClientRect();
      if (rect.width >= 80 && rect.height >= 80) {
        if (videoRect.height > 0) {
          // Stay within player bounds (within 1.5x of video height & width)
          if (rect.height <= videoRect.height * 1.5 && rect.width <= videoRect.width * 1.5) {
            highestPlayer = current;
          } else {
            break; // Exceeded player boundary
          }
        } else if (rect.height <= 850) {
          highestPlayer = current;
        }
      }

      current = current.parentElement;
    }

    return highestPlayer;
  }

  // Inject Floating Button onto any Video or Media Player
  function injectDownloadButton(targetElement, isCanvas = false) {
    if (!targetElement) return;

    let anchor = isCanvas ? targetElement.parentElement : findVideoPlayerAnchor(targetElement);
    if (!anchor) anchor = targetElement.parentElement;
    if (!anchor) return;

    // Prevent duplicate button in the same anchor or near targetElement
    if (anchor.querySelector(':scope > .fd-download-btn-container') || (targetElement.parentElement && targetElement.parentElement.querySelector(':scope > .fd-download-btn-container'))) {
      return;
    }

    // Ensure anchor is positioning context
    const anchorStyle = window.getComputedStyle(anchor);
    if (anchorStyle.position === 'static') {
      anchor.style.position = 'relative';
    }

    // Hover helpers
    anchor.addEventListener('mouseenter', () => anchor.classList.add('fd-parent-hover'));
    anchor.addEventListener('mouseleave', () => {
      anchor.classList.remove('fd-parent-hover');
      container.classList.remove('fd-menu-open');
      menu.classList.remove('fd-show');
    });

    const container = document.createElement('div');
    container.className = 'fd-download-btn-container';

    // Stop clicks/pointers from bubbling out to Facebook player behind the container
    const stopBubbleEvents = ['pointerdown', 'mousedown', 'mouseup', 'click', 'dblclick', 'touchstart', 'touchend'];
    stopBubbleEvents.forEach(evt => {
      container.addEventListener(evt, (e) => {
        e.stopPropagation();
      }, false);
    });

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

    // 1-Click Direct Download on Pill or Action click
    const triggerDownload = async (e, quality = 'best') => {
      e.stopPropagation();
      e.preventDefault();
      if (isCanvas) {
        recordCanvasStream(targetElement, 15000);
        return;
      }

      let videoSrc = detectBestMediaUrl(targetElement);

      // Auto-nudge video element silently if paused or has blob src so platform requests stream chunks
      if ((!videoSrc || videoSrc === window.location.href || videoSrc.startsWith('blob:')) && targetElement && targetElement.tagName === 'VIDEO') {
        const textSpan = pill.querySelector('.fd-pill-text');
        if (textSpan) textSpan.textContent = 'Buffering...';

        const wasPaused = targetElement.paused;
        const prevMuted = targetElement.muted;
        try {
          targetElement.muted = true;
          const p = targetElement.play();
          if (p && typeof p.then === 'function') await p.catch(() => {});
        } catch (err) {}

        for (let i = 0; i < 5; i++) {
          await new Promise(r => setTimeout(r, 100));
          videoSrc = detectBestMediaUrl(targetElement);
          if (videoSrc && videoSrc !== window.location.href && !videoSrc.startsWith('blob:')) {
            break;
          }
        }

        if (wasPaused) {
          try { targetElement.pause(); } catch (err) {}
        }
        targetElement.muted = prevMuted;
      }

      if (!videoSrc || videoSrc === window.location.href || videoSrc.startsWith('blob:')) {
        videoSrc = findSocialPostPermalink(targetElement) || window.location.href;
      }

      sendToFastDownloader(videoSrc, document.title, 'video', quality, pill);
    };

    mainAction.addEventListener('click', triggerDownload);
    pill.addEventListener('click', (e) => {
      if (arrowBtn.contains(e.target)) return;
      triggerDownload(e, 'best');
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
          try {
            chrome.runtime.sendMessage({ type: 'FD_OPEN_APP' });
          } catch (err) {
            window.open('http://127.0.0.1:6801/', '_blank');
          }
          return;
        }

        if (isCanvas) {
          const sec = action === 'rec-60' ? 60000 : action === 'rec-30' ? 30000 : 15000;
          recordCanvasStream(targetElement, sec);
        } else {
          const quality = item.getAttribute('data-quality') || 'best';
          triggerDownload(e, quality);
        }
      });
    });

    container.appendChild(pill);
    container.appendChild(menu);
    anchor.appendChild(container);
  }

  // Inject Native Action Bar Button on Video Watch Pages
  function injectNativePlatformActionBar() {
    if (!window.location.hostname.includes(atob('eW91dHViZS5jb20='))) return;
    if (document.getElementById('fd-yt-action-container')) return;

    // Platform actions toolbar container
    const actionsBar = document.querySelector('#actions #top-level-buttons-computed') || 
                       document.querySelector('#menu #top-level-buttons-computed') ||
                       document.querySelector('ytd-menu-renderer #top-level-buttons-computed');
    if (!actionsBar) return;

    const wrap = document.createElement('div');
    wrap.id = 'fd-yt-action-container';
    wrap.className = 'fd-yt-action-wrap';

    const btn = document.createElement('button');
    btn.className = 'fd-yt-action-btn';
    btn.title = 'Download video with FastDownloader (16x Turbo)';
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
      const rect = v.getBoundingClientRect();
      if (v.offsetWidth >= 80 || v.videoWidth >= 80 || rect.width >= 80) {
        injectDownloadButton(v, false);
      }
    });

    // 2. Embedded Video Container
    const ytPlayer = document.getElementById('movie_player') || document.querySelector('.html5-video-player');
    if (ytPlayer && !processedElements.has(ytPlayer)) {
      const v = ytPlayer.querySelector('video');
      if (v) injectDownloadButton(ytPlayer, false);
    }

    // 3. Platform Action Bar Button
    injectNativePlatformActionBar();

    // 4. Interactive / Game / WebGL Canvas elements
    document.querySelectorAll('canvas').forEach((c) => {
      if (c.offsetWidth >= 320 && c.offsetHeight >= 220 && !c.closest('.fd-download-btn-container')) {
        injectDownloadButton(c, true);
      }
    });
  }

  // Listen for messages from background script (Batch grabber & Toast alerts)
  if (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.onMessage) {
    chrome.runtime.onMessage.addListener((req, sender, sendResponse) => {
      if (req.type === 'FD_COLLECT_ALL_LINKS') {
        const found = [];
        const seen = new Set();
        const exts = new Set([
          "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso", "img",
          "exe", "msi", "dmg", "pkg", "apk", "bin",
          "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm",
          "mp3", "flac", "wav", "m4a", "aac", "ogg",
          "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
          "torrent"
        ]);

        // Scan all anchor links
        document.querySelectorAll('a[href]').forEach(a => {
          try {
            const raw = a.href;
            if (!raw || raw.startsWith('javascript:') || raw.startsWith('mailto:') || raw.startsWith('#')) return;
            const u = new URL(raw, window.location.href);
            const path = u.pathname.toLowerCase();
            const dotIdx = path.lastIndexOf('.');
            const ext = dotIdx !== -1 ? path.substring(dotIdx + 1) : '';
            if (exts.has(ext) && !seen.has(u.href)) {
              seen.add(u.href);
              const text = (a.innerText || a.getAttribute('title') || '').trim();
              found.push({
                url: u.href,
                title: text || path.split('/').pop() || 'Download',
                filename: path.split('/').pop() || ''
              });
            }
          } catch (e) {}
        });

        // Scan all audio/video elements
        document.querySelectorAll('video, audio, source').forEach(el => {
          try {
            const src = el.src || el.currentSrc;
            if (src && !src.startsWith('blob:') && !src.startsWith('data:') && !seen.has(src)) {
              seen.add(src);
              found.push({
                url: src,
                title: document.title || 'Media File',
                filename: ''
              });
            }
          } catch (e) {}
        });

        showToast(`⚡ Batch Grabber: Found <strong>${found.length}</strong> downloadable file(s)!`);
        sendResponse({ ok: true, count: found.length, links: found });
        return true;
      }

      if (req.type === 'FD_GET_PAGE_MEDIA') {
        const v = document.querySelector('video');
        let streamUrl = null;
        if (v) {
          streamUrl = detectBestMediaUrl(v);
        }
        sendResponse({
          ok: true,
          hasMedia: !!v || interceptedStreams.length > 0,
          url: streamUrl || (interceptedStreams.length > 0 ? interceptedStreams[0].url : window.location.href),
          title: document.title
        });
        return true;
      }

      if (req.type === 'FD_SHOW_TOAST') {
        showToast(req.message, req.isError);
        sendResponse({ ok: true });
        return true;
      }
    });
  }

  // Scan immediately and observe dynamic single-page-app DOM changes (debounced to save CPU)
  scanMedia();
  let scanDebounceTimer = null;
  const observer = new MutationObserver(() => {
    if (scanDebounceTimer) return;
    scanDebounceTimer = setTimeout(() => {
      scanDebounceTimer = null;
      scanMedia();
    }, 150);
  });
  if (document.body) {
    observer.observe(document.body, { childList: true, subtree: true });
  } else {
    document.addEventListener('DOMContentLoaded', () => {
      observer.observe(document.body, { childList: true, subtree: true });
    });
  }

  // Real-time hover & playback listeners (captures dynamically loaded feed & iframe videos)
  document.addEventListener('play', (e) => {
    if (e.target && e.target.tagName === 'VIDEO') {
      injectDownloadButton(e.target, false);
    }
  }, true);

  document.addEventListener('mouseover', (e) => {
    const el = e.target;
    if (!el) return;
    if (el.tagName === 'VIDEO') {
      injectDownloadButton(el, false);
    } else if (el.closest) {
      const v = el.querySelector('video') || el.closest('div')?.querySelector('video');
      if (v && (v.offsetWidth >= 80 || v.videoWidth >= 80 || v.getBoundingClientRect().width >= 80)) {
        injectDownloadButton(v, false);
      }
    }
  }, { capture: true, passive: true });
})();
