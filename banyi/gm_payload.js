'use strict';
// TOOL_DIR 由打包阶段 EnsurePayloadToolDir() 注入实际路径；源文件保持占位符以避免泄漏开发机路径
var TOOL_DIR = "__TOOL_DIR__";

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

// 占位符未被替换时回退到 WORK_DIR，避免把字面量 "__TOOL_DIR__" 当真实路径
var TOOL_ROOT = (typeof TOOL_DIR === 'string' && TOOL_DIR.length > 0 && TOOL_DIR.indexOf('__TOOL_DIR__') < 0) ? TOOL_DIR : WORK_DIR;

// 默认文件名（svc.cfg存在时会被覆盖）
var LOG_FILE = TOOL_ROOT + "\\gm_tool.log";
var RESULT_FILE = TOOL_ROOT + "\\gm_cmd_result.txt";
var CMD_FILE_FALLBACK = TOOL_ROOT + "\\gm_cmd.txt";
var MMF_NAME = "Global\\WinSvcSharedMem";
var MMF_SIZE = 262144; // 256KB

var kernel32 = Process.getModuleByName('kernel32.dll');
var CreateFileW = new NativeFunction(kernel32.getExportByName('CreateFileW'), 'pointer', ['pointer', 'uint', 'uint', 'pointer', 'uint', 'uint', 'pointer']);
var ReadFile = new NativeFunction(kernel32.getExportByName('ReadFile'), 'int', ['pointer', 'pointer', 'uint', 'pointer', 'pointer']);
var WriteFile = new NativeFunction(kernel32.getExportByName('WriteFile'), 'int', ['pointer', 'pointer', 'uint', 'pointer', 'pointer']);
var CloseHandle = new NativeFunction(kernel32.getExportByName('CloseHandle'), 'int', ['pointer']);
var SetFilePointer = new NativeFunction(kernel32.getExportByName('SetFilePointer'), 'uint', ['pointer', 'int', 'pointer', 'uint']);
var GetFileSize = new NativeFunction(kernel32.getExportByName('GetFileSize'), 'uint', ['pointer', 'pointer']);

// 读取随机化通信配置（覆盖默认文件名和共享内存名）
try {
    var cfgPath = TOOL_ROOT + "\\svc.cfg";
    var cfgBuf = Memory.allocUtf16String(cfgPath);
    var hCfg = CreateFileW(cfgBuf, 0x80000000, 1, ptr(0), 3, 0x80, ptr(0));
    if (!hCfg.equals(ptr(-1))) {
        var cfgSize = GetFileSize(hCfg, ptr(0));
        if (cfgSize > 0 && cfgSize < 4096) {
            var cfgData = Memory.alloc(cfgSize + 1);
            var cfgRead = Memory.alloc(4);
            ReadFile(hCfg, cfgData, cfgSize, cfgRead, ptr(0));
            var cfgText = cfgData.readUtf8String(cfgSize);
            // strip BOM and split by either \r\n or \n
            cfgText = cfgText.replace(/^﻿/, '');
            var cfgLines = cfgText.split(/\r?\n/);
            // path-traversal guard for filenames
            function safeFile(s) {
                s = (s || '').replace(/[\r\n\t]+/g, '').replace(/^\s+|\s+$/g, '');
                if (!s || s.indexOf('..') >= 0 || s.indexOf('/') >= 0 || s.indexOf('\\') >= 0 || s.indexOf(':') >= 0) return null;
                return s;
            }
            if (cfgLines.length >= 4) {
                var mmf = (cfgLines[0] || '').replace(/[\r\n\t]+/g, '').replace(/^\s+|\s+$/g, '');
                var f1 = safeFile(cfgLines[1]);
                var f2 = safeFile(cfgLines[2]);
                var f3 = safeFile(cfgLines[3]);
                if (mmf) MMF_NAME = mmf;
                if (f1) CMD_FILE_FALLBACK = TOOL_ROOT + '\\' + f1;
                if (f2) RESULT_FILE = TOOL_ROOT + '\\' + f2;
                if (f3) LOG_FILE = TOOL_ROOT + '\\' + f3;
            }
        }
        CloseHandle(hCfg);
    }
} catch (e) {}

