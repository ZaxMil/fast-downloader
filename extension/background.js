// FastDownloader Extension Background Worker (v0.1.0-beta)
// IDM-Grade Session & Cookie Handoff + Automatic Download Interception

const FD_API_BASE = "http://127.0.0.1:6801";

// List of file extensions to intercept automatically
const INTERCEPT_EXTENSIONS = new Set([
  "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso", "img",
  "exe", "msi", "dmg", "pkg", "apk", "bin",
  "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm",
  "mp3", "flac", "wav", "m4a", "aac", "ogg",
  "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
  "torrent"
]);

// 1. EXTRACT COOKIES FOR TARGET URL
async function getCookiesForUrl(url) {
  if (!url || typeof url !== "string" || !url.startsWith("http")) return "";
  try {
    const cookies = await chrome.cookies.getAll({ url: url });
    if (!cookies || !cookies.length) return "";
    return cookies.map(c => `${c.name}=${c.value}`).join("; ");
  } catch (err) {
    return "";
  }
}

// 2. SYNTHESIZE FULL BROWSER ANTI-BOT FINGERPRINT HEADERS
function getBrowserClientHeaders(url, referer) {
  const headers = {};

  // Standard high-fidelity browser Accept headers
  headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";
  headers["Accept-Language"] = (navigator.languages && navigator.languages.length) ? navigator.languages.join(",") : (navigator.language || "en-US,en;q=0.9");
  headers["Upgrade-Insecure-Requests"] = "1";

  // Modern Client Hints (Sec-CH-UA)
  if (navigator.userAgentData && navigator.userAgentData.brands && navigator.userAgentData.brands.length) {
    const brands = navigator.userAgentData.brands.map(b => `"${b.brand}";v="${b.version}"`).join(", ");
    headers["Sec-Ch-Ua"] = brands;
    headers["Sec-Ch-Ua-Mobile"] = navigator.userAgentData.mobile ? "?1" : "?0";
    headers["Sec-Ch-Ua-Platform"] = `"${navigator.userAgentData.platform || "Windows"}"`;
  } else {
    const isEdge = navigator.userAgent.includes("Edg/");
    const chromeVerMatch = navigator.userAgent.match(/Chrome\/(\d+)/);
    const ver = chromeVerMatch ? chromeVerMatch[1] : "131";
    if (isEdge) {
      headers["Sec-Ch-Ua"] = `"Chromium";v="${ver}", "Not_A Brand";v="24", "Microsoft Edge";v="${ver}"`;
    } else {
      headers["Sec-Ch-Ua"] = `"Chromium";v="${ver}", "Not_A Brand";v="24", "Google Chrome";v="${ver}"`;
    }
    headers["Sec-Ch-Ua-Mobile"] = "?0";
    headers["Sec-Ch-Ua-Platform"] = '"Windows"';
  }

  // Sec-Fetch-* metadata
  headers["Sec-Fetch-Dest"] = "document";
  headers["Sec-Fetch-Mode"] = "navigate";
  headers["Sec-Fetch-Site"] = referer ? "cross-site" : "none";
  headers["Sec-Fetch-User"] = "?1";

  return headers;
}

// 3. DISPATCH DOWNLOAD TO FASTDOWNLOADER APP (Chrome Native Messaging + HTTP Bridge)
async function sendViaNativeMessaging(payload) {
  return new Promise((resolve) => {
    if (!chrome.runtime || !chrome.runtime.sendNativeMessage) {
      return resolve(null);
    }
    try {
      chrome.runtime.sendNativeMessage("com.fastdownloader.app", payload, (response) => {
        if (chrome.runtime.lastError) {
          console.log("[FastDownloader] Native messaging note:", chrome.runtime.lastError.message);
          resolve(null);
        } else {
          resolve(response || { ok: true });
        }
      });
    } catch (e) {
      console.warn("[FastDownloader] Native messaging exception:", e);
      resolve(null);
    }
  });
}

