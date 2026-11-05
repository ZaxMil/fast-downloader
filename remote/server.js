'use strict';
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const net = require('node:net');
const dns = require('node:dns').promises;
const crypto = require('node:crypto');
const { execFileSync, spawn } = require('node:child_process');
const { DownloadManager, validateUrl, safeFilename } = require('../cross-platform/engine');

const PUBLIC = path.join(__dirname, 'public');
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml' };
const mediaHosts = /(^|\.)(youtube\.com|youtu\.be|facebook\.com|fb\.watch|instagram\.com|tiktok\.com|vimeo\.com|twitter\.com|x\.com)$/i;
const blocked = new net.BlockList();
for (const [subnet, prefix] of [
  ['0.0.0.0',8],['10.0.0.0',8],['100.64.0.0',10],['127.0.0.0',8],
  ['169.254.0.0',16],['172.16.0.0',12],['192.0.0.0',24],['192.0.2.0',24],
  ['192.168.0.0',16],['198.18.0.0',15],['198.51.100.0',24],
  ['203.0.113.0',24],['224.0.0.0',4],['240.0.0.0',4]
]) blocked.addSubnet(subnet,prefix,'ipv4');
for (const [subnet,prefix] of [
  ['::',128],['::1',128],['fc00::',7],['fe80::',10],
  ['ff00::',8],['2001:db8::',32]
]) blocked.addSubnet(subnet,prefix,'ipv6');