// 共享内存 API
var OpenFileMappingW = new NativeFunction(kernel32.getExportByName('OpenFileMappingW'), 'pointer', ['uint', 'int', 'pointer']);
var MapViewOfFile = new NativeFunction(kernel32.getExportByName('MapViewOfFile'), 'pointer', ['pointer', 'uint', 'uint', 'uint', 'uint']);
var UnmapViewOfFile = new NativeFunction(kernel32.getExportByName('UnmapViewOfFile'), 'int', ['pointer']);
var FILE_MAP_READ = 4;
var FILE_MAP_WRITE = 2;
var FILE_MAP_ALL_ACCESS = 0xF001F;

var GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, CREATE_ALWAYS = 2, OPEN_ALWAYS = 4, FILE_SHARE_READ = 1;

// --- 日志缓冲 ---
var logBuffer = [];
var LOG_FLUSH_INTERVAL = 3000;

function flushLogBuffer() {
    if (logBuffer.length === 0) return;
    var lines = logBuffer.join('');
    logBuffer = [];
    try {
        var pathBuf = Memory.allocUtf16String(LOG_FILE);
        var hFile = CreateFileW(pathBuf, GENERIC_WRITE, FILE_SHARE_READ, ptr(0), OPEN_ALWAYS, 0x80, ptr(0));
        if (hFile.equals(ptr(-1))) hFile = CreateFileW(pathBuf, GENERIC_WRITE, 0, ptr(0), CREATE_ALWAYS, 0x80, ptr(0));
        if (!hFile.equals(ptr(-1))) {
            SetFilePointer(hFile, 0, ptr(0), 2);
            var dataBuf = Memory.allocUtf8String(lines);
            var writtenBuf = Memory.alloc(4);
            WriteFile(hFile, dataBuf, lines.length, writtenBuf, ptr(0));
            CloseHandle(hFile);
        }
    } catch (e) {}
}

