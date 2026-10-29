document.addEventListener('DOMContentLoaded', async () => {
  const dot = document.getElementById('dot');
  const statusText = document.getElementById('statusText');
  const statusBadge = document.getElementById('statusBadge');
  const openAppBtn = document.getElementById('openAppBtn');
  const interceptToggle = document.getElementById('interceptToggle');
  const videoBtnToggle = document.getElementById('videoBtnToggle');

  const mediaCard = document.getElementById('mediaCard');
  const mediaTitle = document.getElementById('mediaTitle');
  const copyMediaBtn = document.getElementById('copyMediaBtn');

  const urlInput = document.getElementById('urlInput');
  const pasteBtn = document.getElementById('pasteBtn');
  const downloadBtn = document.getElementById('downloadBtn');

  const probePreview = document.getElementById('probePreview');
  const probeFileName = document.getElementById('probeFileName');
  const probeSize = document.getElementById('probeSize');
  const probeResumable = document.getElementById('probeResumable');
  const probeCategory = document.getElementById('probeCategory');

  const activityBar = document.getElementById('activityBar');
  const activityText = document.getElementById('activityText');

  let activeTabUrl = '';
  let activeTabTitle = '';
  let activeMediaUrl = '';

  // 1. OPEN DESKTOP APP ACTION
  function openApp() {
    if (openAppBtn) {
      const origHtml = openAppBtn.innerHTML;
      openAppBtn.textContent = '...';
      chrome.runtime.sendMessage({ type: 'FD_OPEN_APP' }, () => {
        setTimeout(() => { openAppBtn.innerHTML = origHtml; }, 1000);
      });
    } else {
      chrome.runtime.sendMessage({ type: 'FD_OPEN_APP' });
    }
  }

  if (openAppBtn) openAppBtn.addEventListener('click', openApp);
  const reloadExtBtn = document.getElementById('reloadExtBtn');
  if (reloadExtBtn) {
    reloadExtBtn.addEventListener('click', () => {
      reloadExtBtn.style.transform = 'rotate(360deg)';
      reloadExtBtn.style.transition = 'transform 0.5s ease';
      chrome.runtime.sendMessage({ type: 'FD_RELOAD_EXTENSION' });
      setTimeout(() => {
        try { chrome.runtime.reload(); } catch(e) {}
        window.close();
      }, 300);
    });
  }
  const extVerEl = document.getElementById('extVerText');
  if (extVerEl && chrome.runtime && chrome.runtime.getManifest) {
    extVerEl.textContent = 'v' + (chrome.runtime.getManifest().version || '0.1.4');
  }
  if (statusBadge) statusBadge.addEventListener('click', openApp);
  if (activityBar) activityBar.addEventListener('click', openApp);

  // 2. AUTO-GRAB & VIDEO BUTTON SETTING SYNC

  const cardAutoGrab = document.getElementById('cardAutoGrab');
  if (cardAutoGrab && interceptToggle) {
    cardAutoGrab.addEventListener('click', (e) => {
      if (e.target !== interceptToggle && !e.target.closest('.switch-wrap')) {
        interceptToggle.checked = !interceptToggle.checked;
        interceptToggle.dispatchEvent(new Event('change'));
      }
    });
  }

  const cardVideoBar = document.getElementById('cardVideoBar');
  if (cardVideoBar && videoBtnToggle) {
    cardVideoBar.addEventListener('click', (e) => {
      if (e.target !== videoBtnToggle && !e.target.closest('.switch-wrap')) {
        videoBtnToggle.checked = !videoBtnToggle.checked;
        videoBtnToggle.dispatchEvent(new Event('change'));
      }
    });
  }

  chrome.storage.local.get({ autoIntercept: true, videoButtonEnabled: true }, (items) => {
    if (interceptToggle) interceptToggle.checked = !!items.autoIntercept;
    if (videoBtnToggle) videoBtnToggle.checked = !!items.videoButtonEnabled;
  });

  if (videoBtnToggle) {
    videoBtnToggle.addEventListener('change', () => {
      chrome.storage.local.set({ videoButtonEnabled: videoBtnToggle.checked });
    });
  }

  if (interceptToggle) {
    interceptToggle.addEventListener('change', () => {
      const isChecked = interceptToggle.checked;
      chrome.storage.local.set({ autoIntercept: isChecked });
      try {
        chrome.runtime.sendMessage({
          type: 'FD_SET_SETTINGS',
          settings: { autoIntercept: isChecked }
        });
      } catch (e) {}
      fetch('http://127.0.0.1:6801/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ autoCapture: isChecked })
      }).catch(() => {});
    });
  }

  // 3. CONNECTION & ACTIVITY POLLING
  async function checkStatusAndActivity() {
    let connected = false;
    try {
      const res = await fetch('http://127.0.0.1:6801/api/status');
      if (res.ok) {
        const data = await res.json();
        connected = true;
        dot.classList.add('connected');
        statusText.textContent = 'Connected';
        if (typeof data.autoCapture === 'boolean' && interceptToggle) {
          interceptToggle.checked = data.autoCapture;
          chrome.storage.local.set({ autoIntercept: data.autoCapture });
        }
      }
    } catch (e) {}

    if (!connected && chrome.runtime.sendNativeMessage) {
      try {
        chrome.runtime.sendNativeMessage('com.fastdownloader.app', { action: 'ping' }, (resp) => {
          if (resp && resp.ok) {
            dot.classList.add('connected');
            statusText.textContent = 'Native Ready';
          } else {
            dot.classList.remove('connected');
            statusText.textContent = 'Offline';
          }
        });
      } catch (e) {
        dot.classList.remove('connected');
        statusText.textContent = 'Offline';
      }
    } else if (!connected) {
      dot.classList.remove('connected');
      statusText.textContent = 'Offline';
    }

    // Check Live Active Downloads
    if (connected && activityText) {
      try {
        const tasksRes = await fetch('http://127.0.0.1:6801/api/tasks');
        if (tasksRes.ok) {
          const tData = await tasksRes.json();
          const tasks = tData.tasks || [];
          const activeTasks = tasks.filter(t => t.status === 'active' || t.status === 'downloading');
          if (activeTasks.length > 0) {
            let totalSpeed = 0;
            activeTasks.forEach(t => totalSpeed += (t.downloadSpeed || 0));
            const speedFormatted = formatBytes(totalSpeed) + '/s';
            activityText.textContent = `${activeTasks.length} downloading • ${speedFormatted}`;
            const dotEl = activityBar.querySelector('.activity-dot');
            if (dotEl) dotEl.style.background = '#10b981';
          } else {
            activityText.textContent = 'Ready • 16x Turbo active';
          }
        }
      } catch (e) {}
    }
  }

  function formatBytes(bytes) {
    if (!bytes || bytes <= 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  // Immediately ping daemon on popup open to notify desktop app
  fetch('http://127.0.0.1:6801/api/extension/ping', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      version: (chrome.runtime && chrome.runtime.getManifest) ? chrome.runtime.getManifest().version : '0.1.6',
      userAgent: navigator.userAgent
    })
  }).catch(() => {});

  checkStatusAndActivity();
  const statusPollInterval = setInterval(checkStatusAndActivity, 2500);
  window.addEventListener('unload', () => clearInterval(statusPollInterval));

  // 4. DETECT PAGE MEDIA ON ACTIVE TAB
  function isMediaPlatform(url) {
    if (!url) return false;
    const lower = url.toLowerCase();
    return lower.includes('youtube.com') || lower.includes('youtu.be') ||
           lower.includes('facebook.com') || lower.includes('fb.watch') ||
           lower.includes('instagram.com') || lower.includes('tiktok.com') ||
           lower.includes('twitter.com') || lower.includes('x.com') ||
           lower.includes('reddit.com') || lower.includes('vimeo.com') ||
           lower.includes('dailymotion.com') || lower.includes('twitch.tv') ||
           /\.(mp4|mkv|webm|m3u8|mpd|mp3|m4a|wav|aac)(\?|$)/i.test(lower);
  }

  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (tabs && tabs[0] && tabs[0].url && !tabs[0].url.startsWith('chrome://')) {
      activeTabUrl = tabs[0].url;
      activeTabTitle = tabs[0].title || 'Web Download';
      activeMediaUrl = activeTabUrl;

      // Ask content script for any detected video / media element
      try {
        chrome.tabs.sendMessage(tabs[0].id, { type: 'FD_GET_PAGE_MEDIA' }, (resp) => {
          if (!chrome.runtime.lastError && resp && resp.ok) {
            if (resp.url) activeMediaUrl = resp.url;
            if (resp.hasMedia || isMediaPlatform(activeTabUrl)) {
              showMediaSection(resp.title || activeTabTitle);
            }
          } else if (isMediaPlatform(activeTabUrl)) {
            showMediaSection(activeTabTitle);
          }
        });
      } catch (e) {
        if (isMediaPlatform(activeTabUrl)) {
          showMediaSection(activeTabTitle);
        }
      }
    }
  });

  function showMediaSection(title) {
    if (!mediaCard) return;
    mediaCard.style.display = 'block';
    if (mediaTitle) {
      let clean = title.replace(/ - YouTube$/, '').replace(/ \| Facebook$/, '').replace(/ \/ X$/, '');
      mediaTitle.textContent = clean;
      mediaTitle.title = clean;
    }
  }

  // Media Quality Buttons Click
  if (mediaCard) {
    mediaCard.querySelectorAll('.pill-btn[data-quality]').forEach(btn => {
      btn.addEventListener('click', () => {
        const quality = btn.getAttribute('data-quality') || 'best';
        const origText = btn.innerHTML;
        btn.innerHTML = '<span>⚡ Sent!</span>';

        chrome.runtime.sendMessage({
          type: 'FD_DOWNLOAD_REQUEST',
          url: activeMediaUrl || activeTabUrl,
          title: activeTabTitle,
          quality: quality,
          mediaType: quality === 'audio' ? 'audio' : 'video',
          referer: activeTabUrl
        }, (res) => {
          if (res && res.ok) {
            setTimeout(() => window.close(), 1000);
          } else {
            setTimeout(() => { btn.innerHTML = origText; }, 2000);
          }
        });
      });
    });
  }

  // Copy Media Link
  if (copyMediaBtn) {
    copyMediaBtn.addEventListener('click', () => {
      const target = activeMediaUrl || activeTabUrl;
      navigator.clipboard.writeText(target);
      copyMediaBtn.title = 'Copied!';
      copyMediaBtn.style.color = '#10b981';
      setTimeout(() => {
        copyMediaBtn.title = 'Copy Video Link';
        copyMediaBtn.style.color = '';
      }, 1500);
    });
  }

  // 5. PASTE FROM CLIPBOARD BUTTON
  if (pasteBtn && urlInput) {
    pasteBtn.addEventListener('click', async () => {
      try {
        const text = await navigator.clipboard.readText();
        if (text && (text.startsWith('http://') || text.startsWith('https://') || text.startsWith('magnet:'))) {
          urlInput.value = text.trim();
          probeUrl(text.trim());
        }
      } catch (err) {}
    });
  }

  // 6. PROBE URL DEBOUNCE
  let probeDebounce = null;
  function probeUrl(targetUrl) {
    clearTimeout(probeDebounce);
    if (!targetUrl || (!targetUrl.startsWith('http://') && !targetUrl.startsWith('https://') && !targetUrl.startsWith('magnet:'))) {
      if (probePreview) probePreview.style.display = 'none';
      return;
    }

    probeDebounce = setTimeout(() => {
      chrome.runtime.sendMessage({
        type: 'FD_PROBE_REQUEST',
        url: targetUrl,
        referer: activeTabUrl
      }, (res) => {
        if (chrome.runtime.lastError || !res || !res.ok || !res.data) {
          if (probePreview) probePreview.style.display = 'none';
          return;
        }
        const data = res.data;
        if (probePreview && probeFileName) {
          probePreview.style.display = 'block';
          probeFileName.textContent = data.filename || 'Direct Download';
          if (probeSize) probeSize.textContent = data.sizeFormatted || 'Unknown size';
          if (probeResumable) probeResumable.textContent = data.resumable ? 'Resumable' : 'Single Stream';
          if (probeCategory) probeCategory.textContent = data.category || 'General';
        }
      });
    }, 280);
  }

  if (urlInput) {
    urlInput.addEventListener('input', () => {
      probeUrl(urlInput.value.trim());
    });
  }

  // 7. MAIN DOWNLOAD CTA BUTTON
  if (downloadBtn) {
    downloadBtn.addEventListener('click', () => {
      let url = urlInput ? urlInput.value.trim() : '';
      if (!url) {
        url = activeMediaUrl || activeTabUrl;
      }
      if (!url) {
        if (urlInput) urlInput.focus();
        return;
      }

      downloadBtn.innerHTML = '<span>⚡ Connecting...</span>';

      chrome.runtime.sendMessage({
        type: 'FD_DOWNLOAD_REQUEST',
        url: url,
        title: (probeFileName && probeFileName.textContent ? probeFileName.textContent : activeTabTitle),
        quality: 'best',
        mediaType: 'auto',
        referer: activeTabUrl,
        category: (probeCategory ? probeCategory.textContent : '')
      }, (response) => {
        if (response && response.ok) {
          downloadBtn.innerHTML = '<span>✓ Sent to FastDownloader!</span>';
          setTimeout(() => window.close(), 1000);
        } else {
          downloadBtn.innerHTML = '<span>⚠️ Could not reach app</span>';
          setTimeout(() => {
            downloadBtn.innerHTML = `
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              <span>Turbo Download (16x Streams)</span>
            `;
          }, 2200);
        }
      });
    });
  }
});
