'use strict';
const { test }=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const http=require('node:http');
const {spawnSync}=require('node:child_process');
const { startRemoteServer, assertPublicUrl, tokenMatch }=require('../server');

const resolver=(host)=>Promise.resolve([{address:host==='safe.example'?'93.184.215.14':'192.168.1.3'}]);
test('rejects localhost, reserved IP and private DNS answers',async()=>{
  for(const u of ['http://localhost/','http://127.0.0.1/','http://10.1.2.3/','http://192.168.1.2/','http://[::1]/','http://bad.example/'])
    await assert.rejects(assertPublicUrl(u,resolver));
  assert.equal((await assertPublicUrl('https://safe.example/files/a.zip',resolver)).hostname,'safe.example');
});
test('constant-time token validation does not accept arbitrary keys',()=>{
  assert.equal(tokenMatch('correct-test-key-which-is-long','correct-test-key-which-is-long'),true);
  assert.equal(tokenMatch('wrong','correct-test-key-which-is-long'),false);
});
test('private HTTP API requires auth, creates local test download and blocks traversal',async t=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'fd-remote-test-'));

  const content=Buffer.from('FDP REMOTE TEST ' .repeat(1500));
  const fixture=http.createServer((req,res)=>{
    res.writeHead(200,{'content-type':'application/octet-stream','content-length':content.length});res.end(content);
  });
  await new Promise(r=>fixture.listen(0,'127.0.0.1',r));

  const token='test-vps-token-is-long-enough-12345678';
  const app=await startRemoteServer({port:0,token,downloadsDir:path.join(root,'downloads'),dataDir:path.join(root,'state'),guard:async url=>new URL(url)});
  t.after(async()=>{await app.stop();await new Promise(r=>fixture.close(r));fs.rmSync(root,{recursive:true,force:true});});
  const origin='http://127.0.0.1:'+app.port;
  const req=async(route,body,secret=token)=>{
    const response=await fetch(origin+route,{method:body===undefined?'GET':'POST',headers:{'Authorization':'Bearer '+secret,...(body===undefined?{}:{'Content-Type':'application/json'})},body:body===undefined?undefined:JSON.stringify(body)});
    return {status:response.status,data:await response.json()};
  };
  assert.equal((await req('/api/tasks',undefined,'wrong')).status,401);
  assert.equal((await req('/api/status')).data.mode,'VPS');
  assert.equal((await req('/api/destinations')).status,200);
  assert.equal((await req('/api/upload',{gid:'missing',destination:'nope'})).status,400);
  const url='http://127.0.0.1:'+fixture.address().port+'/file.zip';
  const added=await req('/api/download',{url,filename:'remote-test.bin',type:'file'});
  assert.equal(added.status,201);
  const gid=added.data.gid;
  for(let i=0;i<90&&app.manager.tasks.get(gid).status==='downloading';i++)await new Promise(r=>setTimeout(r,25));
  assert.equal(app.manager.tasks.get(gid).status,'complete');
  assert.deepEqual(fs.readFileSync(path.join(root,'downloads','remote-test.bin')),content);
  if (spawnSync('rclone',['version'],{stdio:'ignore'}).status===0) {
    const config=path.join(root,'rclone.conf');
    const target=path.join(root,'cloud-simulation');
    fs.mkdirSync(target);
    fs.writeFileSync(config,'[localtest]\ntype = alias\nremote = '+target+'\n');
    const oldConfig=process.env.RCLONE_CONFIG;
    process.env.RCLONE_CONFIG=config;
    try {
      const sent=await req('/api/upload',{gid,destination:'localtest'});
      assert.equal(sent.status,202);
      const id=sent.data.jobId;
      let job;
      for(let x=0;x<100;x++){
        job=(await req('/api/tasks')).data.uploads.find(u=>u.id===id);
        if(job&&job.status!=='uploading')break;
        await new Promise(r=>setTimeout(r,50));
      }
      assert.equal(job.status,'complete',job.error||'rclone failed');
      assert.deepEqual(fs.readFileSync(path.join(target,'FastDownloader','remote-test.bin')),content);
    } finally {if(oldConfig===undefined)delete process.env.RCLONE_CONFIG;else process.env.RCLONE_CONFIG=oldConfig;}
  }
  assert.equal((await req('/api/action',{gid,action:'remove'})).status,200);
  assert.equal(fs.existsSync(path.join(root,'downloads','remote-test.bin')),true,'removing task must not delete completed files');
  const bad=await fetch(origin+'/api/tasks',{headers:{'Authorization':'Bearer '+token,'Origin':'https://evil.example'}});
  assert.equal(bad.status,403);
  const staticBad=await fetch(origin+'/../../etc/passwd');
  assert.notEqual(staticBad.status,200);
});
