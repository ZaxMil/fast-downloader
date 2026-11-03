'use strict';
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { once } = require('node:events');
const { spawn, spawnSync } = require('node:child_process');

const MEDIA_HOSTS = /(^|\.)((youtube\.com)|(youtu\.be)|(facebook\.com)|(fb\.watch)|(instagram\.com)|(tiktok\.com)|(vimeo\.com)|(twitter\.com)|(x\.com))$/i;
const STATUSES = new Set(['waiting', 'paused', 'downloading', 'complete', 'error']);

function safeFilename(input) {
  const cleaned = String(input || '').normalize('NFC')
    .replace(/[<>:"/\\|?*\x00-\x1f]/g, '_')
    .replace(/^\.+/, '_').replace(/[. ]+$/g, '').slice(0, 180);
  return cleaned && cleaned !== '_' ? cleaned : 'download';
}
function defaultDataDir() {
  if (process.platform === 'win32') return path.join(process.env.LOCALAPPDATA || os.homedir(), 'FastDownloader', 'CrossPlatform');
  if (process.platform === 'darwin') return path.join(os.homedir(), 'Library', 'Application Support', 'FastDownloader');
  return path.join(process.env.XDG_STATE_HOME || path.join(os.homedir(), '.local', 'state'), 'fastdownloader');
}
function defaultDownloadsDir() { return path.join(os.homedir(), 'Downloads', 'FastDownloader'); }
function resolveBinary(name) {
  const candidate = process.env[name === 'yt-dlp' ? 'FASTDOWNLOADER_YTDLP' : 'FASTDOWNLOADER_ARIA2'];
  if (candidate && fs.existsSync(candidate)) return candidate;
  const probe = spawnSync(process.platform === 'win32' ? 'where' : 'which', [name], { encoding: 'utf8', timeout: 2000 });
  return probe.status === 0 ? name : null;
}
function isMediaUrl(raw) {
  try { return MEDIA_HOSTS.test(new URL(raw).hostname); } catch { return false; }
}
function validateUrl(raw) {
  const parsed = new URL(String(raw || '').trim());
  if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password) {
    throw new Error('Only HTTP and HTTPS download links without embedded credentials are supported');
  }
  return parsed;
}
function suggestedName(url, gid) {
  let name;
  try { name = decodeURIComponent(path.posix.basename(url.pathname)); } catch { name = path.posix.basename(url.pathname); }
  return safeFilename(name && name !== '/' ? name : 'download-' + gid.slice(0, 8));
}
class DownloadManager {
  constructor(opts = {}) {
    this.dataDir = opts.dataDir || defaultDataDir();
    this.downloadsDir = opts.downloadsDir || defaultDownloadsDir();
    this.ytdlp = opts.ytdlp === undefined ? resolveBinary('yt-dlp') : opts.ytdlp;
    this.tasks = new Map();
    this.active = new Map();
    this.saveTimer = null;
    fs.mkdirSync(this.dataDir, { recursive: true });
    fs.mkdirSync(this.downloadsDir, { recursive: true });
    this.stateFile = path.join(this.dataDir, 'tasks.json');
    this.load();
  }
  load() {
    try {
      const saved = JSON.parse(fs.readFileSync(this.stateFile, 'utf8'));
      if (!Array.isArray(saved.tasks)) return;
      for (const task of saved.tasks) {
        if (!task || !task.gid || !task.url || !task.filePath) continue;
        if (!path.resolve(task.filePath).startsWith(path.resolve(this.downloadsDir) + path.sep)) continue;
        task.status = task.status === 'complete' ? 'complete' : 'paused';
        task.downloadSpeed = 0;
        task.eta = 0;
        this.tasks.set(task.gid, task);
      }
    } catch (err) {
      if (err.code !== 'ENOENT') process.stderr.write('Cannot load saved tasks: ' + err.message + '\n');
    }
  }
  save() {
    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = null;
    const tmp = this.stateFile + '.tmp';
    try {
      fs.writeFileSync(tmp, JSON.stringify({ version: 1, tasks: [...this.tasks.values()] }, null, 2), { mode: 0o600 });
      fs.renameSync(tmp, this.stateFile);
    } catch (err) { process.stderr.write('Cannot save tasks: ' + err.message + '\n'); }
  }
  scheduleSave() {
    if (!this.saveTimer) {
      this.saveTimer = setTimeout(() => this.save(), 900);
      this.saveTimer.unref();
    }
  }
  list() { return [...this.tasks.values()].sort((a, b) => b.dateAdded.localeCompare(a.dateAdded)); }
  add(data) {
    const url = validateUrl(data.url);
    const gid = crypto.randomUUID();
    const media = data.type === 'video' || isMediaUrl(url.href);
    const engine = media ? 'yt-dlp' : 'http';
    const initialName = media && !data.filename ? 'Media ' + gid.slice(0, 8) : suggestedName(url, gid);
    let name = safeFilename(data.filename || initialName);
    let filePath = path.join(this.downloadsDir, name);
    let suffix = 2;
    while (fs.existsSync(filePath) || [...this.tasks.values()].some(t => t.filePath === filePath)) {
      const ext = path.extname(name);
      filePath = path.join(this.downloadsDir, name.slice(0, name.length - ext.length) + ' (' + suffix++ + ')' + ext);
    }
    name = path.basename(filePath);
    const task = {
      gid, name, url: url.href, engine, status: 'paused', totalLength: 0,
      completedLength: 0, downloadSpeed: 0, connections: 0, progress: 0,
      eta: 0, filePath, dateAdded: new Date().toISOString(), dateFinished: null,
      quality: ['480','720','1080'].includes(String(data.quality)) ? String(data.quality) : 'best'
    };
    this.tasks.set(gid, task);
    this.save();
    if (data.startNow !== false) this.resume(gid);
    return task;
  }
  resume(gid) {
    const task = this.tasks.get(gid);
    if (!task) throw new Error('Download task not found');
    if (task.status === 'complete') return task;
    if (this.active.has(gid)) {
      // A previous paused process may still be closing. Restart once it exits.
      this.active.get(gid).restartAfterStop = true;
      task.status = 'paused';
      return task;
    }
    task.status = 'downloading';
    task.error = '';
    task.dateFinished = null;
    const controller = new AbortController();
    this.active.set(gid, { controller, child: null });
    this.scheduleSave();
    const work = task.engine === 'yt-dlp' ? this.runMedia(task, controller.signal) : this.runHttp(task, controller.signal);
    work.then(() => {
      if (this.tasks.has(gid) && task.status === 'downloading') {
        task.status = 'complete';
        task.progress = 100;
        task.dateFinished = new Date().toISOString();
      }
    }).catch(err => {
      if (this.tasks.has(gid) && task.status === 'downloading') {
        task.status = 'error';
        task.error = err.message;
      }
    }).finally(() => {
      const previous = this.active.get(gid);
      this.active.delete(gid);
      task.downloadSpeed = 0;
      task.eta = 0;
      this.save();
      if (previous && previous.restartAfterStop && this.tasks.has(gid)) this.resume(gid);
    });
    return task;
  }
  pause(gid) {
    const task = this.tasks.get(gid);
    if (!task) throw new Error('Download task not found');
    if (task.status === 'complete') return;
    task.status = 'paused';
    const job = this.active.get(gid);
    if (job) {
      job.restartAfterStop = false;
      job.controller.abort();
      if (job.child && !job.child.killed) job.child.kill();
    }
    this.scheduleSave();
  }
  action(gid, action) {
    if (action === 'pauseAll' || action === 'resumeAll') {
      for (const t of this.tasks.values()) {
        if (action === 'pauseAll' && t.status === 'downloading') this.pause(t.gid);
        if (action === 'resumeAll' && ['paused', 'error'].includes(t.status)) this.resume(t.gid);
      }
      return;
    }
    if (action === 'pause') return this.pause(gid);
    if (action === 'resume') return this.resume(gid);
    if (action === 'remove') {
      this.pause(gid);
      this.tasks.delete(gid);
      this.save();
      return;
    }
    throw new Error('Unsupported task action');
  }
  async runHttp(task, signal) {
    let offset = 0;
    try { offset = fs.statSync(task.filePath).size; } catch {}
    let url = task.url;
    let response;
    for (let hop = 0; hop < 6; hop++) {
      response = await fetch(url, {
        signal, redirect: 'manual',
        headers: offset ? { Range: 'bytes=' + offset + '-' } : {},
      });
      if (![301, 302, 303, 307, 308].includes(response.status)) break;
      const next = response.headers.get('location');
      if (!next) throw new Error('Redirect without Location header');
      url = validateUrl(new URL(next, url).href).href;
    }
    if ([301, 302, 303, 307, 308].includes(response.status)) throw new Error('Too many redirects');
    if (!response.ok) throw new Error('HTTP ' + response.status);
    let append = offset > 0 && response.status === 206;
    if (append) {
      const range = response.headers.get('content-range') || '';
      if (!range.startsWith('bytes ' + offset + '-')) throw new Error('Server returned an invalid resume range');
    }
    if (!append) offset = 0; // Range ignored: restart safely, never append duplicate bytes.
    const length = Number(response.headers.get('content-length')) || 0;
    task.totalLength = length ? length + offset : 0;
    task.completedLength = offset;
    task.connections = 1;
    const stream = fs.createWriteStream(task.filePath, { flags: append ? 'a' : 'w' });
    let streamError = null;
    stream.on('error', err => { streamError = err; });
    let sampleTime = Date.now(), sampleBytes = offset;
    try {
      for await (const chunk of response.body) {
        if (signal.aborted) throw new Error('Download paused');
        if (streamError) throw streamError;
        if (!stream.write(chunk)) await once(stream, 'drain');
        task.completedLength += chunk.length;
        const now = Date.now();
        if (now - sampleTime >= 400) {
          task.downloadSpeed = Math.max(0, Math.round((task.completedLength - sampleBytes) * 1000 / (now - sampleTime)));
          sampleTime = now;
          sampleBytes = task.completedLength;
          task.progress = task.totalLength ? Math.min(100, Math.round(task.completedLength * 100 / task.totalLength)) : 0;
          task.eta = task.downloadSpeed && task.totalLength ? Math.max(0, Math.ceil((task.totalLength - task.completedLength) / task.downloadSpeed)) : 0;
          this.scheduleSave();
        }
      }
      if (task.totalLength && task.completedLength !== task.totalLength) throw new Error('Incomplete download');
    } finally {
      await new Promise(resolve => stream.end(resolve));
    }
  }
  async runMedia(task, signal) {
    if (!this.ytdlp) throw new Error('yt-dlp is not installed. Install yt-dlp to download supported videos.');
    const output = task.name.startsWith('Media ') ? '%(title).180B [%(id)s].%(ext)s' : task.name;
    const format = task.quality === 'best' ? 'bv*+ba/b' :
      'bv*[height<=' + task.quality + ']+ba/b[height<=' + task.quality + ']/b';
    const args = ['--newline', '--no-playlist', '--continue', '--no-overwrites', '--no-color',
      '--format', format,
      '--paths', this.downloadsDir, '--output', output,
      '--progress-template', 'download:%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.speed)s|%(progress.eta)s',
      '--print', 'after_move:filepath', '--', task.url];
    const child = spawn(this.ytdlp, args, { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    const job = this.active.get(task.gid);
    if (job) job.child = child;
    if (signal.aborted) child.kill();
    let tail = '', last = '', outputBuffer = '';
    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', data => {
      outputBuffer += data;
      const lines = outputBuffer.split(/\r?\n/);
      outputBuffer = lines.pop().slice(-4000);
      for (const line of lines) {
        if (line.startsWith('download:')) {
          const [downloaded, total, speed, eta] = line.slice(9).split('|').map(Number);
          if (Number.isFinite(downloaded)) task.completedLength = downloaded;
          if (Number.isFinite(total)) task.totalLength = total;
          if (Number.isFinite(speed)) task.downloadSpeed = speed;
          if (Number.isFinite(eta)) task.eta = eta;
          task.progress = task.totalLength ? Math.round(100 * task.completedLength / task.totalLength) : 0;
          this.scheduleSave();
        } else if (path.isAbsolute(line.trim()) && line.trim().startsWith(this.downloadsDir + path.sep)) {
          last = line.trim();
        }
      }
    });
    child.stderr.on('data', data => { tail = (tail + data).slice(-1000); });
    const code = await new Promise((resolve, reject) => {
      child.on('error', reject);
      child.on('close', resolve);
    });
    if (signal.aborted) return;
    if (code !== 0) throw new Error(tail.trim() || 'yt-dlp exited with code ' + code);
    if (last && fs.existsSync(last)) {
      task.filePath = last;
      task.name = path.basename(last);
      task.completedLength = fs.statSync(last).size;
      task.totalLength = task.completedLength;
    }
  }
  stop() {
    for (const t of this.tasks.values()) if (t.status === 'downloading') this.pause(t.gid);
    this.save();
  }
}
module.exports = { DownloadManager, safeFilename, validateUrl, defaultDataDir, defaultDownloadsDir, resolveBinary };
