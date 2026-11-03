'use strict';
const { app, BrowserWindow, shell } = require('electron');
const { startServer } = require('./server');

let window, backend;
const singleInstance = app.requestSingleInstanceLock();
if (!singleInstance) app.quit();
else {
  app.on('second-instance', () => {
    if (window) { if (window.isMinimized()) window.restore(); window.focus(); }
  });
  app.whenReady().then(async () => {
    backend = await startServer({ port: 0 });
    window = new BrowserWindow({
      width: 1200, height: 850, minWidth: 760, minHeight: 550,
      title: 'FastDownloader Pro',
      backgroundColor: '#0b1019',
      show: false,
      webPreferences: { nodeIntegration: false, contextIsolation: true, sandbox: true,
        webSecurity: true, devTools: !app.isPackaged }
    });
    window.once('ready-to-show', () => window.show());
    window.webContents.setWindowOpenHandler(({ url }) => {
      if (/^https?:\/\//i.test(url)) shell.openExternal(url);
      return { action: 'deny' };
    });
    window.webContents.on('will-navigate', (event, url) => {
      if (!url.startsWith(backend.url)) event.preventDefault();
    });
    await window.loadURL(backend.url);
  }).catch(err => {
    console.error('Failed to start FastDownloader:', err);
    app.quit();
  });
  app.on('window-all-closed', () => app.quit());
  app.on('before-quit', () => { if (backend) backend.manager.stop(); });
}
