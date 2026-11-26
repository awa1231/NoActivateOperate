#!/usr/bin/env node
'use strict';

const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

if (process.platform !== 'win32') {
  console.error('Non Activating Operate is a Windows utility; it runs on Windows 10 and 11 only.');
  process.exit(1);
}

const exe = path.join(__dirname, 'Non Activating Operate.exe');
if (!fs.existsSync(exe)) {
  console.error('Executable not found: ' + exe);
  console.error('Reinstall the package: npm install -g non-activating-operate');
  process.exit(1);
}

const child = spawn(exe, [], { detached: true, stdio: 'ignore', windowsHide: false });
child.unref();

console.log('Non Activating Operate started.');
console.log('Look for the two-window icon in the notification area, then press Ctrl+F to enable it.');
console.log('Right-click the tray icon for options: stacking mode, shortcut, launch at startup, exit.');
process.exit(0);
