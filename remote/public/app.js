'use strict';
const $ = id => document.getElementById(id);
let accessKey = sessionStorage.getItem('fd_remote_key') || '';
let destinations = [];
let checking = false;
let polling;
function notify(message) { $('toast').textContent = message; $('toast').classList.add('visible'); setTimeout(()=>$('toast').classList.remove('visible'),4200); }
function size(num) { if (!Number.isFinite(+num)||+num<0) return '—'; let n=+num,i=0;const units=['B','KB','MB','GB','TB'];while(n>=1024&&i<4){n/=1024;i++;}return n.toFixed(i&&n<10?1:0)+' '+units[i]; }
async function api(route, body) {
  const result=await fetch('/api/'+route,{method:body===undefined?'GET':'POST',headers:{'Authorization':'Bearer '+accessKey,...(body!==undefined?{'Content-Type':'application/json'}:{})},body:body===undefined?undefined:JSON.stringify(body),cache:'no-store'});
  const data=await result.json().catch(()=>({}));
  if(!result.ok||data.ok===false){ if(result.status===401)disconnect();throw new Error(data.error||'Request failed'); }
  return data;
}
function disconnect() {accessKey='';sessionStorage.removeItem('fd_remote_key');$('loginPanel').hidden=false;$('dashboard').hidden=true;$('connection').textContent='Disconnected';clearInterval(polling);}
function button(label,action){const el=document.createElement('button');el.textContent=label;el.addEventListener('click',action);return el;}
function taskNode(t){
  const el=document.createElement('div');el.className='task';
  const top=document.createElement('div');top.className='tasktop';
  const info=document.createElement('div');info.style.minWidth='0';
  const name=document.createElement('div');name.className='taskname';name.textContent=t.name;
  const url=document.createElement('div');url.className='taskurl';url.textContent=t.url;
  info.append(name,url);
  const state=document.createElement('span');state.className='taskstate '+t.status;state.textContent=t.status;
  top.append(info,state);
  const bar=document.createElement('div');bar.className='track';const fill=document.createElement('div');fill.className='fill';fill.style.width=Math.min(100,Math.max(0,+t.progress||0))+'%';bar.append(fill);
  const bottom=document.createElement('div');bottom.className='taskbottom';
  const meta=document.createElement('span');meta.textContent=size(t.completedLength)+(t.totalLength?' / '+size(t.totalLength):'')+' · '+size(t.downloadSpeed)+'/s';
  const actions=document.createElement('div');actions.className='actions';
  if(t.status==='downloading') actions.append(button('Pause',()=>operation(t.gid,'pause')));
  if(['paused','error'].includes(t.status)) actions.append(button('Resume',()=>operation(t.gid,'resume')));
  if(t.status==='complete'&&destinations.length){
    const choose=document.createElement('select');const placeholder=document.createElement('option');placeholder.value='';placeholder.textContent='Send to cloud…';choose.append(placeholder);
    for(const remote of destinations){const opt=document.createElement('option');opt.value=remote;opt.textContent=remote;choose.append(opt);}
    choose.addEventListener('change',async()=>{if(!choose.value)return;try{await api('upload',{gid:t.gid,destination:choose.value});notify('Server-to-cloud transfer started');await refresh();}catch(err){notify(err.message);}finally{choose.value='';}});
    actions.append(choose);
  }
  actions.append(button('Remove from list',async()=>{if(!confirm('Remove task? Files remain on server.'))return;await operation(t.gid,'remove');}));
  bottom.append(meta,actions);el.append(top,bar,bottom);
  if(t.error){const error=document.createElement('div');error.className='error';error.textContent=t.error;el.append(error);}return el;
}
async function operation(gid,action){try{await api('action',{gid,action});await refresh();}catch(err){notify(err.message);}}
async function refresh(){if(!accessKey||checking)return;checking=true;try{
  const [status,items,rem]=await Promise.all([api('status'),api('tasks'),api('destinations')]);
  destinations=rem.destinations||[];
  $('connection').textContent='VPS connected';$('freeDisk').textContent=size(status.storage.freeBytes);
  $('destinationCount').textContent=destinations.length;
  $('activeCount').textContent=(items.tasks||[]).filter(t=>t.status==='downloading').length;
  $('totalSpeed').textContent=size((items.tasks||[]).reduce((a,t)=>a+(+t.downloadSpeed||0),0))+'/s';
  const list=$('taskList');list.replaceChildren();if(!items.tasks.length){const empty=document.createElement('div');empty.className='empty';empty.textContent='No downloads yet.';list.append(empty);}else for(const t of items.tasks)list.append(taskNode(t));
  const uploads=$('uploadList');uploads.replaceChildren();const jobs=(items.uploads||[]).slice(-15).reverse();if(!jobs.length){const empty=document.createElement('p');empty.className='help';empty.textContent=destinations.length?'Select a destination on a completed download.':'No cloud destinations configured yet (rclone listremotes is empty).';uploads.append(empty);}else for(const job of jobs){const line=document.createElement('div');line.className='upload';line.textContent=job.filename+' → '+job.destination+' · '+job.status+(job.error?' · '+job.error:'');uploads.append(line);}
}catch(err){if(accessKey)notify(err.message);}finally{checking=false;}}
$('loginForm').addEventListener('submit',async event=>{event.preventDefault();accessKey=$('accessKey').value.trim();try{await api('status');sessionStorage.setItem('fd_remote_key',accessKey);$('accessKey').value='';$('loginPanel').hidden=true;$('dashboard').hidden=false;await refresh();clearInterval(polling);polling=setInterval(refresh,2500);}catch(err){notify(err.message);}});
$('downloadForm').addEventListener('submit',async event=>{event.preventDefault();$('add').disabled=true;try{await api('download',{url:$('url').value.trim(),filename:$('filename').value.trim(),type:$('type').value,quality:$('quality').value});$('url').value='';$('filename').value='';notify('VPS download started');await refresh();}catch(err){notify(err.message);}finally{$('add').disabled=false;}});
$('logout').addEventListener('click',disconnect);
$('refresh').addEventListener('click',refresh);
if(accessKey){api('status').then(()=>{$('loginPanel').hidden=true;$('dashboard').hidden=false;refresh();polling=setInterval(refresh,2500);}).catch(()=>disconnect());}
