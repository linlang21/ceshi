'use strict';

var WORK_DIR = ".";
try {
    var yyslsMod = Process.findModuleByName('yysls.exe');
    if (yyslsMod) {
        var p = yyslsMod.path;
        var idx = p.lastIndexOf('\\');
        if (idx < 0) idx = p.lastIndexOf('/');
        if (idx > 0) WORK_DIR = p.substring(0, idx);
    }
} catch (e) {}

var TOOL_ROOT = (typeof TOOL_DIR === 'string' && TOOL_DIR.length > 0) ? TOOL_DIR : WORK_DIR;
var LOG_FILE = (typeof LOG_PATH === 'string' && LOG_PATH.length > 0) ? LOG_PATH : (TOOL_ROOT + "\\gm_tool.log");
var CMD_FILE = TOOL_ROOT + "\\gm_cmd.txt";
var ALT_CMD_FILE = WORK_DIR + "\\gm_cmd.txt";
var RESULT_FILE = TOOL_ROOT + "\\gm_cmd_result.txt";

var kernel32 = Process.getModuleByName('kernel32.dll');
var CreateFileW = new NativeFunction(kernel32.getExportByName('CreateFileW'), 'pointer', ['pointer', 'uint', 'uint', 'pointer', 'uint', 'uint', 'pointer']);
var ReadFile = new NativeFunction(kernel32.getExportByName('ReadFile'), 'int', ['pointer', 'pointer', 'uint', 'pointer', 'pointer']);
var WriteFile = new NativeFunction(kernel32.getExportByName('WriteFile'), 'int', ['pointer', 'pointer', 'uint', 'pointer', 'pointer']);
var CloseHandle = new NativeFunction(kernel32.getExportByName('CloseHandle'), 'int', ['pointer']);
var SetFilePointer = new NativeFunction(kernel32.getExportByName('SetFilePointer'), 'uint', ['pointer', 'int', 'pointer', 'uint']);
var GetFileSize = new NativeFunction(kernel32.getExportByName('GetFileSize'), 'uint', ['pointer', 'pointer']);
var GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, CREATE_ALWAYS = 2, OPEN_EXISTING = 3, OPEN_ALWAYS = 4, FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;

function log(msg) {
    send('[GM] ' + msg);
    try {
        var pathBuf = Memory.allocUtf16String(LOG_FILE);
        var hFile = CreateFileW(pathBuf, GENERIC_WRITE, FILE_SHARE_READ, ptr(0), OPEN_ALWAYS, 0x80, ptr(0));
        if (hFile.equals(ptr(-1))) hFile = CreateFileW(pathBuf, GENERIC_WRITE, 0, ptr(0), CREATE_ALWAYS, 0x80, ptr(0));
        if (!hFile.equals(ptr(-1))) {
            SetFilePointer(hFile, 0, ptr(0), 2);
            var d = new Date();
            var ts = '[' + d.getHours() + ':' + ('0' + d.getMinutes()).slice(-2) + ':' + ('0' + d.getSeconds()).slice(-2) + '] ';
            var line = ts + msg + '\n';
            var dataBuf = Memory.allocUtf8String(line);
            var writtenBuf = Memory.alloc(4);
            WriteFile(hFile, dataBuf, line.length, writtenBuf, ptr(0));
            CloseHandle(hFile);
        }
    } catch (e) {}
}

function nativeWriteFile(path, text) {
    try {
        var pathBuf = Memory.allocUtf16String(path);
        var hFile = CreateFileW(pathBuf, GENERIC_WRITE, 0, ptr(0), CREATE_ALWAYS, 0x80, ptr(0));
        if (hFile.equals(ptr(-1))) return false;
        var dataBuf = Memory.allocUtf8String(text);
        var writtenBuf = Memory.alloc(4);
        WriteFile(hFile, dataBuf, text.length, writtenBuf, ptr(0));
        CloseHandle(hFile);
        return true;
    } catch (e) {
        return false;
    }
}

function nativeReadFile(path, maxBytes) {
    try {
        var pathBuf = Memory.allocUtf16String(path);
        var hFile = CreateFileW(pathBuf, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, ptr(0), OPEN_EXISTING, 0x80, ptr(0));
        if (hFile.equals(ptr(-1))) return '';
        var size = GetFileSize(hFile, ptr(0));
        if (size === 0 || size === 0xffffffff) {
            CloseHandle(hFile);
            return '';
        }
        if (size > maxBytes) size = maxBytes;
        var dataBuf = Memory.alloc(size + 1);
        var readBuf = Memory.alloc(4);
        var ok = ReadFile(hFile, dataBuf, size, readBuf, ptr(0));
        CloseHandle(hFile);
        if (!ok) return '';
        var readLen = readBuf.readU32();
        if (readLen <= 0) return '';
        dataBuf.add(readLen).writeU8(0);
        return dataBuf.readUtf8String(readLen) || '';
    } catch (e) {
        return '';
    }
}

