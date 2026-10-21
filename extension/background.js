// FastDownloader Extension Background Worker (v1.1.0)
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

// 2. DISPATCH DOWNLOAD TO FASTDOWNLOADER APP
async function sendDownloadToApp({ url, title, type = "auto", quality = "best", referer = "", filename = "" }) {
  if (!url) return { ok: false, error: "Missing URL" };

  const cookies = await getCookiesForUrl(url);

  const payload = {
    url: url,
    title: title || "Download",
    type: type,
    quality: quality,
    cookies: cookies,
    userAgent: navigator.userAgent,
    referer: referer || url,
    filename: filename || ""
  };

  try {
    const resp = await fetch(`${FD_API_BASE}/api/download`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (resp.ok) {
      const data = await resp.json().catch(() => ({}));
      return { ok: true, data: data };
    }
    return { ok: false, error: "FastDownloader returned error" };
  } catch (err) {
    console.warn("FastDownloader daemon not reachable on port 6801:", err);
    return { ok: false, error: "App unreachable" };
  }
}

// 3. CONTEXT MENUS SETUP
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "fd-download-link",
    title: "⚡ Download with FastDownloader",
    contexts: ["link", "video", "audio", "image"]
  });
  sendHeartbeat();
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "fd-download-link") {
    const targetUrl = info.linkUrl || info.srcUrl || info.pageUrl;
    const referer = tab ? tab.url : "";
    const title = tab ? tab.title : "Direct Download";
    await sendDownloadToApp({
      url: targetUrl,
      title: title,
      referer: referer,
      type: "auto",
      quality: "best"
    });
  }
});

// 4. CONTENT & POPUP MESSAGE BUS
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === "FD_DOWNLOAD_REQUEST") {
    const referer = request.referer || (sender.tab ? sender.tab.url : "");
    const title = request.title || (sender.tab ? sender.tab.title : "");
    sendDownloadToApp({
      url: request.url,
      title: title,
      type: request.mediaType || "auto",
      quality: request.quality || "best",
      referer: referer,
      filename: request.filename || ""
    }).then(result => {
      sendResponse(result);
    });
    return true; // Keep channel open for async response
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

    // Forward to FastDownloader with complete cookies and referrer
    await sendDownloadToApp({
      url: targetUrl,
      title: item.filename ? item.filename.split(/[\\/]/).pop() : "Browser Download",
      referer: item.referrer || "",
      filename: item.filename ? item.filename.split(/[\\/]/).pop() : ""
    });
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
      version: "1.1.0",
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
