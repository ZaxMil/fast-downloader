// FastDownloader Main-World Media & Stream Interceptor
// Injected into the page's MAIN execution world to intercept HLS (.m3u8), DASH (.mpd), and media streams
(function () {
  'use strict';
  if (window.__FD_STREAM_INJECTED__) return;
  window.__FD_STREAM_INJECTED__ = true;

  const STREAM_REGEX = /\.(m3u8|mpd|f4m|ism|ts|m4s)(\?|$)/i;
  const recentStreams = [];

  function isMediaStreamUrl(url) {
    if (!url || typeof url !== 'string' || url.startsWith('blob:') || url.startsWith('data:')) return false;
    if (STREAM_REGEX.test(url)) return true;
    const lower = url.toLowerCase();
    if (lower.includes('/hls/') || lower.includes('/dash/') || lower.includes('manifest')) return true;
    if (lower.includes('fbcdn.net') && (lower.includes('.mp4') || lower.includes('/v/t') || lower.includes('bytestart'))) return true;
    if (lower.includes('.mp4?') || lower.endsWith('.mp4') || lower.includes('.webm') || lower.includes('.mkv')) return true;
    return false;
  }

  function recordStream(url) {
    if (!url || typeof url !== 'string' || url.startsWith('blob:') || url.startsWith('data:')) return;
    try {
      let fullUrl = new URL(url, window.location.href).href;

      // For Facebook CDN chunks, strip bytestart/byteend to get the entire continuous video
      if (fullUrl.includes('fbcdn.net')) {
        try {
          const u = new URL(fullUrl);
          u.searchParams.delete('bytestart');
          u.searchParams.delete('byteend');
          fullUrl = u.toString();
        } catch (e) {}
      }

      if (recentStreams.some(s => s.url === fullUrl)) return;

      const isMaster = /\.(m3u8|mpd)(\?|$)/i.test(fullUrl) || fullUrl.includes('fbcdn.net');
      const isChunk = /\.(ts|m4s)(\?|$)/i.test(fullUrl);

      const entry = {
        url: fullUrl,
        type: isMaster ? (fullUrl.includes('.mpd') ? 'dash' : (fullUrl.includes('fbcdn.net') ? 'fbcdn' : 'hls')) : (isChunk ? 'chunk' : 'video'),
        isMaster: isMaster,
        timestamp: Date.now()
      };

      if (isMaster) {
        recentStreams.unshift(entry);
      } else {
        recentStreams.push(entry);
      }
      if (recentStreams.length > 60) recentStreams.pop();

      // Relay to the extension content script
      window.postMessage({
        source: 'FD_STREAM_INTERCEPTOR',
        type: 'NEW_STREAM',
        stream: entry
      }, '*');
    } catch (e) {}
  }

  // 1. Intercept window.fetch
  const origFetch = window.fetch;
  if (typeof origFetch === 'function') {
    window.fetch = async function (...args) {
      try {
        const input = args[0];
        const url = typeof input === 'string' ? input : (input && input.url ? input.url : '');
        if (url && isMediaStreamUrl(url)) {
          recordStream(url);
        }
      } catch (e) {}
      return origFetch.apply(this, args);
    };
  }

  // 2. Intercept XMLHttpRequest
  const origOpen = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function (method, url, ...rest) {
    try {
      if (url && isMediaStreamUrl(url)) {
        recordStream(url);
      }
    } catch (e) {}
    return origOpen.call(this, method, url, ...rest);
  };

  // 3. Monitor MediaSource attachments
  if (window.MediaSource) {
    const origAddSourceBuffer = MediaSource.prototype.addSourceBuffer;
    MediaSource.prototype.addSourceBuffer = function (mimeType) {
      try {
        window.postMessage({
          source: 'FD_STREAM_INTERCEPTOR',
          type: 'MEDIASOURCE_ATTACHED',
          mime: mimeType
        }, '*');
      } catch (e) {}
      return origAddSourceBuffer.call(this, mimeType);
    };
  }

  // 4. Respond to explicit queries from Content Script
  window.addEventListener('message', (e) => {
    if (e.source !== window || !e.data || e.data.source !== 'FD_CONTENT_QUERY') return;
    if (e.data.type === 'GET_STREAMS') {
      window.postMessage({
        source: 'FD_STREAM_INTERCEPTOR',
        type: 'ALL_STREAMS',
        streams: recentStreams
      }, '*');
    }
  });
})();
