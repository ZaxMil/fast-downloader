"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");

const html = fs.readFileSync(path.join(__dirname, "..", "ui", "index.html"), "utf8");
const part = html.match(/    function formatDiskSize\(bytes\) \{[\s\S]*?    async function refreshDiskSpace\(\) \{[\s\S]*?\n    \}\n/);
assert.ok(part, "Expected storage widget implementation exists");

function createUi() {
  const nodes = {};
  const create = () => ({
    textContent: "",
    title: "",
    style: { width: "", background: "" },
    attributes: {},
    setAttribute(name, value) { this.attributes[name] = value; }
  });
  const doc = {
    getElementById(id) { return nodes[id] || (nodes[id] = create()); },
    querySelector(sel) { return nodes[sel] || (nodes[sel] = create()); }
  };
  const context = vm.createContext({
    document: doc, currentLang: "en", Number, Math, console,
    fetch: async () => { throw new Error("unconfigured"); }
  });
  vm.runInContext(part[0], context);
  return { context, nodes };
}

(async () => {
  const ui = createUi();
  vm.runInContext("renderDiskSpace({ok: true, drive: 'D:\\\\', downloadsPath: 'D:\\\\Downloads', totalBytes: 500*1024**3, freeBytes: 25*1024**3})", ui.context);
  assert.equal(ui.nodes.diskFreeText.textContent, "Free: 25.0 GB");
  assert.equal(ui.nodes.diskDriveText.textContent, " · D:");
  assert.equal(ui.nodes.diskProgressFill.style.width, "95.0%", "The bar reflects used capacity, not a fixed 45%");
  assert.equal(ui.nodes.diskProgressBar.attributes["aria-valuenow"], "95");
  assert.match(ui.nodes[".disk-space-card"].title, /D:\\Downloads/);
  assert.match(ui.nodes.diskProgressFill.style.background, /ef4444/, "Very full drives should show a warning");

  vm.runInContext("renderDiskSpace({ok: true, drive: 'C:\\\\', downloadsPath: 'C:\\\\downloads', totalBytes: 200*1024**3, freeBytes: 180*1024**3})", ui.context);
  assert.equal(ui.nodes.diskFreeText.textContent, "Free: 180.0 GB");
  assert.equal(ui.nodes.diskDriveText.textContent, " · C:");
  assert.equal(ui.nodes.diskProgressFill.style.width, "10.0%");
  assert.equal(ui.nodes.diskProgressFill.style.background, "", "Clear old warning when the drive changes");

  vm.runInContext("renderDiskSpace({ok: false})", ui.context);
  assert.equal(ui.nodes.diskFreeText.textContent, "Free: unavailable", "Do not fabricate remaining space");
  assert.equal(ui.nodes.diskProgressFill.style.width, "0%");

  vm.runInContext("currentLang = 'ar'; renderDiskSpace({ok: true, drive: 'D:\\\\', totalBytes: 100*1024**3, freeBytes: 7.5*1024**3})", ui.context);
  assert.equal(ui.nodes.diskFreeText.textContent, "المتاح: 7.5 GB");

  vm.runInContext("renderDiskSpace({ok: true, totalBytes: 0, freeBytes: 0})", ui.context);
  assert.equal(ui.nodes.diskFreeText.textContent, "المتاح: غير معروف");

  console.log("PASS: 5 disk widget cases: real values, drive, usage, warning, unavailable and Arabic");
})().catch(error => { console.error(error); process.exitCode = 1; });