async function sendDownloadToApp({ url, title, type = "auto", quality = "best", referer = "", filename = "", subfolder = "", category = "" }) {
  if (!url) return { ok: false, error: "Missing URL" };

  const cookies = await getCookiesForUrl(url);
  const clientHeaders = getBrowserClientHeaders(url, referer);

  const payload = {
    action: "download",
    url: url,
    title: title || "Download",
    type: type,
    quality: quality,
    cookies: cookies,
    userAgent: navigator.userAgent,
    referer: referer || url,
    filename: filename || "",
    subfolder: subfolder || category || "",
    category: category || subfolder || "",
    headers: clientHeaders
  };

  // 1. First priority: IDM-grade Chrome Native Messaging (Auto-launches app, zero port conflict)
  try {
    const nativeRes = await sendViaNativeMessaging(payload);
    if (nativeRes && (nativeRes.ok || nativeRes.gid)) {
      return { ok: true, data: nativeRes, transport: "native" };
    }
  } catch (err) {
    console.warn("[FastDownloader] Native messaging attempt error, falling back to HTTP:", err);
  }

  // 2. Fallback: Localhost HTTP Bridge (Port 6801)
  try {
    const resp = await fetch(`${FD_API_BASE}/api/download`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (resp.ok) {
      const data = await resp.json().catch(() => ({}));
      return { ok: true, data: data, transport: "http" };
    }
    return { ok: false, error: "FastDownloader returned error" };
  } catch (err) {
    console.warn("FastDownloader daemon not reachable on port 6801:", err);
    return { ok: false, error: "App unreachable" };
  }
}

// 2.5 OPEN FASTDOWNLOADER APP (Native Launch & Focus with HTTP Fallback)
async function openFastDownloaderApp() {
  // 1. Try Native Messaging first (launches process if stopped, restores and focuses window)
  try {
    const nativeRes = await sendViaNativeMessaging({ action: "openApp" });
    if (nativeRes && (nativeRes.ok || nativeRes.message)) {
      return { ok: true, source: "native" };
    }
  } catch (e) {}

  // 2. Try HTTP daemon /api/openapp
  try {
    const resp = await fetch(`${FD_API_BASE}/api/openapp`, { method: "POST" });
    if (resp.ok) return { ok: true, source: "http" };
  } catch (e) {}

  // 3. Fallback: Open web dashboard tab
  try {
    chrome.tabs.create({ url: `${FD_API_BASE}/` });
    return { ok: true, source: "web" };
  } catch (e) {
    return { ok: false, error: e.message };
  }
}

// 3. CONTEXT MENUS SETUP (IDM-Grade Right-Click Menus)
function setupContextMenus() {
  chrome.contextMenus.removeAll(() => {
    // 0. Open App
    chrome.contextMenus.create({
      id: "fd-open-app",
      title: "⚡ Open FastDownloader App",
      contexts: ["action", "page", "video", "link"]
    });

    // 1. Direct Link Download (16x Threads)
    chrome.contextMenus.create({
      id: "fd-download-link",
      title: "⚡ Fast Download Link (16x Turbo)",
      contexts: ["link"]
    });

    // 2. Video - Best Quality
    chrome.contextMenus.create({
      id: "fd-download-video-best",
      title: "⚡ Fast Download Video (Best Quality)",
      contexts: ["video"]
    });

    // 3. Audio Extraction (MP3)
    chrome.contextMenus.create({
      id: "fd-download-video-audio",
      title: "🎵 Extract Audio (MP3)",
      contexts: ["video", "audio"]
    });

    // 4. Image Download
    chrome.contextMenus.create({
      id: "fd-download-image",
      title: "⚡ Fast Download Image",
      contexts: ["image"]
    });

    // 5. Highlighted Selection (Extract all URLs / Magnets)
    chrome.contextMenus.create({
      id: "fd-download-selection",
      title: "⚡ Download Selected Links",
      contexts: ["selection"]
    });

    // 6. Page-wide Batch Grabber
    chrome.contextMenus.create({
      id: "fd-download-all",
      title: "⚡ Download All Links with FastDownloader...",
      contexts: ["page", "frame"]
    });
  });
}

// Ensure menus are created immediately on script evaluation, install, and browser startup
setupContextMenus();

chrome.runtime.onInstalled.addListener(() => {
  setupContextMenus();
  sendHeartbeat();
});

chrome.runtime.onStartup.addListener(() => {
  setupContextMenus();
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  const referer = tab ? tab.url : "";
  const tabTitle = tab ? tab.title : "";

  // 0. Open App
  if (info.menuItemId === "fd-open-app") {
    await openFastDownloaderApp();
    return;
  }

  // 1. Single Link Download
  if (info.menuItemId === "fd-download-link") {
    const targetUrl = info.linkUrl;
    if (!targetUrl) return;

    let filename = "";
    try {
      const u = new URL(targetUrl);
      filename = u.pathname.split("/").pop() || "";
    } catch (e) {}

    const res = await sendDownloadToApp({
      url: targetUrl,
      title: filename || tabTitle || "Direct Link Download",
      referer: referer,
      filename: filename,
      type: "auto",
      quality: "best"
    });

    if (tab && tab.id) {
      chrome.tabs.sendMessage(tab.id, {
        type: "FD_SHOW_TOAST",
        message: res.ok ? "⚡ Fast Download started!" : "⚠️ App unreachable. Start FastDownloader.",
        isError: !res.ok
      }).catch(() => {});
    }
    return;
  }

  // 2. Video - Best Quality
  if (info.menuItemId === "fd-download-video-best") {
    const targetUrl = info.srcUrl || info.pageUrl;
    const res = await sendDownloadToApp({
      url: targetUrl,
      title: tabTitle || "Video Download",
      referer: referer,
      type: "video",
      quality: "best"
    });
    if (tab && tab.id) {
      chrome.tabs.sendMessage(tab.id, {
        type: "FD_SHOW_TOAST",
        message: res.ok ? "⚡ Video download sent in Best Quality!" : "⚠️ App unreachable.",
        isError: !res.ok
      }).catch(() => {});
    }
    return;
  }

  // 3. Audio Extraction (MP3)
  if (info.menuItemId === "fd-download-video-audio") {
    const targetUrl = info.srcUrl || info.pageUrl;
    const res = await sendDownloadToApp({
      url: targetUrl,
      title: tabTitle || "Audio Track",
      referer: referer,
      type: "audio",
      quality: "audio"
    });
    if (tab && tab.id) {
      chrome.tabs.sendMessage(tab.id, {
        type: "FD_SHOW_TOAST",
        message: res.ok ? "🎵 Audio extraction sent to FastDownloader!" : "⚠️ App unreachable.",
        isError: !res.ok
      }).catch(() => {});
    }
    return;
  }

  // 4. Image Download
  if (info.menuItemId === "fd-download-image") {
    const targetUrl = info.srcUrl;
    if (!targetUrl) return;
    let filename = "";
    try {
      filename = new URL(targetUrl).pathname.split("/").pop() || "";
    } catch (e) {}
    await sendDownloadToApp({
      url: targetUrl,
      title: filename || "Image Download",
      referer: referer,
      filename: filename,
      type: "image"
    });
    return;
  }

  // 5. Selected Links Grabber
  if (info.menuItemId === "fd-download-selection") {
    const text = info.selectionText || "";
    const urlRegex = /(https?:\/\/[^\s"'<>]+|magnet:\?xt=[^\s"'<>]+)/gi;
    const matches = text.match(urlRegex) || [];
    if (!matches.length) {
      if (tab && tab.id) {
        chrome.tabs.sendMessage(tab.id, {
          type: "FD_SHOW_TOAST",
          message: "⚠️ No valid URLs found in selection.",
          isError: true
        }).catch(() => {});
      }
      return;
    }

    let queued = 0;
    for (const u of matches) {
      const cleanUrl = u.replace(/[.,;:)\]>]+$/, "");
      sendDownloadToApp({
        url: cleanUrl,
        title: "Selected Link",
        referer: referer,
        type: "auto"
      });
      queued++;
    }

    if (tab && tab.id) {
      chrome.tabs.sendMessage(tab.id, {
        type: "FD_SHOW_TOAST",
        message: `⚡ Queued <strong>${queued}</strong> selected link(s) in FastDownloader!`
      }).catch(() => {});
    }
    return;
  }

  // 6. Download All Links on Page (Batch Grabber)
  if (info.menuItemId === "fd-download-all") {
    if (!tab || !tab.id) return;
    chrome.tabs.sendMessage(tab.id, { type: "FD_COLLECT_ALL_LINKS" }, async (resp) => {
      if (chrome.runtime.lastError || !resp || !resp.links || !resp.links.length) {
        chrome.tabs.sendMessage(tab.id, {
          type: "FD_SHOW_TOAST",
          message: "ℹ️ No downloadable archives or media found on this page.",
          isError: false
        }).catch(() => {});
        return;
      }

      for (const item of resp.links) {
        await sendDownloadToApp({
          url: item.url,
          title: item.title,
          referer: referer,
          filename: item.filename || "",
          type: "auto"
        });
      }
    });
  }
});

// 4. CONTENT & POPUP MESSAGE BUS
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === "FD_OPEN_APP") {
    openFastDownloaderApp().then(res => sendResponse(res));
    return true;
  }

  if (request.type === "FD_DOWNLOAD_REQUEST") {
    const referer = request.referer || (sender.tab ? sender.tab.url : "");
    const title = request.title || (sender.tab ? sender.tab.title : "");
    sendDownloadToApp({
      url: request.url,
      title: title,
      type: request.mediaType || "auto",
      quality: request.quality || "best",
      referer: referer,
      filename: request.filename || "",
      subfolder: request.subfolder || request.category || "",
      category: request.category || request.subfolder || ""
    }).then(result => {
      sendResponse(result);
    });
    return true; // Keep channel open for async response
  }

  if (request.type === "FD_PROBE_REQUEST") {
    (async () => {
      const url = request.url;
      if (!url) return sendResponse({ ok: false, error: "Missing URL" });

      const cookies = await getCookiesForUrl(url);
      const clientHeaders = getBrowserClientHeaders(url, request.referer || "");
      const probePayload = {
        action: "probe",
        url: url,
        cookies: cookies,
        userAgent: navigator.userAgent,
        headers: clientHeaders
      };

      // Try Native Messaging first
      try {
        const nativeRes = await sendViaNativeMessaging(probePayload);
        if (nativeRes && nativeRes.ok) {
          return sendResponse({ ok: true, data: nativeRes });
        }
      } catch (e) {}

      // Fallback to HTTP API
      try {
        const resp = await fetch(`${FD_API_BASE}/api/probe`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(probePayload)
        });
        if (resp.ok) {
          const data = await resp.json();
          return sendResponse({ ok: true, data: data });
        }
      } catch (e) {}

      sendResponse({ ok: false, error: "Probe unavailable" });
    })();
    return true;
  }

  if (request.type === "FD_GET_SETTINGS") {
    chrome.storage.local.get({ autoIntercept: true }, (items) => {
      sendResponse(items);
    });
    return true;
  }

  if (request.type === "FD_SET_SETTINGS") {
    if (request.settings && typeof request.settings.autoIntercept === "boolean") {
      cachedAutoIntercept = request.settings.autoIntercept;
    }
    chrome.storage.local.set(request.settings, () => {
      sendResponse({ ok: true });
    });
    return true;
  }
});

