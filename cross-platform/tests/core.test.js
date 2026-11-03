'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const http = require('node:http');
const { once } = require('node:events');
const { DownloadManager, safeFilename, validateUrl } = require('../engine');
const { startServer } = require('../server');

const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(fn, timeout = 8000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { const result = fn(); if (result) return result; await wait(30); }
  throw new Error('Timed out waiting for expected state');
}
function makeFixture() {
  const data = Buffer.alloc(256 * 1024);
  for (let i = 0; i < data.length; i++) data[i] = i % 251;
  let rangeRequests = 0;
  const server = http.createServer((req, res) => {
    if (req.url !== '/test-file.bin') { res.writeHead(404); res.end(); return; }
    const match = /^bytes=(\d+)-$/.exec(req.headers.range || '');
    const start = match ? Number(match[1]) : 0;
    if (match) rangeRequests++;
    if (start >= data.length) { res.writeHead(416); res.end(); return; }
    const status = match ? 206 : 200;
    const headers = { 'Content-Type': 'application/octet-stream',
      'Content-Length': data.length - start, 'Accept-Ranges': 'bytes' };
    if (match) headers['Content-Range'] = 'bytes ' + start + '-' + (data.length - 1) + '/' + data.length;
    res.writeHead(status, headers);
    let offset = start;
    const timer = setInterval(() => {
      if (offset >= data.length || res.destroyed) { clearInterval(timer); if (!res.destroyed) res.end(); return; }
      const end = Math.min(offset + 8192, data.length);
      res.write(data.subarray(offset, end));
      offset = end;
    }, 12);
    res.on('close', () => clearInterval(timer));
  });
  return { server, data, get rangeRequests() { return rangeRequests; } };
}
async function setup() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'fd-cross-test-'));
  const manager = new DownloadManager({ dataDir: path.join(dir, 'state'), downloadsDir: path.join(dir, 'downloads'), ytdlp: null });
  const app = await startServer({ port: 0, manager });
  return { dir, manager, app, async close() { await app.stop(); fs.rmSync(dir, { recursive: true, force: true }); } };
}
async function post(app, route, body, headers = {}) {
  return fetch(app.url + 'api/' + route, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: JSON.stringify(body) });
}
test('filenames are safe and Unicode names survive', () => {
  assert.equal(safeFilename('فيديو عربي.mp4'), 'فيديو عربي.mp4');
  assert.equal(safeFilename('../../bad:name?.mp4'), '__.._bad_name_.mp4');
  assert.throws(() => validateUrl('file:///etc/passwd'));
  assert.throws(() => validateUrl('javascript:alert(1)'));
});
test('local API rejects cross-site POSTs and invalid hosts', async () => {
  const s = await setup();
  try {
    const bad = await post(s.app, 'download', { url: 'https://example.com/test.bin' }, { Origin: 'https://evil.example' });
    assert.equal(bad.status, 403);
    const wrongHost = await new Promise((resolve, reject) => {
      http.get(s.app.url + 'api/status', { headers: { Host: 'evil.example' } }, response => {
        response.resume(); resolve(response.statusCode);
      }).on('error', reject);
    });
    assert.equal(wrongHost, 403);
    const noContentType = await fetch(s.app.url + 'api/action', { method: 'POST', body: '{}' });
    assert.equal(noContentType.status, 415);
    const page = await fetch(s.app.url);
    assert.equal(page.status, 200);
    assert.match(await page.text(), /FastDownloader Pro/);
    assert.equal(s.manager.list().length, 0);
  } finally { await s.close(); }
});
test('HTTP downloads, progress, pause/resume and persisted history', async () => {
  const fixture = makeFixture();
  fixture.server.listen(0, '127.0.0.1');
  await once(fixture.server, 'listening');
  const s = await setup();
  const source = 'http://127.0.0.1:' + fixture.server.address().port + '/test-file.bin';
  try {
    const added = await post(s.app, 'download', { url: source, filename: 'ملف تجريبي.bin' });
    assert.equal(added.status, 200);
    const { gid } = await added.json();
    await until(() => s.manager.tasks.get(gid).completedLength > 0);
    const pause = await post(s.app, 'action', { gid, action: 'pause' });
    assert.equal(pause.status, 200);
    await until(() => !s.manager.active.has(gid));
    assert.equal(s.manager.tasks.get(gid).status, 'paused');
    const bytesBefore = fs.statSync(s.manager.tasks.get(gid).filePath).size;
    assert.ok(bytesBefore > 0 && bytesBefore < fixture.data.length);
    const resume = await post(s.app, 'action', { gid, action: 'resume' });
    assert.equal(resume.status, 200);
    await until(() => s.manager.tasks.get(gid).status === 'complete');
    const task = s.manager.tasks.get(gid);
    assert.equal(task.name, 'ملف تجريبي.bin');
    assert.deepEqual(fs.readFileSync(task.filePath), fixture.data);
    assert.ok(fixture.rangeRequests >= 1, 'Download must resume with Range');
    assert.equal(task.progress, 100);
    const tasksResponse = await fetch(s.app.url + 'api/tasks');
    assert.equal((await tasksResponse.json()).tasks[0].status, 'complete');
    const restored = new DownloadManager({ dataDir: s.manager.dataDir, downloadsDir: s.manager.downloadsDir, ytdlp: null });
    assert.equal(restored.list()[0].status, 'complete');
    await post(s.app, 'action', { gid, action: 'remove' });
    assert.equal(s.manager.list().length, 0);
    assert.ok(fs.existsSync(task.filePath), 'Removing a task must never delete its downloaded file');
  } finally {
    await s.close();
    await new Promise(resolve => fixture.server.close(resolve));
  }
});
test('video without yt-dlp fails visibly rather than pretending success', async () => {
  const s = await setup();
  try {
    const response = await post(s.app, 'download', { url: 'https://www.youtube.com/watch?v=example', type: 'video' });
    const { gid } = await response.json();
    const task = await until(() => {
      const item = s.manager.tasks.get(gid);
      return item && item.status === 'error' && item;
    });
    assert.match(task.error, /yt-dlp is not installed/);
  } finally { await s.close(); }
});

