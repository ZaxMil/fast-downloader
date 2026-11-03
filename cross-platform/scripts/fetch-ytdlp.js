'use strict';
// Download only official pinned yt-dlp release binaries, and verify SHA-256
// before packaging them. Never trust an unverified "latest" executable.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const RELEASE = '2026.10.26';
const ASSETS = {
  win32: { name: 'yt-dlp.exe', installed: 'yt-dlp.exe',
    sha256: 'f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d' },
  linux: { name: 'yt-dlp_linux', installed: 'yt-dlp',
    sha256: '767ba9dce7052aa196f8b12652f433d76de128ba168966fa84b6394d5e6c6795' },
  darwin: { name: 'yt-dlp_macos', installed: 'yt-dlp',
    sha256: '0f922c15ccc0b83eefa1c8bc9104e434318c4bf16e408e1ee93b7745a6efe0aa' }
};
async function fetchVerifiedBinary(platform = process.platform) {
  const asset = ASSETS[platform];
  if (!asset) throw new Error('No yt-dlp binary available for ' + platform);
  const folder = path.join(__dirname, '..', 'bin');
  const destination = path.join(folder, asset.installed);
  fs.mkdirSync(folder, { recursive: true });
  const digest = data => crypto.createHash('sha256').update(data).digest('hex');
  if (fs.existsSync(destination) && digest(fs.readFileSync(destination)) === asset.sha256) {
    console.log('Verified cached yt-dlp for ' + platform);
    return destination;
  }
  const url = 'https://github.com/yt-dlp/yt-dlp/releases/download/' + RELEASE + '/' + asset.name;
  console.log('Fetching official yt-dlp ' + RELEASE + ' for ' + platform);
  const response = await fetch(url, { signal: AbortSignal.timeout(120000) });
  if (!response.ok) throw new Error('Cannot fetch yt-dlp: HTTP ' + response.status);
  const bytes = Buffer.from(await response.arrayBuffer());
  if (digest(bytes) !== asset.sha256) throw new Error('yt-dlp SHA-256 mismatch; refusing to package binary');
  fs.writeFileSync(destination, bytes, { mode: 0o755 });
  if (platform !== 'win32') fs.chmodSync(destination, 0o755);
  console.log('Verified yt-dlp: ' + bytes.length + ' bytes');
  return destination;
}
if (require.main === module) fetchVerifiedBinary().catch(err => { console.error(err); process.exitCode = 1; });
module.exports = { fetchVerifiedBinary, ASSETS, RELEASE };
