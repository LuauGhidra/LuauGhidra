'use strict';

const { execFileSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { Deobfuscator } = require('./deobf/deobfuscator.js');
const { Serializer } = require('./deobf/bytecode/serializer.js');
const { Disassembler } = require('./deobf/bytecode/disassembler.js');

const TOOLS_DIR = path.join(__dirname, '..', 'tools');
const LUADEC = process.platform === 'win32'
    ? path.join(TOOLS_DIR, 'luadec.exe')
    : path.join(TOOLS_DIR, 'luadec');
const UNLUAC = path.join(TOOLS_DIR, 'unluac.jar');

function deobfuscateToBytecode(code) {
    const result = new Deobfuscator().deobfuscate(code);
    return new Serializer().serialize(result);
}

function stripHeader(output) {
    const lines = output.split('\n');
    const start = lines.findIndex(line => line.startsWith('-- params'));
    return start === -1 ? output : lines.slice(start).join('\n');
}

function runLuadec(tmp) {
    return execFileSync(LUADEC, [tmp], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'ignore'] });
}

function runUnluac(tmp) {
    return execFileSync('java', ['-jar', UNLUAC, tmp], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'ignore'] });
}

function withTempBytecode(bytes, fn) {
    const tmp = path.join(os.tmpdir(), `ms-${process.pid}-${Date.now()}.luac`);
    fs.writeFileSync(tmp, bytes);

    try {
        return fn(tmp);
    } finally {
        fs.rmSync(tmp, { force: true });
    }
}

function decompile(bytes) {
    if (fs.existsSync(LUADEC)) {
        return stripHeader(withTempBytecode(bytes, runLuadec));
    }

    if (fs.existsSync(UNLUAC)) {
        return stripHeader(withTempBytecode(bytes, runUnluac));
    }

    throw new Error('no decompiler available: expected luadec (or luadec.exe on Windows) or unluac.jar in tools/');
}

function deobfuscateToSource(code) {
    const bytes = deobfuscateToBytecode(code);
    return decompile(bytes);
}

function deobfuscateToDisassembly(code) {
    const result = new Deobfuscator().deobfuscate(code);
    return new Disassembler(result).disassemble();
}

module.exports = { deobfuscateToBytecode, deobfuscateToSource, deobfuscateToDisassembly };