test('immediate resume while a paused request is closing does not strand the task', async () => {
  const fixture = makeFixture();
  fixture.server.listen(0, '127.0.0.1');
  await once(fixture.server, 'listening');
  const s = await setup();
  const source = 'http://127.0.0.1:' + fixture.server.address().port + '/test-file.bin';
  try {
    const response = await post(s.app, 'download', { url: source });
    const { gid } = await response.json();
    await until(() => s.manager.tasks.get(gid).completedLength > 0);
    await post(s.app, 'action', { gid, action: 'pause' });
    await post(s.app, 'action', { gid, action: 'resume' });
    await until(() => s.manager.tasks.get(gid).status === 'complete');
    assert.deepEqual(fs.readFileSync(s.manager.tasks.get(gid).filePath), fixture.data);
  } finally {
    await s.close();
    await new Promise(resolve => fixture.server.close(resolve));
  }
});
test('a server ignoring Range triggers a clean restart rather than corrupt append', async () => {
  const payload = Buffer.alloc(192 * 1024, 0x7e);
  const fixture = http.createServer((req, res) => {
    res.writeHead(200, { 'Content-Length': payload.length });
    let pos = 0;
    const timer = setInterval(() => {
      if (res.destroyed || pos >= payload.length) { clearInterval(timer); if (!res.destroyed) res.end(); return; }
      const next = Math.min(pos + 4096, payload.length);
      res.write(payload.subarray(pos, next)); pos = next;
    }, 10);
    res.on('close', () => clearInterval(timer));
  });
  fixture.listen(0, '127.0.0.1');
  await once(fixture, 'listening');
  const s = await setup();
  try {
    const { gid } = await (await post(s.app, 'download', { url: 'http://127.0.0.1:' + fixture.address().port + '/file.bin' })).json();
    await until(() => s.manager.tasks.get(gid).completedLength > 0);
    await post(s.app, 'action', { gid, action: 'pause' });
    await until(() => !s.manager.active.has(gid));
    await post(s.app, 'action', { gid, action: 'resume' });
    await until(() => s.manager.tasks.get(gid).status === 'complete');
    assert.deepEqual(fs.readFileSync(s.manager.tasks.get(gid).filePath), payload);
  } finally {
    await s.close();
    await new Promise(resolve => fixture.close(resolve));
  }
});
