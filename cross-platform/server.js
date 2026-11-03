'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { DownloadManager, validateUrl } = require('./engine');

const PUBLIC = path.join(__dirname, 'public');
const MIME = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8', '.svg': 'image/svg+xml' };

function json(res, status, data) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(data));
}
function openInSystem(target) {
  let command, args;
  if (process.platform === 'win32') { command = 'explorer.exe'; args = [target]; }
  else if (process.platform === 'darwin') { command = 'open'; args = [target]; }
  else { command = 'xdg-open'; args = [target]; }
  const child = spawn(command, args, { detached: true, stdio: 'ignore', windowsHide: true });
  child.on('error', err => process.stderr.write('Cannot open system viewer: ' + err.message + '\n'));
  child.unref();
}
async function readJson(req) {
  const parts = [];
  let size = 0;
  for await (const part of req) {
    size += part.length;
    if (size > 16 * 1024) throw new Error('Request too large');
    parts.push(part);
  }
  const body = JSON.parse(Buffer.concat(parts).toString('utf8') || '{}');
  if (!body || typeof body !== 'object' || Array.isArray(body)) throw new Error('Expected a JSON object');
  return body;
}
function storage(downloadsDir) {
  try {
    const stat = fs.statfsSync(downloadsDir);
    const totalBytes = stat.blocks * stat.bsize;
    const freeBytes = stat.bavail * stat.bsize;
    return { ok: true, drive: path.parse(downloadsDir).root, totalBytes, freeBytes,
      usedPercent: totalBytes ? Math.round((totalBytes - freeBytes) * 100 / totalBytes) : 0 };
  } catch { return { ok: false, error: 'Disk information unavailable' }; }
}
async function startServer(opts = {}) {
  const manager = opts.manager || new DownloadManager(opts);
  const server = http.createServer(async (req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('X-Frame-Options', 'DENY');
    res.setHeader('Referrer-Policy', 'no-referrer');
    res.setHeader('Content-Security-Policy',
      "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'");
    const address = server.address();
    const port = address && address.port;
    const allowedHosts = new Set(['127.0.0.1:' + port, 'localhost:' + port]);
    const host = String(req.headers.host || '').toLowerCase();
    if (!allowedHosts.has(host)) return json(res, 403, { ok: false, error: 'Invalid host' });
    if (req.method === 'POST') {
      const origin = req.headers.origin;
      if (origin && !allowedHosts.has(new URL(origin).host.toLowerCase())) {
        return json(res, 403, { ok: false, error: 'Cross-origin requests are disabled' });
      }
      if (req.headers['sec-fetch-site'] === 'cross-site') {
        return json(res, 403, { ok: false, error: 'Cross-site requests are disabled' });
      }
      if (!String(req.headers['content-type'] || '').startsWith('application/json')) {
        return json(res, 415, { ok: false, error: 'JSON content type required' });
      }
    }
    const pathname = new URL(req.url, 'http://127.0.0.1:' + port).pathname;
    try {
      if (req.method === 'GET' && pathname === '/api/status') {
        return json(res, 200, { status: 'running', engine: manager.ytdlp ? 'http+yt-dlp' : 'http',
          downloads: manager.downloadsDir, platform: process.platform, extensionConnected: false,
          autoCapture: false, appUpdateAvailable: false, latestAppVersion: '0.2.0-preview' });
      }
      if (req.method === 'GET' && pathname === '/api/tasks') {
        return json(res, 200, { tasks: manager.list(), pending: [] });
      }
      if (req.method === 'GET' && pathname === '/api/storage') {
        return json(res, 200, storage(manager.downloadsDir));
      }
      if (req.method === 'GET' && pathname === '/api/settings') {
        return json(res, 200, { autoCapture: false, platform: process.platform });
      }
      if (req.method === 'GET' && pathname === '/api/update/status') {
        return json(res, 200, { app: { currentVersion: '0.2.0-preview', latestVersion: '0.2.0-preview',
          updateAvailable: false }, installation: { status: 'idle' },
          ytdlp: { currentVersion: manager.ytdlp ? 'installed' : 'not installed', updateAvailable: false } });
      }
      if (req.method === 'POST' && pathname === '/api/download') {
        const body = await readJson(req);
        const task = manager.add(body);
        return json(res, 200, { ok: true, gid: task.gid });
      }
      if (req.method === 'POST' && pathname === '/api/action') {
        const body = await readJson(req);
        manager.action(body.gid, body.action);
        return json(res, 200, { ok: true });
      }
      if (req.method === 'POST' && pathname === '/api/openfolder') {
        await readJson(req);
        openInSystem(manager.downloadsDir);
        return json(res, 200, { ok: true });
      }
      if (req.method === 'POST' && pathname === '/api/openfile') {
        const body = await readJson(req);
        const task = manager.tasks.get(body.gid);
        if (!task || !fs.existsSync(task.filePath)) return json(res, 404, { ok: false, error: 'File not found' });
        openInSystem(task.filePath);
        return json(res, 200, { ok: true });
      }
      if (req.method === 'POST' && pathname === '/api/probe') {
        const body = await readJson(req);
        const target = validateUrl(body.url);
        // HEAD is best-effort only; do not download the file as a side effect.
        const response = await fetch(target.href, { method: 'HEAD', signal: AbortSignal.timeout(7000) });
        return json(res, 200, { ok: response.ok, filename: path.posix.basename(target.pathname),
          size: Number(response.headers.get('content-length')) || 0,
          contentType: response.headers.get('content-type') || '' });
      }
      if (pathname.startsWith('/api/')) {
        return json(res, 501, { ok: false, error: 'This Windows-only feature is not available in the cross-platform preview' });
      }
      if (req.method !== 'GET' && req.method !== 'HEAD') return json(res, 405, { ok: false, error: 'Method not allowed' });
      const name = pathname === '/' ? 'index.html' : decodeURIComponent(pathname).slice(1);
      const file = path.resolve(PUBLIC, name);
      if (file !== PUBLIC && !file.startsWith(PUBLIC + path.sep)) return json(res, 403, { ok: false, error: 'Invalid path' });
      let data;
      try { data = fs.readFileSync(file); } catch { return json(res, 404, { ok: false, error: 'Not found' }); }
      const ext = path.extname(file).toLowerCase();
      res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': 'no-store' });
      return res.end(req.method === 'HEAD' ? undefined : data);
    } catch (err) {
      return json(res, err instanceof SyntaxError ? 400 : 400, { ok: false, error: err.message });
    }
  });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(opts.port === undefined ? 6802 : opts.port, '127.0.0.1', resolve);
  });
  const url = 'http://127.0.0.1:' + server.address().port + '/';
  return { url, port: server.address().port, manager, server,
    async stop() { manager.stop(); await new Promise(resolve => server.close(resolve)); } };
}
if (require.main === module) {
  const noOpen = process.argv.includes('--no-open');
  startServer().then(app => {
    console.log('FastDownloader Pro cross-platform preview: ' + app.url);
    console.log('Downloads: ' + app.manager.downloadsDir);
    if (!noOpen) openInSystem(app.url);
    const close = () => app.stop().then(() => process.exit(0));
    process.once('SIGINT', close);
    process.once('SIGTERM', close);
  }).catch(err => { console.error(err); process.exitCode = 1; });
}
module.exports = { startServer, storage, readJson };