async function assertPublicUrl(raw, resolver = dns.lookup) {
  const url = validateUrl(raw);
  if (!['http:', 'https:'].includes(url.protocol)) throw new Error('Unsupported URL');
  const host = url.hostname.replace(/^\[|\]$/g,'');
  if (!host || host === 'localhost' || host.endsWith('.localhost') || host.endsWith('.local')) throw new Error('Private destinations are blocked');
  const ipFamily = net.isIP(host);
  const addresses = ipFamily ? [{ address: host }] : await resolver(host, { all: true, verbatim: true });
  if (!addresses.length || addresses.some(x => {
    const family = net.isIP(x.address);
    if (!family) return true;
    // URLs canonicalize IPv4-mapped IPv6 addresses to ::ffff:hex:hex.
    if (family === 6 && new URL('http://[' + x.address + ']/').hostname.toLowerCase().startsWith('[::ffff:')) return true;
    return blocked.check(x.address, family === 4 ? 'ipv4' : 'ipv6');
  })) {
    throw new Error('Private or reserved network destinations are blocked');
  }
  return url;
}
function sendJson(res, code, data) {
  res.writeHead(code, { 'Content-Type':'application/json; charset=utf-8', 'Cache-Control':'no-store' });
  res.end(JSON.stringify(data));
}
async function readBody(req) {
  const chunks = []; let length=0;
  for await (const chunk of req) { length+=chunk.length; if(length>8192) throw new Error('Request too large'); chunks.push(chunk); }
  const body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
  if (!body || Array.isArray(body) || typeof body!=='object') throw new Error('Expected JSON object');
  return body;
}
function tokenMatch(actual, expected) {
  if (typeof actual !== 'string' || typeof expected !== 'string') return false;
  const a=Buffer.from(actual), b=Buffer.from(expected);
  return a.length === b.length && crypto.timingSafeEqual(a,b);
}
function remotes() {
  try {
    return execFileSync('rclone',['listremotes'],{timeout:6000,encoding:'utf8',stdio:['ignore','pipe','ignore']})
      .split(/\r?\n/).map(s=>s.trim().replace(/:$/,'')).filter(s=>/^[a-zA-Z0-9_.-]{1,64}$/.test(s));
  } catch { return []; }
}
function userStorage(dir) {
  try { const s=fs.statfsSync(dir); return {freeBytes:s.bavail*s.bsize,totalBytes:s.blocks*s.bsize}; }
  catch { return {freeBytes:0,totalBytes:0}; }
}
async function startRemoteServer(opts={}) {
  const token = opts.token || process.env.FASTDOWNLOADER_REMOTE_TOKEN;
  if (typeof token !== 'string' || token.length < 24) throw new Error('Set FASTDOWNLOADER_REMOTE_TOKEN (at least 24 random characters)');
  const home = path.join(os.homedir(), 'fastdownloader-remote');
  const downloadsDir=opts.downloadsDir || process.env.FASTDOWNLOADER_REMOTE_DOWNLOADS || path.join(home,'downloads');
  const dataDir=opts.dataDir || process.env.FASTDOWNLOADER_REMOTE_STATE || path.join(home,'state');
  const maxFileBytes=opts.maxFileBytes || 20*1024**3;
  const manager=opts.manager || new DownloadManager({ downloadsDir,dataDir,urlGuard:opts.guard || assertPublicUrl,maxFileBytes });
  const uploadJobs=new Map();
  const uploadProcesses=new Map();
  let authFailures=0;
  const server=http.createServer(async(req,res)=>{
    res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('X-Frame-Options','DENY');
    res.setHeader('Referrer-Policy','no-referrer');
    res.setHeader('Cache-Control','no-store');
    res.setHeader('Content-Security-Policy',"default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; base-uri 'none'; form-action 'self'; frame-ancestors 'none'");
    try {
      const host=String(req.headers.host||'');
      const origin=req.headers.origin;
      if (origin && new URL(origin).host !== host) return sendJson(res,403,{ok:false,error:'Cross-origin access blocked'});
      if (req.headers['sec-fetch-site']==='cross-site') return sendJson(res,403,{ok:false,error:'Cross-site access blocked'});
      const pathname=new URL(req.url,'http://127.0.0.1').pathname;
      if (pathname.startsWith('/api/')) {
        const supplied=String(req.headers.authorization||'').replace(/^Bearer\s+/i,'');
        if (!tokenMatch(supplied,token)) {
          authFailures++;
          return sendJson(res,401,{ok:false,error:'Authentication required'});
        }
        if (req.method==='POST' && !String(req.headers['content-type']||'').startsWith('application/json')) {
          return sendJson(res,415,{ok:false,error:'JSON content type required'});
        }
        if (pathname==='/api/status' && req.method==='GET') return sendJson(res,200,{ok:true,mode:'VPS',engine:manager.ytdlp?'HTTP + yt-dlp':'HTTP',downloadsDir:manager.downloadsDir,storage:userStorage(manager.downloadsDir),uploadDestinations:remotes().length});
        if (pathname==='/api/tasks' && req.method==='GET') return sendJson(res,200,{ok:true,tasks:manager.list(),uploads:[...uploadJobs.values()].slice(-100)});
        if (pathname==='/api/destinations' && req.method==='GET') return sendJson(res,200,{ok:true,destinations:remotes()});
        if (pathname==='/api/download' && req.method==='POST') {
          const body=await readBody(req);
          const url=await (opts.guard || assertPublicUrl)(body.url);
          if (body.type==='video' && !mediaHosts.test(url.hostname)) return sendJson(res,400,{ok:false,error:'Remote media mode supports known video platforms only'});
          const disk=userStorage(manager.downloadsDir);
          if (disk.freeBytes < 5*1024**3) return sendJson(res,507,{ok:false,error:'Server needs at least 5 GB free space'});
          if (manager.active.size >= 3) return sendJson(res,429,{ok:false,error:'Three active downloads allowed; finish or pause one first'});
          const task=manager.add({...body,url:url.href,startNow:true});
          return sendJson(res,201,{ok:true,gid:task.gid});
        }
        if (pathname==='/api/action' && req.method==='POST') {
          const body=await readBody(req);
          if (!['pause','resume','remove'].includes(body.action)) throw new Error('Unsupported action');
          manager.action(body.gid,body.action);
          return sendJson(res,200,{ok:true});
        }
        if (pathname==='/api/upload' && req.method==='POST') {
          const body=await readBody(req);
          const task=manager.tasks.get(body.gid);
          if (!task || task.status!=='complete') return sendJson(res,400,{ok:false,error:'Only completed downloads can be sent'});
          const destination=String(body.destination||'');
          if (!/^[a-zA-Z0-9_.-]{1,64}$/.test(destination) || !remotes().includes(destination)) return sendJson(res,400,{ok:false,error:'Cloud destination is not configured'});
          const base=fs.realpathSync(manager.downloadsDir);
          const actual=fs.realpathSync(task.filePath);
          if (!actual.startsWith(base+path.sep)) return sendJson(res,403,{ok:false,error:'File is outside downloads folder'});
          if (uploadProcesses.size >= 1) return sendJson(res,429,{ok:false,error:'One cloud transfer at a time'});
          const jobId=crypto.randomUUID();
          const target=destination+':FastDownloader/'+safeFilename(path.basename(actual));
          const job={id:jobId,filename:path.basename(actual),destination,status:'uploading',startedAt:new Date().toISOString()};
          uploadJobs.set(jobId,job);
          const child=spawn('rclone',['copyto',actual,target,'--stats','2s','--log-level','ERROR'],{stdio:['ignore','ignore','pipe']});
          uploadProcesses.set(jobId,child);
          let tail='';
          child.stderr.on('data',chunk=>{tail=(tail+chunk.toString('utf8')).slice(-700);});
          child.on('error',err=>{job.status='error';job.error=err.message;uploadProcesses.delete(jobId);});
          child.on('close',code=>{if(job.status==='uploading'){job.status=code===0?'complete':'error';if(code!==0)job.error=tail||'Transfer failed';}job.finishedAt=new Date().toISOString();uploadProcesses.delete(jobId);});
          return sendJson(res,202,{ok:true,jobId});
        }
        return sendJson(res,404,{ok:false,error:'Unknown API endpoint'});
      }
      if (!['GET','HEAD'].includes(req.method)) return sendJson(res,405,{ok:false,error:'Method not allowed'});
      const name=pathname==='/'?'index.html':decodeURIComponent(pathname.slice(1));
      const full=path.resolve(PUBLIC,name);
      if(!full.startsWith(PUBLIC+path.sep)) return sendJson(res,403,{ok:false,error:'Invalid file'});
      const data=fs.readFileSync(full);
      res.writeHead(200,{'Content-Type':MIME[path.extname(full)]||'application/octet-stream','Cache-Control':'no-store'});
      res.end(req.method==='HEAD'?undefined:data);
    } catch(err) {
      sendJson(res,err.code==='ENOENT'?404:400,{ok:false,error:err.code==='ENOENT'?'Not found':err.message});
    }
  });
  const port=opts.port===undefined?Number(process.env.FASTDOWNLOADER_REMOTE_PORT||6810):opts.port;
  // Never publicly bind this app. Publish only behind a separate authenticated HTTPS proxy.
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(port,'127.0.0.1',resolve);});
  return {manager,server,port:server.address().port,authFailures,
    stop:async()=>{for(const child of uploadProcesses.values())child.kill();manager.stop();await new Promise(resolve=>server.close(resolve));}};
}
if(require.main===module) startRemoteServer().then(app=>{
  console.log('FastDownloader Remote listening on 127.0.0.1:'+app.port+' (private only)');
  const exit=()=>app.stop().then(()=>process.exit(0));
  process.on('SIGINT',exit); process.on('SIGTERM',exit);
}).catch(err=>{console.error(err.message);process.exitCode=1;});
module.exports={startRemoteServer,assertPublicUrl,tokenMatch,remotes};