function scan(sig, name, base, modSize) {
    try {
        var res = Memory.scanSync(base, modSize, sig);
        if (!res.length) {
            log('Signature not found: ' + name);
            return null;
        }
        var addr = res[0].address;
        log(name + ' @ RVA=0x' + addr.sub(base).toString(16));
        return addr;
    } catch (e) {
        log('scan error for ' + name + ': ' + e);
        return null;
    }
}

function scanNear(sig, name, center, range, base, modSize) {
    try {
        var start = center.sub(range);
        if (start.compare(base) < 0) start = base;
        var end = center.add(range);
        var modEnd = base.add(modSize);
        if (end.compare(modEnd) > 0) end = modEnd;
        var size = end.sub(start).toUInt32();
        var res = Memory.scanSync(start, size, sig);
        if (!res.length) {
            log('Signature not found near lua_pcall: ' + name);
            return null;
        }
        var addr = res[0].address;
        log(name + ' @ RVA=0x' + addr.sub(base).toString(16) + ' (near scan)');
        return addr;
    } catch (e) {
        log('near scan error for ' + name + ': ' + e);
        return null;
    }
}

var g_state = {
    executing: false,
    commandSeq: 0,
    capturedL: null,
    luaThreadId: 0,
    lastPcallAt: 0,
    loadbufferx: null,
    loadbufferRvaText: '?',
    luaPcall: null
};

function normalizeCommand(text) {
    if (!text) return '';
    text = text.replace(/^\uFEFF/, '');
    text = text.replace(/^\s+|\s+$/g, '');
    return text;
}

function utf8ByteLength(text) {
    var len = 0;
    for (var i = 0; i < text.length; i++) {
        var code = text.charCodeAt(i);
        if (code < 0x80) len += 1;
        else if (code < 0x800) len += 2;
        else if (code >= 0xd800 && code <= 0xdbff) {
            len += 4;
            i += 1;
        } else len += 3;
    }
    return len;
}

function executeCommandDirect(L, commandText) {
    g_state.commandSeq += 1;
    var seq = g_state.commandSeq;
    var nameText = 'GM_CMD_DIRECT_' + seq;
    var commandLen = utf8ByteLength(commandText);
    log('CMD #' + seq + ' begin len=' + commandLen + ' L=' + L);
    nativeWriteFile(RESULT_FILE, 'RUNNING\nseq=' + seq + '\n');
    try {
        var code = Memory.allocUtf8String(commandText);
        var name = Memory.allocUtf8String(nameText);
        var r = g_state.loadbufferx(L, code, uint64(commandLen), name, ptr(0));
        log('CMD #' + seq + ' loadbufferx(0x' + g_state.loadbufferRvaText + ') -> ' + r);
        if (r === 0) {
            var pr = g_state.luaPcall(L, 0, 0, 0, ptr(0), ptr(0));
            log('CMD #' + seq + ' lua_pcall -> ' + pr);
            nativeWriteFile(RESULT_FILE, 'DONE\nseq=' + seq + '\nloadbufferx=' + r + '\nlua_pcall=' + pr + '\n');
        } else {
            nativeWriteFile(RESULT_FILE, 'LOAD_FAIL\nseq=' + seq + '\nloadbufferx=' + r + '\n');
        }
    } catch (e) {
        log('CMD #' + seq + ' exception: ' + e);
        nativeWriteFile(RESULT_FILE, 'EXCEPTION\nseq=' + seq + '\nerror=' + e + '\n');
    }
}

function readPendingCommand() {
    var primary = normalizeCommand(nativeReadFile(CMD_FILE, 1024 * 256));
    if (primary) return { path: CMD_FILE, text: primary };
    var alt = normalizeCommand(nativeReadFile(ALT_CMD_FILE, 1024 * 256));
    if (alt) return { path: ALT_CMD_FILE, text: alt };
    return null;
}

function executePendingCommand(reason, L, threadId) {
    if (g_state.executing) return;
    var pendingCommand = null;
    try {
        pendingCommand = readPendingCommand();
    } catch (e) {
        log('CMD read exception: ' + e);
        return;
    }
    if (!pendingCommand) return;

    g_state.executing = true;
    nativeWriteFile(pendingCommand.path, '');
    log('CMD source (' + reason + '): ' + pendingCommand.path);

    try {
        if (reason === 'pcall') {
            executeCommandDirect(L, pendingCommand.text);
            g_state.executing = false;
            return;
        }

        if (!threadId) {
            log('CMD skipped: missing lua thread id');
            nativeWriteFile(pendingCommand.path, pendingCommand.text);
            g_state.executing = false;
            return;
        }

        Process.runOnThread(threadId, function () {
            executeCommandDirect(g_state.capturedL, pendingCommand.text);
            return 0;
        }).then(function () {
            g_state.executing = false;
        }, function (e) {
            log('CMD runOnThread failed: ' + e);
            nativeWriteFile(pendingCommand.path, pendingCommand.text);
            g_state.executing = false;
        });
    } catch (e) {
        log('CMD execute exception: ' + e);
        nativeWriteFile(pendingCommand.path, pendingCommand.text);
        g_state.executing = false;
    }
}

