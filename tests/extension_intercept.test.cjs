"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");

const SOURCE = fs.readFileSync(path.join(__dirname, "..", "extension", "background.js"), "utf8");

async function scenario({ native = { ok: true, pending: { pendingId: "pen_123" } }, http = null, autoIntercept = true, id = 42, shouldIntercept = true } = {}) {
  const events = { suggested: 0, canceled: 0, erased: 0, searches: 0, posted: 0, native: 0 };
  const handlers = {};
  const event = name => ({ addListener: fn => { handlers[name] = fn; } });
  const chrome = {
    cookies: { getAll: async () => [] },
    contextMenus: { removeAll: cb => cb(), create: () => {}, onClicked: event("contextMenu") },
    runtime: {
      lastError: null,
      sendNativeMessage: (_host, _payload, cb) => { events.native++; cb(native); },
      getManifest: () => ({ version: "0.1.7" }),
      onInstalled: event("installed"),
      onStartup: event("startup"),
      onMessage: event("message"),
      reload: () => {}
    },
    storage: {
      local: {
        get: (_options, callback) => {
          const data = { autoIntercept };
          if (callback) callback(data);
          else return Promise.resolve(data);
        },
        set: (_options, callback) => { if (callback) callback(); }
      },
      onChanged: event("storageChanged")
    },
    tabs: { sendMessage: () => Promise.resolve() },
    downloads: {
      onCreated: event("onCreated"),
      onDeterminingFilename: event("onDeterminingFilename"),
      search: async () => { events.searches++; return [{ state: "in_progress", id }]; },
      cancel: (_id, cb) => { events.canceled++; cb(); },
      erase: (_query, cb) => { events.erased++; cb(); }
    }
  };
  const fetch = async (url) => {
    if (String(url).includes("/api/extension/ping")) return { ok: true, json: async () => ({}) };
    events.posted++;
    if (http instanceof Error) throw http;
    return http || { ok: false, json: async () => ({ ok: false, error: "not running" }) };
  };
  const context = vm.createContext({
    chrome, fetch, URL, navigator: { userAgent: "Test Browser", languages: ["en-US"] },
    setTimeout: () => 0, setInterval: () => 0, console: { log() {}, warn() {}, error() {} }
  });
  vm.runInContext(SOURCE, context, { filename: "background.js" });

  const item = {
    id, filename: shouldIntercept ? "archive.zip" : "notes.txt",
    url: shouldIntercept ? "https://example.com/archive.zip" : "https://example.com/notes.txt",
    referrer: "https://example.com/", mime: "application/zip"
  };
  handlers.onDeterminingFilename(item, () => { events.suggested++; });
  // A second event can arrive before the asynchronous first interception completes.
  handlers.onCreated(item);
  for (let i = 0; i < 20; i++) await new Promise(resolve => setImmediate(resolve));
  return events;
}

(async () => {
  let events = await scenario({ shouldIntercept: false });
  assert.equal(events.suggested, 1, "Always unblock Chrome filename determination");
  assert.equal(events.canceled, 0, "Never cancel an unrelated native browser download");
  assert.equal(events.posted, 0);

  events = await scenario();
  assert.equal(events.suggested, 1);
  assert.equal(events.canceled, 1, "Cancel only after native pending acknowledgement");
  assert.equal(events.erased, 1);
  assert.equal(events.native, 1, "Do not double-dispatch the same browser download");
  assert.equal(events.posted, 0);

  events = await scenario({ native: null });
  assert.equal(events.suggested, 1);
  assert.equal(events.canceled, 0, "No acknowledgement means browser remains owner");
  assert.equal(events.posted, 1);

  events = await scenario({
    native: { ok: false, error: "native failed" },
    http: { ok: true, json: async () => ({ ok: true, pending: { pendingId: "pen_http" } }) }
  });
  assert.equal(events.canceled, 1, "Valid localhost fallback may take ownership");
  assert.equal(events.posted, 1);

  events = await scenario({
    native: null,
    http: { ok: true, json: async () => ({ ok: true }) }
  });
  assert.equal(events.canceled, 0, "HTTP 200 without a pending ID is not proof of acceptance");

  events = await scenario({ native: null, http: new Error("network unavailable") });
  assert.equal(events.canceled, 0, "Network errors must preserve Chrome download");

  events = await scenario({ autoIntercept: false });
  assert.equal(events.canceled, 0, "Disabled interception must leave Chrome alone");

  events = await scenario({ id: 0 });
  assert.equal(events.canceled, 1, "Chrome download ID zero is valid");

  console.log("PASS: 8 browser handoff and filename regression cases");
})().catch(err => { console.error(err); process.exitCode = 1; });
