"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const html = fs.readFileSync(path.join(__dirname, "..", "ui", "index.html"), "utf8").replace(/\r\n/g, "\n");
const backend = fs.readFileSync(path.join(__dirname, "..", "src", "Program.cs"), "utf8");
function section(start,end) {
  const a=html.indexOf(start), b=html.indexOf(end,a);
  assert.ok(a>=0&&b>a,"UI functions present: "+start);
  return html.slice(a,b);
}
function mockTable() {
  const widths={name:280,queue:110,size:85,progress:145,speed:145,eta:115,status:100,dateAdded:130,dateFinished:130,actions:110};
  const mins={name:220,queue:75,size:65,progress:100,speed:90,eta:75,status:75,dateAdded:110,dateFinished:110,actions:85};
  const flags=new Set(), values=new Map();
  const headers=Object.keys(widths).map(col=>({dataset:{col,minWidth:String(mins[col])},style:{width:widths[col]+"px"},getAttribute:key=>key==="data-col"?col:null}));
  const table={style:{},classList:{toggle(name,on){if(on)flags.add(name);else flags.delete(name)}},querySelectorAll:selector=>selector==="thead th[data-col]"?headers:[]};
  const container={clientWidth:950};
  const document={getElementById:id=>id==="downloadsTable"?table:null,querySelector:selector=>selector===".table-container"?container:null};
  const localStorage={getItem:k=>values.get(k)||null,setItem:(k,v)=>values.set(k,String(v))};
  return {document,table,container,flags,headers,localStorage};
}
{
  const env=mockTable();
  const ctx=vm.createContext({...env,renderColumnCustomizerList(){}});
  vm.runInContext(section("    const DEFAULT_COLUMN_ORDER =", "    function getCustomColumnLabels() {"),ctx);
  const vis=ctx.getColumnVisibility();
  assert.equal(vis.name,true);
  assert.equal(vis.dateAdded,false);
  assert.equal(vis.dateFinished,false);
  ctx.applyColumnVisibility();
  assert.ok(env.flags.has("hide-col-dateAdded"));
  assert.ok(env.flags.has("hide-col-dateFinished"));
  assert.equal(env.table.style.width,"950px");
  ctx.toggleColumnVisibility("dateAdded",true);
  assert.ok(!env.flags.has("hide-col-dateAdded"));
  assert.ok(env.flags.has("hide-col-dateFinished"));
  assert.ok(parseInt(env.table.style.width)>=1000);
  ctx.toggleColumnVisibility("actions",false);
  assert.ok(env.flags.has("hide-col-actions"));
  ctx.toggleColumnVisibility("name",false);
  assert.ok(!env.flags.has("hide-col-name"));
  assert.match(html,/hide-col-dateAdded th\[data-col="dateAdded"\]/);
  assert.match(html,/hide-col-dateFinished td\[data-col="dateFinished"\]/);
  assert.match(html,/data-min-width="220" style="width:280px/);
}
{
  const elements=new Map();
  const document={getElementById:id=>{if(!elements.has(id))elements.set(id,{textContent:""});return elements.get(id)}};
  const ctx=vm.createContext({tasks:[],taskQueueMap:{},Map,Set,Number,Math,Array,sessionBytes:0,observedTaskBytes:new Map(),speedHistory:new Array(60).fill(0),totalSpeed:0,isGraphVisible:false,document,formatBytes:n=>n+" B",getFileCategory:()=>"other",renderTable(){},drawSpeedGraph(){}});
  vm.runInContext(section("    function processIncomingTasks(newTasks) {", "    function formatDateDisplay("),ctx);
  const task={gid:"A",name:"Arabic.mp4",completedLength:1000,downloadSpeed:150,status:"active"};
  ctx.processIncomingTasks([task]);
  assert.equal(ctx.sessionBytes,0);
  ctx.processIncomingTasks([{...task,completedLength:1900}]);
  assert.equal(ctx.sessionBytes,900);
  ctx.processIncomingTasks([{...task,completedLength:1900,downloadSpeed:900}]);
  assert.equal(ctx.sessionBytes,900);
  assert.equal(elements.get("bottomSessionBytes").textContent,"900 B");
}
assert.ok(backend.includes('item["dateAdded"] = dates.DateAdded;'));
assert.ok(backend.includes('item["dateFinished"] = ytDates.DateFinished;'));
assert.ok(backend.includes('item["connections"] = liveConnections;'));
assert.ok(!html.includes("Free: 120 GB"));
assert.ok(!html.includes("formatBytes(maxVal)"));
console.log("PASS: live table columns, resizing safeguards, date fields, and transferred-byte totals");
