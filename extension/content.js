// FastDownloader Pro - Floating Video Download Bar & In-Page Media Interceptor

(function () {

  'use strict';



  const FD_PORT = 6801;

  const processedElements = new WeakSet();

  const fdLightningSvg = '<svg class="fd-pill-icon" viewBox="0 0 24 24" width="17" height="17" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3v12m-5-5 5 5 5-5"/><path d="M5 17v3h14v-3"/></svg>';



  // Intercepted Main-World Streams cache (HLS, DASH, Master playlists)
  const interceptedStreams = [];

  // User Setting: Floating Video Button Enabled & Remembered Custom Position
  let videoButtonEnabled = true;
  let globalButtonPos = null;

  if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
    chrome.storage.local.get({ videoButtonEnabled: true, savedButtonPos: null }, (items) => {
      if (items) {
        if (typeof items.videoButtonEnabled === 'boolean') {
          videoButtonEnabled = items.videoButtonEnabled;
          if (!videoButtonEnabled) {
            document.querySelectorAll('.fd-download-btn-container').forEach(el => el.remove());
            const ytBtn = document.getElementById('fd-yt-action-container');
            if (ytBtn) ytBtn.remove();
          }
        }
        if (items.savedButtonPos) {
          globalButtonPos = items.savedButtonPos;
        }
      }
    });

    chrome.storage.onChanged.addListener((changes, area) => {
      if (area === 'local') {
        if (changes.videoButtonEnabled !== undefined) {
          videoButtonEnabled = !!changes.videoButtonEnabled.newValue;
          if (!videoButtonEnabled) {
            document.querySelectorAll('.fd-download-btn-container').forEach(el => el.remove());
            const ytBtn = document.getElementById('fd-yt-action-container');
            if (ytBtn) ytBtn.remove();
          } else {
            scanMedia();
          }
        }
        if (changes.savedButtonPos !== undefined) {
          globalButtonPos = changes.savedButtonPos.newValue;
        }
      }
    });
  }



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

          ${fdLightningSvg}

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



  // Helper to clean tracking params from social media URLs

  function cleanSocialUrl(href) {

    if (!href) return '';

    try {

      const u = new URL(href);

      u.searchParams.delete('__cft__[0]');

      u.searchParams.delete('__cft__');

      u.searchParams.delete('__tn__');

      u.searchParams.delete('fbclid');

      u.searchParams.delete('notif_id');

      u.searchParams.delete('notif_t');

      u.searchParams.delete('ref');

      return u.toString();

    } catch (e) {

      return href;

    }

  }



  // Social Post / Group Permalink Sniffer

  function findSocialPostPermalink(element) {

    const isFb = window.location.hostname.includes('facebook.com') || window.location.hostname.includes('fb.watch');

    if (isFb) {

      // 1. Direct watch or reel path in current browser tab

      if (window.location.pathname.includes('/videos/') || window.location.pathname.includes('/reel/') || window.location.pathname.includes('/watch') || window.location.search.includes('v=')) {

        return cleanSocialUrl(window.location.href);

      }



      if (element) {

        // 2. Check if element or parent has a direct link

        const aParent = element.closest('a[href]');

        if (aParent && aParent.href && (aParent.href.includes('/videos/') || aParent.href.includes('/reel/') || aParent.href.includes('/watch') || aParent.href.includes('watch?v='))) {

          return cleanSocialUrl(aParent.href);

        }



        // 3. Climb up parent hierarchy from element to find the post permalink
        let curr = element;
        for (let depth = 0; depth < 16 && curr && curr !== document.body; depth++) {
          const links = curr.querySelectorAll("a[href]");
          for (const a of links) {
            const href = a.href || "";
            if (href.includes("/videos/") || href.includes("/reel/") || href.includes("/watch") || href.includes("watch?v=")) {
              return cleanSocialUrl(href);
            }
          }
          for (const a of links) {
            const href = a.href || "";
            if (href.includes("/posts/") || href.includes("/permalink/") || href.includes("story_fbid=") || href.includes("permalink.php") || href.includes("story.php")) {
              return cleanSocialUrl(href);
            }
          }
          curr = curr.parentElement;
        }
      }



      // 4. Check open dialog or theater mode overlay

      const dialogLink = document.querySelector('div[role="dialog"] a[href*="/videos/"], div[role="dialog"] a[href*="/reel/"], div[role="dialog"] a[href*="watch?v="]');

      if (dialogLink && dialogLink.href) return cleanSocialUrl(dialogLink.href);



      // 5. Check canonical og:url

      const og = document.querySelector('meta[property="og:url"]');

      if (og && og.content && (og.content.includes('/videos/') || og.content.includes('/reel/') || og.content.includes('/watch') || og.content.includes('/posts/'))) {

        return cleanSocialUrl(og.content);

      }



      // 6. Fallback if on a specific Facebook page

      if (window.location.href && !window.location.href.endsWith('facebook.com/') && !window.location.href.endsWith('facebook.com')) {

        return cleanSocialUrl(window.location.href);

      }

    }



    const isIg = window.location.hostname.includes('instagram.com');

    if (isIg) {

      if (window.location.pathname.includes('/p/') || window.location.pathname.includes('/reel/')) {

        return cleanSocialUrl(window.location.href);

      }

      if (element) {

        const container = element.closest('article, main') || document.body;

        const link = container.querySelector('a[href*="/p/"], a[href*="/reel/"]');

        if (link && link.href) return cleanSocialUrl(link.href);

      }

    }



    return null;

  }



  // Detect Best Media / Stream URL (Sniffs HLS, DASH, or direct src)

  function detectBestMediaUrl(element) {

    if (!element) return window.location.href;



    // 1. Social Platforms (Facebook, Instagram) - ALWAYS prefer the post/reel/video permalink!

    // Facebook and Instagram videos are DASH MSE streams; yt-dlp requires the post URL to extract full video+audio!

    const isFb = window.location.hostname.includes('facebook.com') || window.location.hostname.includes('fb.watch');

    if (isFb) {

      const socialUrl = findSocialPostPermalink(element);

      if (socialUrl) return socialUrl;

      if (window.location.pathname.includes('/videos/') || window.location.pathname.includes('/reel/') || window.location.pathname.includes('/watch') || window.location.search.includes('v=')) {

        return cleanSocialUrl(window.location.href);

      }

    }



    const isIg = window.location.hostname.includes('instagram.com');

    if (isIg) {

      const socialUrl = findSocialPostPermalink(element);

      if (socialUrl) return socialUrl;

      if (window.location.pathname.includes('/p/') || window.location.pathname.includes('/reel/')) {

        return cleanSocialUrl(window.location.href);

      }

    }



    // 2. Direct video src check (non-blob)

    let src = element.currentSrc || element.src;

    if (src && !src.startsWith('blob:') && !src.startsWith('data:') && src.startsWith('http') && !src.includes('fbcdn.net')) {

      return src;

    }

    const sources = element.querySelectorAll('source');

    for (const s of sources) {

      if (s.src && !s.src.startsWith('blob:') && !s.src.startsWith('data:') && s.src.startsWith('http') && !s.src.includes('fbcdn.net')) {

        return s.src;

      }

    }



    // 3. Intercepted Main-World Master Stream (.m3u8, .mpd) - Ignore raw chunk fragments

    const masterStream = interceptedStreams.find(s => s.isMaster && !s.url.includes('fbcdn.net'));

    if (masterStream) {

      return masterStream.url;

    }



    // 4. Check network performance entries for real HLS/DASH master manifests (.m3u8, .mpd)

    try {

      const entries = performance.getEntriesByType('resource');

      for (let i = entries.length - 1; i >= 0 && i >= entries.length - 60; i--) {

        const name = entries[i].name;

        if (!name || typeof name !== 'string') continue;

        if (/\.(m3u8|mpd)(\?.*)?$/i.test(name)) {

          return name;

        }

      }

    } catch (e) {}



    // 5. Any recent non-chunk intercepted stream

    const validStream = interceptedStreams.find(s => !s.url.includes('fbcdn.net') && (s.url.includes('.m3u8') || s.url.includes('.mpd') || s.url.includes('.mp4')));

    if (validStream) {

      return validStream.url;

    }



    // 6. Social post permalink fallback

    const socialUrl = findSocialPostPermalink(element);

    if (socialUrl) {

      return socialUrl;

    }



    return window.location.href;

  }



  // Send Download Task to FastDownloader Desktop Backend (Via Extension Service Worker for Cookie Auth)

  // One request with truthful UI feedback; no fake success before acknowledgement.
  async function sendToFastDownloader(url, title, type = 'video', quality = 'best', triggerPill = null) {
    const label = triggerPill ? triggerPill.querySelector('.fd-pill-text') : null;
    const originalLabel = label ? label.textContent : '';
    let accepted = false;

    if (triggerPill && triggerPill.classList.contains('fd-busy')) return false;
    if (triggerPill) triggerPill.classList.add('fd-busy');
    if (label) label.textContent = 'Sending…';

    try {
      if (window.location.hostname.includes('facebook.com') ||
          window.location.hostname.includes('fb.watch')) {
        if (!title || title === 'Facebook' || title === 'Log into Facebook' ||
            title.includes('unknown_video')) {
          const h1 = document.querySelector('h1, [data-pagelet*="Reel"] h1, [role="main"] h1');
          title = h1 && h1.textContent.trim() ? h1.textContent.trim().substring(0, 60) : '';
        }
        if (!url || url.includes('fbcdn.net') || url === 'https://www.facebook.com/' ||
            url === 'https://facebook.com/' || url === 'https://www.facebook.com') {
          const related = triggerPill ? triggerPill.closest(
            'article, [role="article"], [data-pagelet], main') : null;
          url = findSocialPostPermalink(related) || cleanSocialUrl(window.location.href);
        }
      }

      if (typeof chrome === 'undefined' || !chrome.runtime ||
          typeof chrome.runtime.sendMessage !== 'function') {
        showToast('FastDownloader extension is unavailable. Reload this tab.', true);
        return false;
      }

      const payload = {
        type: 'FD_DOWNLOAD_REQUEST',
        url: url || window.location.href,
        title: title || document.title,
        mediaType: quality === 'audio' ? 'audio' : type,
        quality: quality,
        referer: window.location.href
      };

      const result = await new Promise(resolve => {
        let settled = false;
        const timer = setTimeout(() => {
          if (settled) return;
          settled = true;
          resolve({ ok: false, error: 'timeout' });
        }, 14000);
        try {
          chrome.runtime.sendMessage(payload, response => {
            if (settled) return;
            settled = true;
            clearTimeout(timer);
            let runtimeError = null;
            try { runtimeError = chrome.runtime.lastError; } catch (_) {}
            resolve(runtimeError ? { ok: false, error: 'extension' } : (response || null));
          });
        } catch (err) {
          if (settled) return;
          settled = true;
          clearTimeout(timer);
          resolve({ ok: false, error: 'extension' });
        }
      });

      accepted = !!(result && result.ok === true);
      if (accepted) {
        if (label) label.textContent = 'Added ✓';
        showToast(quality === 'audio'
          ? 'Audio request accepted by FastDownloader.'
          : 'Video request accepted by FastDownloader.');
      } else {
        if (label) label.textContent = 'Retry';
        showToast(result && result.error === 'timeout'
          ? 'FastDownloader did not respond. Try again.'
          : 'Download was not accepted. Check the app or reload the extension.', true);
      }
      return accepted;
    } catch (error) {
      if (label) label.textContent = 'Retry';
      showToast('Unable to send download request.', true);
      return false;
    } finally {
      if (triggerPill) setTimeout(() => {
        if (label && label.isConnected) label.textContent = originalLabel;
        triggerPill.classList.remove('fd-busy');
      }, accepted ? 1200 : 750);
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



    // 0. YouTube Shorts Reel Renderer
    const reel = targetElement.closest('ytd-reel-video-renderer');
    if (reel) {
      // In YouTube Shorts, the overlay renderer sits on top of player-container.
      // Attaching to the overlay ensures the button sits cleanly in top-left,
      // completely ABOVE the video and away from the right-side control buttons!
      const overlay = reel.querySelector('ytd-reel-player-overlay-renderer') || reel;
      return overlay;
    }

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

      // Never attach inside a player control bar or bottom toolbar
      const className = typeof current.className === 'string' ? current.className.toLowerCase() : '';
      if (className.includes('controls') || className.includes('toolbar') || role === 'toolbar') {
        current = current.parentElement;
        continue;
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
    if (!videoButtonEnabled) return;
    if (!targetElement || targetElement._fdDismissed) return;

    let anchor = isCanvas ? targetElement.parentElement : findVideoPlayerAnchor(targetElement);
    if (!anchor) anchor = targetElement.parentElement;
    if (!anchor || anchor._fdDismissed) return;

    // Prevent duplicate button in the same anchor or near targetElement
    if (anchor.querySelector(':scope > .fd-download-btn-container') || (targetElement.parentElement && targetElement.parentElement.querySelector(':scope > .fd-download-btn-container'))) {
      return;
    }

    // Ensure anchor is positioning context
    const anchorStyle = window.getComputedStyle(anchor);
    if (anchorStyle.position === 'static') {
      anchor.style.position = 'relative';
    }

    // No dropdown to obscure the player.
    anchor.addEventListener('mouseenter', () => anchor.classList.add('fd-parent-hover'));
    anchor.addEventListener('mouseleave', () => anchor.classList.remove('fd-parent-hover'));

    const container = document.createElement('div');
    container.className = 'fd-download-btn-container';

    // Apply remembered custom position if user previously dragged button
    if (anchor._fdCustomPos) {
      container.style.setProperty('left', anchor._fdCustomPos.left, 'important');
      container.style.setProperty('top', anchor._fdCustomPos.top, 'important');
      container.style.setProperty('right', 'auto', 'important');
    } else if (globalButtonPos && globalButtonPos.left && globalButtonPos.top) {
      container.style.setProperty('left', globalButtonPos.left, 'important');
      container.style.setProperty('top', globalButtonPos.top, 'important');
      container.style.setProperty('right', 'auto', 'important');
    } else {
      container.style.setProperty('top', '14px', 'important');
      container.style.setProperty('left', '14px', 'important');
      container.style.setProperty('right', 'auto', 'important');
    }

    // Stop clicks from bubbling out to Facebook / YouTube background players
    const stopBubbleEvents = ['click', 'dblclick', 'contextmenu'];
    stopBubbleEvents.forEach(evt => {
      container.addEventListener(evt, (e) => {
        e.stopPropagation();
      }, false);
    });

    // Minimal, functional player overlay; no misleading quality menu.
    const pill = document.createElement('div');
    pill.className = 'fd-download-pill';
    pill.setAttribute('role', 'group');
    pill.setAttribute('aria-label', isCanvas ? 'Canvas recorder' : 'Video download');

    const dragHandle = document.createElement('span');
    dragHandle.className = 'fd-pill-drag';
    dragHandle.title = 'Drag to reposition';
    dragHandle.innerHTML = '<svg viewBox="0 0 12 20" width="12" height="20" aria-hidden="true" fill="currentColor"><circle cx="3" cy="5" r="1.3"/><circle cx="9" cy="5" r="1.3"/><circle cx="3" cy="10" r="1.3"/><circle cx="9" cy="10" r="1.3"/><circle cx="3" cy="15" r="1.3"/><circle cx="9" cy="15" r="1.3"/></svg>';

    const mainAction = document.createElement('button');
    mainAction.type = 'button';
    mainAction.className = 'fd-pill-action';
    mainAction.title = isCanvas ? 'Record 15 seconds' : 'Download video with FastDownloader';
    mainAction.setAttribute('aria-label', mainAction.title);
    mainAction.innerHTML = fdLightningSvg + '<span class="fd-pill-text">' +
      (isCanvas ? 'Record 15s' : 'Download') + '</span>';

    const audioBtn = document.createElement('button');
    audioBtn.type = 'button';
    audioBtn.className = 'fd-pill-audio';
    audioBtn.textContent = isCanvas ? '30s' : 'Audio';
    audioBtn.title = isCanvas ? 'Record for 30 seconds' : 'Download audio if the source supports it';
    audioBtn.setAttribute('aria-label', audioBtn.title);

    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'fd-pill-close';
    closeBtn.title = 'Hide on this player';
    closeBtn.setAttribute('aria-label', closeBtn.title);
    closeBtn.innerHTML = '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6 18 18M18 6 6 18"/></svg>';

    pill.appendChild(dragHandle);
    pill.appendChild(mainAction);
    pill.appendChild(audioBtn);
    pill.appendChild(closeBtn);

    // Dragging is exclusive to the grip; action buttons stay clickable.
    let isDragging = false;
    let hasDragged = false;
    let startX = 0, startY = 0, leftAtStart = 0, topAtStart = 0;

    function onPointerMove(e) {
      if (!isDragging) return;
      const dx = e.clientX - startX, dy = e.clientY - startY;
      if (!hasDragged && Math.abs(dx) <= 4 && Math.abs(dy) <= 4) return;
      hasDragged = true;
      container.classList.add('fd-dragging');
      e.preventDefault();
      e.stopPropagation();
      const rect = anchor.getBoundingClientRect();
      const x = Math.min(Math.max(4, rect.width - container.offsetWidth - 4),
        Math.max(4, leftAtStart + dx));
      const y = Math.min(Math.max(4, rect.height - container.offsetHeight - 4),
        Math.max(4, topAtStart + dy));
      container.style.setProperty('left', x + 'px', 'important');
      container.style.setProperty('top', y + 'px', 'important');
      container.style.setProperty('right', 'auto', 'important');
    }
    function onPointerUp(e) {
      window.removeEventListener('pointermove', onPointerMove, true);
      window.removeEventListener('pointerup', onPointerUp, true);
      window.removeEventListener('pointercancel', onPointerUp, true);
      if (!isDragging) return;
      isDragging = false;
      container.classList.remove('fd-dragging');
      if (hasDragged) {
        e.preventDefault();
        e.stopPropagation();
        const pos = { left: container.style.left, top: container.style.top };
        anchor._fdCustomPos = pos;
        globalButtonPos = pos;
        if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local)
          chrome.storage.local.set({ savedButtonPos: pos });
      }
    }
    dragHandle.addEventListener('pointerdown', e => {
      if (e.button !== 0) return;
      e.stopPropagation();
      e.preventDefault();
      isDragging = true;
      hasDragged = false;
      startX = e.clientX;
      startY = e.clientY;
      const current = container.getBoundingClientRect();
      const parent = anchor.getBoundingClientRect();
      leftAtStart = current.left - parent.left;
      topAtStart = current.top - parent.top;
      window.addEventListener('pointermove', onPointerMove, { capture: true, passive: false });
      window.addEventListener('pointerup', onPointerUp, true);
      window.addEventListener('pointercancel', onPointerUp, true);
    });

    closeBtn.addEventListener('click', e => {
      e.stopPropagation();
      e.preventDefault();
      targetElement._fdDismissed = true;
      anchor._fdDismissed = true;
      container.remove();
    });

    let requestBusy = false;
    async function startDownload(e, quality) {
      e.stopPropagation();
      e.preventDefault();
      if (requestBusy) return;
      requestBusy = true;
      try {
        if (isCanvas) {
          recordCanvasStream(targetElement, quality === '30s' ? 30000 : 15000);
          return;
        }
        let url = detectBestMediaUrl(targetElement);
        // Never autoplay or change video volume when detecting a download.
        if (!url || url === window.location.href || url.startsWith('blob:'))
          url = findSocialPostPermalink(targetElement) || window.location.href;
        await sendToFastDownloader(url, document.title, 'video', quality, pill);
      } catch (err) {
        showToast('Could not prepare the download.', true);
      } finally {
        requestBusy = false;
      }
    }

    // Bind exactly one handler per button. Parent click previously doubled requests.
    mainAction.addEventListener('click', e => startDownload(e, isCanvas ? '15s' : 'best'));
    audioBtn.addEventListener('click', e => startDownload(e, isCanvas ? '30s' : 'audio'));

    container.appendChild(pill);
    anchor.appendChild(container);
  }

  // Inject Native Action Bar Button on Video Watch Pages

  function injectNativePlatformActionBar() {
    if (!videoButtonEnabled) return;

    if (!window.location.hostname.includes(atob('eW91dHViZS5jb20='))) return;

    // NEVER inject into YouTube Shorts!
    // Shorts uses a vertical action column where horizontal watch-bar buttons break layout.
    if (window.location.pathname.includes('/shorts/') || document.querySelector('ytd-shorts, ytd-reel-video-renderer')) {
      const existing = document.getElementById('fd-yt-action-container');
      if (existing) existing.remove();
      return;
    }

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

    btn.title = 'Download video with FastDownloader';

    btn.innerHTML = `

      ${fdLightningSvg}

      <span>Download</span>

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
    if (!videoButtonEnabled) return;

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

      if (v) injectDownloadButton(v, false);

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

