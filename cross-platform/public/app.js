'use strict';
const $ = id => document.getElementById(id);
let toastTimer;
function notify(message) {
  $('toast').textContent = message;
  $('toast').classList.add('visible');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => $('toast').classList.remove('visible'), 4500);
}
function bytes(n) {
  if (!Number.isFinite(Number(n)) || Number(n) < 0) return '—';
  let value = Number(n), unit = 0;
  const units = ['B','KB','MB','GB','TB'];
  while (value >= 1024 && unit < units.length - 1) { value /= 1024; unit++; }
  return value.toFixed(unit && value < 10 ? 1 : 0) + ' ' + units[unit];
}
async function api(endpoint, opts) {
  const res = await fetch('/api/' + endpoint, opts);
  let data = {};
  try { data = await res.json(); } catch {}
  if (!res.ok || data.ok === false) throw new Error(data.error || 'Request failed (' + res.status + ')');
  return data;
}
function post(endpoint, body) {
  return api(endpoint, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
}
function makeButton(label, action, gid, danger) {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = label;
  if (danger) button.className = 'danger';
  button.addEventListener('click', async () => {
    try {
      if (action === 'remove' && !confirm('Remove this task from the list? Downloaded files will be kept.')) return;
      if (action === 'open') await post('openfile', { gid });
      else await post('action', { action, gid });
      await refresh();
    } catch (err) { notify(err.message); }
  });
  return button;
}
function renderTask(task) {
  const row = document.createElement('div');
  row.className = 'task';
  const top = document.createElement('div'); top.className = 'task-top';
  const info = document.createElement('div'); info.style.minWidth = '0';
  const name = document.createElement('div'); name.className = 'task-name'; name.textContent = task.name;
  const url = document.createElement('div'); url.className = 'task-url'; url.textContent = task.url;
  info.append(name, url);
  const state = document.createElement('div'); state.className = 'task-state ' + task.status;
  state.textContent = task.status;
  top.append(info, state);
  const track = document.createElement('div'); track.className = 'progress-track';
  const fill = document.createElement('div'); fill.className = 'progress-fill';
  fill.style.width = Math.min(100, Math.max(0, Number(task.progress) || 0)) + '%';
  track.append(fill);
  const bottom = document.createElement('div'); bottom.className = 'task-bottom';
  const meta = document.createElement('span'); meta.className = 'task-meta';
  const total = task.totalLength ? ' / ' + bytes(task.totalLength) : '';
  meta.textContent = bytes(task.completedLength) + total + ' · ' + bytes(task.downloadSpeed) + '/s · ' + task.engine;
  const actions = document.createElement('div'); actions.className = 'task-actions';
  if (task.status === 'downloading') actions.append(makeButton('Pause', 'pause', task.gid));
  if (['paused','error','waiting'].includes(task.status)) actions.append(makeButton('Resume', 'resume', task.gid));
  if (task.status === 'complete') actions.append(makeButton('Open', 'open', task.gid));
  actions.append(makeButton('Remove', 'remove', task.gid, true));
  bottom.append(meta, actions);
  row.append(top, track, bottom);
  if (task.error) {
    const error = document.createElement('div'); error.className = 'task-error'; error.textContent = task.error; row.append(error);
  }
  return row;
}
async function refresh() {
  try {
    const [result, status, disk] = await Promise.all([api('tasks'), api('status'), api('storage')]);
    const tasks = result.tasks || [];
    const list = $('taskList');
    list.replaceChildren();
    if (!tasks.length) {
      const empty = document.createElement('div'); empty.className = 'empty';
      empty.textContent = 'No downloads yet. Add a link to get started.';
      list.append(empty);
    } else for (const task of tasks) list.append(renderTask(task));
    $('taskCount').textContent = tasks.length;
    $('activeCount').textContent = tasks.filter(t => t.status === 'downloading').length;
    $('completeCount').textContent = tasks.filter(t => t.status === 'complete').length;
    $('totalSpeed').textContent = bytes(tasks.reduce((sum, t) => sum + (Number(t.downloadSpeed) || 0), 0)) + '/s';
    $('freeSpace').textContent = disk.ok ? bytes(disk.freeBytes) : '—';
    $('engineInfo').textContent = status.engine.includes('yt-dlp') ? 'Direct files + yt-dlp videos available' : 'Direct files available · install yt-dlp for video links';
  } catch (err) { notify('Cannot connect to local download engine: ' + err.message); }
}
$('downloadForm').addEventListener('submit', async event => {
  event.preventDefault();
  const button = $('addButton'); button.disabled = true;
  try {
    const result = await post('download', { url: $('url').value.trim(), filename: $('filename').value.trim(),
      type: $('type').value, quality: $('quality').value, startNow: true });
    if (result.ok) { $('url').value = ''; $('filename').value = ''; notify('Download added'); await refresh(); }
  } catch (err) { notify(err.message); }
  finally { button.disabled = false; }
});
$('refresh').addEventListener('click', refresh);
$('openFolder').addEventListener('click', async () => {
  try { await post('openfolder', {}); } catch (err) { notify(err.message); }
});
refresh();
setInterval(refresh, 1200);