log('=== Frida GM Payload v33.2 (adaptive loadbuffer executor) ===');
log('WORK_DIR: ' + WORK_DIR);
log('TOOL_ROOT: ' + TOOL_ROOT);

var yysls = Process.findModuleByName('yysls.exe');
if (!yysls) {
    log('ERROR: yysls.exe not found!');
} else {
    var base = yysls.base;
    var modSize = yysls.size;
    log('yysls.exe base: ' + base + ' size: 0x' + modSize.toString(16));

    var SIG_LUA_PCALL = '48 89 74 24 18 57 48 83 EC 40 33 F6 48 89 6C 24 58 49 63 C1 41 8B E8 48 8B F9 45 85 C9';
    var luaPcallAddr = scan(SIG_LUA_PCALL, 'lua_pcall', base, modSize);
    var loadbufferAddr = null;
    if (luaPcallAddr) {
        var SIG_LOADBUFFER_WRAPPER = '48 83 EC 48 48 8B 44 24 70 48 89 54 24 30 48 8D 15';
        loadbufferAddr = scanNear(SIG_LOADBUFFER_WRAPPER, 'luaL_loadbufferx wrapper', luaPcallAddr, 0x80000, base, modSize);
        var pcallRva = parseInt(luaPcallAddr.sub(base).toString(16), 16);
        var oldPcallRva = 0x48dbcd0;
        var oldLoadRva = 0x48deb00;
        var guessedLoadRva = oldLoadRva + (pcallRva - oldPcallRva);
        var guesses = [
            guessedLoadRva,
            pcallRva + 0x2e30,
            0x48deb00,
            0x48df720,
            0x48dc060
        ];
        var seenGuess = {};
        if (!loadbufferAddr) {
            for (var gi = 0; gi < guesses.length; gi++) {
                var rva = guesses[gi];
                if (rva <= 0 || seenGuess[rva]) continue;
                seenGuess[rva] = true;
                var addr = base.add(rva);
                try {
                    var head = addr.readByteArray(8);
                    if (head) {
                        loadbufferAddr = addr;
                        log('candidate luaL_loadbufferx wrapper @ RVA=0x' + rva.toString(16) + ' (adaptive guess)');
                        break;
                    }
                } catch (e) {}
            }
        }
    }
    if (loadbufferAddr) {
        g_state.loadbufferRvaText = loadbufferAddr.sub(base).toString(16);
        g_state.loadbufferx = new NativeFunction(loadbufferAddr, 'int', ['pointer', 'pointer', 'uint64', 'pointer', 'pointer']);
    }
    log('This version polls gm_cmd.txt during natural lua_pcall and executes command chunks directly.');

    if (!luaPcallAddr || !loadbufferAddr) {
        log('ERROR: lua_pcall or luaL_loadbufferx not found, cannot execute Lua safely.');
        nativeWriteFile(RESULT_FILE, 'DIAGNOSTIC\nno_lua_entry=1\n');
    } else {
        g_state.luaPcall = new NativeFunction(luaPcallAddr, 'int', ['pointer', 'int', 'int', 'int', 'pointer', 'pointer']);

        Interceptor.attach(luaPcallAddr, {
            onEnter: function (args) {
                g_state.lastPcallAt = Date.now();
                g_state.luaThreadId = this.threadId;
                if (!g_state.capturedL) {
                    g_state.capturedL = args[0];
                    log('CAPTURED L=' + g_state.capturedL + ' thread=' + this.threadId);
                }
                executePendingCommand('pcall', args[0], this.threadId);
            }
        });

        nativeWriteFile(RESULT_FILE, 'READY\nmode=command_direct\n');
        log('Ready. Command direct executor is polling: ' + CMD_FILE + ' and ' + ALT_CMD_FILE);

        setInterval(function () {
            if (!g_state.capturedL || !g_state.luaThreadId || g_state.executing) return;
            var idleMs = Date.now() - g_state.lastPcallAt;
            if (idleMs < 250) return;
            executePendingCommand('runOnThread', g_state.capturedL, g_state.luaThreadId);
        }, 200);
    }
}
