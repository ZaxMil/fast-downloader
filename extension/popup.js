document.addEventListener('DOMContentLoaded', async () => {
  const dot = document.getElementById('dot');
  const statusText = document.getElementById('statusText');
  const urlInput = document.getElementById('urlInput');
  const downloadBtn = document.getElementById('downloadBtn');
  const interceptToggle = document.getElementById('interceptToggle');

  // Load user settings
  chrome.storage.local.get({ autoIntercept: true }, (items) => {
    if (interceptToggle) {
      interceptToggle.checked = !!items.autoIntercept;
    }
  });

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
      // Notify local desktop app
      fetch('http://127.0.0.1:6801/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ autoCapture: isChecked })
      }).catch(() => {});
    });
  }

  // Check connection to local app & sync auto-grab state
  try {
    const res = await fetch('http://127.0.0.1:6801/api/status');
    if (res.ok) {
      const data = await res.json();
      dot.classList.add('connected');
      statusText.textContent = 'Connected (App Running)';
      if (typeof data.autoCapture === 'boolean' && interceptToggle) {
        interceptToggle.checked = data.autoCapture;
        chrome.storage.local.set({ autoIntercept: data.autoCapture });
      }
    } else {
      statusText.textContent = 'App returned error';
    }
  } catch (e) {
    statusText.textContent = 'App Offline (Launch FastDownloader)';
  }

  // Pre-fill active tab URL if on a video or page
  let activeTabUrl = '';
  let activeTabTitle = '';
  chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
    if (tabs[0] && tabs[0].url && !tabs[0].url.startsWith('chrome://')) {
      activeTabUrl = tabs[0].url;
      activeTabTitle = tabs[0].title || '';
      urlInput.placeholder = activeTabUrl.slice(0, 36) + '...';
    }
  });

  downloadBtn.addEventListener('click', async () => {
    let url = urlInput.value.trim();
    if (!url) {
      url = activeTabUrl;
    }
    if (!url) return;

    downloadBtn.textContent = 'Connecting with Cookies...';

    // Send request via background worker to capture cookies and headers
    chrome.runtime.sendMessage({
      type: 'FD_DOWNLOAD_REQUEST',
      url: url,
      title: activeTabTitle || 'Manual Download',
      quality: 'best',
      mediaType: 'auto',
      referer: activeTabUrl
    }, (response) => {
      if (response && response.ok) {
        downloadBtn.textContent = '✅ Sent to FastDownloader!';
        setTimeout(() => window.close(), 1200);
      } else {
        downloadBtn.textContent = '⚠️ Could not connect to app';
        setTimeout(() => {
          downloadBtn.textContent = 'Download with Cookies & 16x';
        }, 2500);
      }
    });
  });
});