// In-memory cache for zero-latency intercept checks
let cachedAutoIntercept = true;
chrome.storage.local.get({ autoIntercept: true }, (items) => {
  cachedAutoIntercept = !!items.autoIntercept;
});
chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "local" && changes.autoIntercept !== undefined) {
    cachedAutoIntercept = !!changes.autoIntercept.newValue;
  }
});

// 5. AUTOMATIC BROWSER DOWNLOAD INTERCEPTION (IDM MODE)
function shouldInterceptDownload(url, filename, mime) {
  if (!url || typeof url !== "string") return false;
  if (url.startsWith("blob:") || url.startsWith("data:") || url.startsWith("chrome:") || url.startsWith("edge:") || url.startsWith("brave:")) {
    return false;
  }
  // Check extension from filename or url path
  const target = (filename || (url.includes("/") ? new URL(url).pathname : "")).toLowerCase();
  const dotIdx = target.lastIndexOf(".");
  if (dotIdx !== -1) {
    const ext = target.substring(dotIdx + 1).split(/[?#]/)[0];
    if (INTERCEPT_EXTENSIONS.has(ext)) return true;
  }
  // Check MIME
  if (mime && (mime.includes("application/zip") || mime.includes("application/x-rar") || mime.includes("application/x-msdownload") || mime.includes("application/octet-stream"))) {
    return true;
  }
  return false;
}

chrome.downloads.onCreated.addListener(async (item) => {
  try {
    if (!cachedAutoIntercept) return;
    const settings = await chrome.storage.local.get({ autoIntercept: true });
    if (!settings.autoIntercept) return;

    const targetUrl = item.finalUrl || item.url;
    if (!shouldInterceptDownload(targetUrl, item.filename, item.mime)) return;

    // Prevent loop for localhost downloads
    if (targetUrl.includes("127.0.0.1:6801") || targetUrl.includes("127.0.0.1:6800")) return;

    // Cancel native browser download
    chrome.downloads.cancel(item.id, () => {
      chrome.downloads.erase({ id: item.id }, () => {});
    });

    // Forward to FastDownloader as an intercepted download (triggers Smart Pre-Flight confirmation dialog)
    const cookies = await getCookiesForUrl(targetUrl);
    const clientHeaders = getBrowserClientHeaders(targetUrl, item.referrer || "");
    const cleanFilename = item.filename ? item.filename.split(/[\\/]/).pop() : "";

    const payload = {
      action: "intercept",
      url: targetUrl,
      title: cleanFilename || "Browser Download",
      filename: cleanFilename,
      referer: item.referrer || "",
      cookies: cookies,
      userAgent: navigator.userAgent,
      headers: clientHeaders
    };

    let handled = false;
    try {
      const nativeRes = await sendViaNativeMessaging(payload);
      if (nativeRes && nativeRes.ok) handled = true;
    } catch (e) {}

    if (!handled) {
      try {
        await fetch(`${FD_API_BASE}/api/intercept`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload)
        });
        handled = true;
      } catch (e) {}
    }

    // Wake and focus the FastDownloader app window
    await openFastDownloaderApp();
  } catch (e) {
    console.error("Failed to intercept download:", e);
  }
});

// 6. PERIODIC HEARTBEAT TO FASTDOWNLOADER DAEMON (Syncs auto-grab setting)
function sendHeartbeat() {
  fetch(`${FD_API_BASE}/api/extension/ping`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      version: "0.1.0",
      userAgent: navigator.userAgent
    })
  })
  .then(res => res.json())
  .then(data => {
    if (data && typeof data.autoCapture === "boolean") {
      if (cachedAutoIntercept !== data.autoCapture) {
        cachedAutoIntercept = data.autoCapture;
        chrome.storage.local.set({ autoIntercept: data.autoCapture });
      }
    }
  })
  .catch(() => {});
}

sendHeartbeat();
setInterval(sendHeartbeat, 10000);
