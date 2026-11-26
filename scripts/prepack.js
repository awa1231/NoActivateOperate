#!/usr/bin/env node
'use strict';

// Runs automatically on "npm pack" / "npm publish".
// Copies the Windows executable next to the launcher so the tarball is self-contained.
// If the executable is missing, it is built with build.bat (Windows only).

const { execSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const source = path.join(root, 'Non Activating Operate.exe');
const binDir = path.join(root, 'bin');
const target = path.join(binDir, 'Non Activating Operate.exe');

if (!fs.existsSync(source)) {
  if (process.platform !== 'win32') {
    throw new Error('The executable is missing. Publish from Windows, or place the built exe at the repository root.');
  }
  console.log('building Non Activating Operate.exe ...');
  execSync('"' + path.join(root, 'build.bat') + '" -nopause', { cwd: root, stdio: 'inherit' });
}

if (!fs.existsSync(source)) {
  throw new Error('Non Activating Operate.exe is missing. Run build.bat first.');
}

fs.mkdirSync(binDir, { recursive: true });
fs.copyFileSync(source, target);
console.log('packaged ' + path.basename(target) + ' (' + fs.statSync(target).size + ' bytes)');