function log(msg) {
    // send() removed: avoid named pipe detection
    var d = new Date();
    var ts = '[' + d.getHours() + ':' + ('0' + d.getMinutes()).slice(-2) + ':' + ('0' + d.getSeconds()).slice(-2) + '] ';
    logBuffer.push(ts + msg + '\n');
    if (msg.indexOf('ERROR') >= 0 || msg.indexOf('exception') >= 0 || msg.indexOf('lua_call ->') >= 0) {
        flushLogBuffer();
    }
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

function nativeReadFile(path) {
    try {
        var pathBuf = Memory.allocUtf16String(path);
        var hFile = CreateFileW(pathBuf, 0x80000000, 1, ptr(0), 3, 0x80, ptr(0));
        if (hFile.equals(ptr(-1))) return null;
        var size = GetFileSize(hFile, ptr(0));
        if (size <= 0 || size > 65536) { CloseHandle(hFile); return null; }
        var dataBuf = Memory.alloc(size + 1);
        var readBuf = Memory.alloc(4);
        ReadFile(hFile, dataBuf, size, readBuf, ptr(0));
        CloseHandle(hFile);
        return dataBuf.readUtf8String(size);
    } catch (e) {
        return null;
    }
}

// --- 共享内存 ---
// 共享内存布局 (256KB):
// [0-3]       命令seq (4字节)
// [4-7]       命令长度 (4字节)
// [8-131079]  命令内容 (128KB)
// [131072-131075] 结果seq (4字节)
// [131076-131079] 结果长度 (4字节)
// [131080-262143] 结果内容 (128KB)
var CMD_OFFSET = 0;
var RESULT_OFFSET = 131072; // 128KB
var HALF_SIZE = 131072;
var g_mmfView = null;

function openSharedMemory() {
    try {
        var nameBuf = Memory.allocUtf16String(MMF_NAME);
        var hMap = OpenFileMappingW(FILE_MAP_ALL_ACCESS, 0, nameBuf);
        if (hMap.isNull() || hMap.equals(ptr(-1))) {
            log('MMF: OpenFileMapping failed');
            return false;
        }
        var view = MapViewOfFile(hMap, FILE_MAP_ALL_ACCESS, 0, 0, MMF_SIZE);
        CloseHandle(hMap);
        if (view.isNull()) {
            log('MMF: MapViewOfFile failed');
            return false;
        }
        g_mmfView = view;
        log('MMF: mapped ok');
        return true;
    } catch (e) {
        log('MMF: open error: ' + e);
        return false;
    }
}

function readSharedCommand() {
    if (!g_mmfView) return null;
    try {
        var seq = g_mmfView.add(CMD_OFFSET).readU32();
        var cmdLen = g_mmfView.add(CMD_OFFSET + 4).readU32();
        if (cmdLen === 0 || cmdLen > HALF_SIZE - 8) return null;
        if (seq === g_state.lastSeq) return null;
        g_state.lastSeq = seq;
        var cmdBuf = g_mmfView.add(CMD_OFFSET + 8);
        var text = cmdBuf.readUtf8String(cmdLen);
        if (!text || text.length === 0) return null;
        return text;
    } catch (e) {
        return null;
    }
}

function writeSharedResult(text) {
    if (!g_mmfView) return false;
    try {
        // Use Memory.allocUtf8String for proper UTF-8 encoding (handles surrogate pairs)
        var byteLen = utf8ByteLength(text);
        if (byteLen > HALF_SIZE - 8) return false;
        var src = Memory.allocUtf8String(text);
        // Write payload first, then length, then publish via seq (read order: seq -> len -> payload)
        Memory.copy(g_mmfView.add(RESULT_OFFSET + 8), src, byteLen);
        g_mmfView.add(RESULT_OFFSET + 4).writeU32(byteLen);
        g_state.resultSeq = (g_state.resultSeq || 0) + 1;
        g_mmfView.add(RESULT_OFFSET).writeU32(g_state.resultSeq);
        return true;
    } catch (e) {
        return false;
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
            log('Signature not found near lua_call: ' + name);
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
    loadbufferx: null,
    loadbufferRvaText: '?',
    luaPcall: null,
    lastSeq: 0,
    hookDetached: false
};

function randomChunkName() {
    var prefixes = ['=[C]', '@SysInit', '@core', '=(load)', '@runtime', '=[string]'];
    var p = prefixes[(Math.random() * prefixes.length) | 0];
    var n = (Math.random() * 0x7fffffff) >>> 0;
    return p + n.toString(36);
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
    var nameText = randomChunkName();
    var commandLen = utf8ByteLength(commandText);
    log('CMD #' + seq + ' begin len=' + commandLen);
    var resultText = 'RUNNING\nseq=' + seq + '\n';
    writeSharedResult(resultText);
    nativeWriteFile(RESULT_FILE, resultText);
    try {
        var code = Memory.allocUtf8String(commandText);
        var name = Memory.allocUtf8String(nameText);
        var r = g_state.loadbufferx(L, code, uint64(commandLen), name, ptr(0));
        log('CMD #' + seq + ' loadbuf -> ' + r);
        if (r === 0) {
            var pr = g_state.luaPcall(L, 0, 0, 0, ptr(0), ptr(0));
            log('CMD #' + seq + ' lua_call -> ' + pr);
            resultText = 'DONE\nseq=' + seq + '\nloadbufferx=' + r + '\nlua_call=' + pr + '\n';
        } else {
            resultText = 'LOAD_FAIL\nseq=' + seq + '\nloadbufferx=' + r + '\n';
        }
    } catch (e) {
        log('CMD #' + seq + ' exception: ' + e);
        resultText = 'EXCEPTION\nseq=' + seq + '\nerror=' + e + '\n';
    }
    // Note: do not read RESULT_FILE here — it is our own write target.
    // Lua side-effect output is on Lua's responsibility to embed via print/return.
    writeSharedResult(resultText);
    nativeWriteFile(RESULT_FILE, resultText);
}

function pollAndExecute() {
    if (g_state.executing || !g_state.capturedL || !g_state.luaThreadId) return;
    var cmd = readSharedCommand();
    if (!cmd) return;

    g_state.executing = true;
    log('CMD from MMF (poll)');

    try {
        Process.runOnThread(g_state.luaThreadId, function () {
            executeCommandDirect(g_state.capturedL, cmd);
            return 0;
        }).then(function () {
            g_state.executing = false;
        }, function (e) {
            log('CMD runOnThread failed: ' + e);
            g_state.executing = false;
        });
    } catch (e) {
        log('CMD execute exception: ' + e);
        g_state.executing = false;
    }
}

log('=== svc v37.0 ===');
log('WORK_DIR: ' + WORK_DIR);
log('TOOL_ROOT: ' + TOOL_ROOT);

// Disabled: runtime memory scan may cause instability.
// function patchRuntimeStrings() { ... }
// patchRuntimeStrings();

var yysls = Process.findModuleByName('yysls.exe');
if (!yysls) {
    log('ERROR: yysls.exe not found!');
} else {
    var base = yysls.base;
    var modSize = yysls.size;
    log('module base: ' + base + ' size: 0x' + modSize.toString(16));

    var SIG_LUA_PCALL = '48 89 74 24 18 57 48 83 EC 40 33 F6 48 89 6C 24 58 49 63 C1 41 8B E8 48 8B F9 45 85 C9';
    var luaPcallAddr = scan(SIG_LUA_PCALL, 'lua_call', base, modSize);
    var loadbufferAddr = null;
    if (luaPcallAddr) {
        var SIG_LOADBUFFER_WRAPPER = '48 83 EC 48 48 8B 44 24 70 48 89 54 24 30 48 8D 15';
        loadbufferAddr = scanNear(SIG_LOADBUFFER_WRAPPER, 'loadbuf', luaPcallAddr, 0x80000, base, modSize);
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
                        log('candidate loadbuf @ RVA=0x' + rva.toString(16) + ' (adaptive guess)');
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

    if (!luaPcallAddr || !loadbufferAddr) {
        log('ERROR: entry points not found, cannot execute.');
        nativeWriteFile(RESULT_FILE, 'DIAGNOSTIC\nno_lua_entry=1\n');
        writeSharedResult('DIAGNOSTIC\nno_lua_entry=1\n');
    } else {
        g_state.luaPcall = new NativeFunction(luaPcallAddr, 'int', ['pointer', 'int', 'int', 'int', 'pointer', 'pointer']);

        // 短暂Hook：只为了捕获L指针和线程ID，捕获后立即detach
        // Use a local capture flag to win the race when multiple threads enter simultaneously.
        var captureClaimed = false;
        var tempHook = Interceptor.attach(luaPcallAddr, {
            onEnter: function (args) {
                // Atomic-ish first-writer-wins: only the first thread to flip captureClaimed
                // gets to assign capturedL/luaThreadId. JS in Frida runs single-threaded per
                // agent thread, but onEnter callbacks may interleave; check-then-set still
                // narrows the window dramatically vs. checking capturedL alone.
                if (!captureClaimed) {
                    captureClaimed = true;
                    g_state.capturedL = args[0];
                    g_state.luaThreadId = this.threadId;
                    g_state.hookDetached = true;
                    log('CAPTURED L=' + g_state.capturedL + ' tid=' + this.threadId);
                    try {
                        tempHook.detach();
                        log('Hook detached');
                    } catch (e) {
                        log('detach error: ' + e);
                    }
                }
            }
        });
        log('Hook attached, waiting for capture.');

        // 打开共享内存
        var mmfOk = openSharedMemory();

        // 如果共享内存不可用，降级为文件轮询
        if (!mmfOk) {
            log('WARNING: MMF not available, falling back to file.');
        }

        nativeWriteFile(RESULT_FILE, 'READY\nmode=' + (mmfOk ? 'shared_memory' : 'file_fallback') + '\n');
        writeSharedResult('READY\nmode=' + (mmfOk ? 'shared_memory' : 'file_fallback') + '\n');
        log('Ready. Mode: ' + (mmfOk ? 'mmf' : 'file'));

        // 日志定时刷新
        setInterval(flushLogBuffer, LOG_FLUSH_INTERVAL);

        // 主轮询：1-3秒随机间隔，避免固定周期特征
        function scheduleNextPoll() {
            var delay = 1000 + Math.random() * 2000;
            setTimeout(function () {
                if (!g_state.capturedL || !g_state.luaThreadId) {
                    scheduleNextPoll();
                    return;
                }
                if (mmfOk) {
                    pollAndExecute();
                } else {
                    // 降级：文件轮询
                    if (g_state.executing) { scheduleNextPoll(); return; }
                    var cmdFile = CMD_FILE_FALLBACK;
                try {
                    var pathBuf = Memory.allocUtf16String(cmdFile);
                    var hFile = CreateFileW(pathBuf, 0x80000000, 1 | 2, ptr(0), 3, 0x80, ptr(0));
                    if (!hFile.equals(ptr(-1))) {
                        var size = GetFileSize(hFile, ptr(0));
                        CloseHandle(hFile);
                        if (size > 0 && size !== 0xffffffff) {
                            var content = '';
                            try {
                                var pathBuf2 = Memory.allocUtf16String(cmdFile);
                                var hFile2 = CreateFileW(pathBuf2, 0x80000000, 1 | 2, ptr(0), 3, 0x80, ptr(0));
                                if (!hFile2.equals(ptr(-1))) {
                                    var sz = GetFileSize(hFile2, ptr(0));
                                    if (sz > 0 && sz < 262144) {
                                        var dataBuf = Memory.alloc(sz + 1);
                                        var readBuf = Memory.alloc(4);
                                        var ok = ReadFile(hFile2, dataBuf, sz, readBuf, ptr(0));
                                        CloseHandle(hFile2);
                                        if (ok) {
                                            var readLen = readBuf.readU32();
                                            if (readLen > 0 && readLen <= sz) {
                                                dataBuf.add(readLen).writeU8(0);
                                                content = dataBuf.readUtf8String(readLen) || '';
                                            }
                                        }
                                    } else {
                                        CloseHandle(hFile2);
                                    }
                                }
                            } catch (e) {}
                            content = content.replace(/^\uFEFF/, '').replace(/^\s+|\s+$/g, '');
                            if (content) {
                                // 清空命令文件
                                var pathBuf3 = Memory.allocUtf16String(cmdFile);
                                var hFile3 = CreateFileW(pathBuf3, GENERIC_WRITE, 0, ptr(0), CREATE_ALWAYS, 0x80, ptr(0));
                                if (!hFile3.equals(ptr(-1))) CloseHandle(hFile3);

                                g_state.executing = true;
                                log('CMD from file (fallback)');
                                try {
                                    Process.runOnThread(g_state.luaThreadId, function () {
                                        executeCommandDirect(g_state.capturedL, content);
                                        return 0;
                                    }).then(function () {
                                        g_state.executing = false;
                                    }, function (e) {
                                        log('CMD runOnThread failed: ' + e);
                                        g_state.executing = false;
                                    });
                                } catch (e) {
                                    log('CMD execute exception: ' + e);
                                    g_state.executing = false;
                                }
                            }
                        }
                    }
                } catch (e) {}
                }
                scheduleNextPoll();
            }, delay);
        }
        scheduleNextPoll();
    }
}
