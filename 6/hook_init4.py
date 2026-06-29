"""
专门抓取初始化按钮4 + CreateProcessA 参数
=========================================
流程：
  Phase 1 (30s): PRE_CLICK   - 不点击，记录基线
  Phase 2 (60s): INIT_BTN_4  - 点击初始化按钮4，记录所有调用
"""
import frida
import sys
import time
import os

AAA_PID = 17164
YYSLS_PID = 1900
LOG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'hook_init4_log.txt')

JS_SRC = r"""
'use strict';

var stats = {
    ntR64: 0, ntW64: 0,
    ntOpen: 0, ntClose: 0,
    vAllocEx: 0, vProtectEx: 0,
    wpm: 0,
    createProcessA: 0, createProcessW: 0,
    createRemoteThread: 0,
};

var yyslsPid = 1900;  // 已正确
var yyslsHandle = null;
var allHandles = {};

var currentMarker = '';
var callCounter = 0;

var logLines = [];
function pushLog(s) {
    logLines.push(s);
    if (logLines.length > 20) flushLog();
}
function pushLogNow(s) {
    logLines.push(s);
    flushLog();
}
function flushLog() {
    if (logLines.length === 0) return;
    send({ type: 'log', lines: logLines.join('\n') });
    logLines = [];
}

function u64FromArgs(args, idx) {
    var lo = args[idx].toInt32() >>> 0;
    var hi = args[idx + 1].toInt32() >>> 0;
    return { lo: lo, hi: hi,
             str: '0x' + hi.toString(16).padStart(8, '0') + lo.toString(16).padStart(8, '0') };
}

function hexify(arrbuf) {
    var b = new Uint8Array(arrbuf);
    var s = '';
    for (var i = 0; i < b.length; i++) {
        s += b[i].toString(16).padStart(2, '0');
        if ((i & 3) === 3 && i !== b.length - 1) s += ' ';
    }
    return s;
}

function readWString(ptr) {
    try {
        if (ptr.isNull()) return '<null>';
        return ptr.readUtf16String();
    } catch (e) { return '<unreadable>'; }
}

function readString(ptr) {
    try {
        if (ptr.isNull()) return '<null>';
        return ptr.readAnsiString();
    } catch (e) { return '<unreadable>'; }
}

function getBacktrace(ctx, depth) {
    depth = depth || 15;
    var bt = [];
    try {
        var bt_arr = Thread.backtrace(ctx, Backtracer.ACCURATE);
        for (var i = 0; i < Math.min(depth, bt_arr.length); i++) {
            var sym = DebugSymbol.fromAddress(bt_arr[i]);
            var mod = sym.moduleName || '?';
            var name = sym.name || '?';
            var off = bt_arr[i].sub(sym.address);
            var addrStr = bt_arr[i].toString();
            bt.push(addrStr + ' ' + mod + '!' + name + '+0x' + off.toString(16));
        }
    } catch (e) {
        bt.push('bt-error: ' + e.message);
    }
    return bt;
}

function isYyslsHandle(h) {
    if (h === yyslsHandle) return true;
    if (allHandles[h]) return true;
    return false;
}

// ====== NtOpenProcess ======
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtOpenProcess'); }
    catch (e) { addr = nt.getExportByName('ZwOpenProcess'); }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            try {
                var cid = args[3];
                if (cid.isNull()) return;
                var pid = cid.readU32();
                this.pid = pid;
                this.phandle = args[0];
            } catch (e) {}
        },
        onLeave: function (retval) {
            stats.ntOpen++;
            if (this.pid === yyslsPid) {
                try {
                    var h = this.phandle.readU32();
                    allHandles[h] = true;
                    if (yyslsHandle === null) yyslsHandle = h;
                    if (currentMarker) {
                        pushLog('[OPEN yysls MARK=' + currentMarker + '] H=0x' + h.toString(16));
                    }
                } catch (e) {}
            }
        }
    });
})();

// ====== NtWow64ReadVirtualMemory64（标记期间记录详情）======
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64ReadVirtualMemory64'); }
    catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntR64++;
            callCounter++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.isYysls = isYyslsHandle(this.handle);
            if (currentMarker && this.isYysls) {
                this.marker = currentMarker;
                this.callIdx = callCounter;
            }
        },
        onLeave: function (retval) {
            if (!this.marker) return;
            var status = retval.toInt32() >>> 0;
            var msg = '[R64 MARK=' + this.marker + '] #' + this.callIdx +
                      ' Base=' + this.base.str +
                      ' Size=0x' + this.size.lo.toString(16) +
                      ' status=0x' + status.toString(16);
            if (this.size.lo > 0 && this.size.lo <= 64) {
                try {
                    msg += ' data=' + hexify(this.buf.readByteArray(this.size.lo));
                } catch (e) {}
            }
            pushLog(msg);
        }
    });
})();

// ====== NtWow64WriteVirtualMemory64（全部记录，含调用栈）======
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64WriteVirtualMemory64'); }
    catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntW64++;
            callCounter++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.isYysls = isYyslsHandle(this.handle);
            this.ctx = this.context;
            this.callIdx = callCounter;
            this.marker = currentMarker || 'UNMARKED';
            try {
                var n = Math.min(this.size.lo, 256);
                this.dataHex = hexify(this.buf.readByteArray(n));
            } catch (e) { this.dataHex = '?'; }
            this.bt = getBacktrace(this.ctx, 12);
        },
        onLeave: function (retval) {
            var status = retval.toInt32() >>> 0;
            var tag = this.isYysls ? 'yysls' : 'other';
            pushLogNow('[W64 ' + tag + ' MARK=' + this.marker + '] #' + this.callIdx +
                    ' Base=' + this.base.str +
                    ' Size=0x' + this.size.lo.toString(16) +
                    ' status=0x' + status.toString(16) +
                    ' data=' + this.dataHex);
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('    bt[' + i + '] ' + this.bt[i]);
            }
        }
    });
})();

// ====== CreateProcessA（关键！捕获启动参数）======
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('CreateProcessA'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.createProcessA++;
            callCounter++;
            this.marker = currentMarker || 'UNMARKED';
            this.callIdx = callCounter;
            this.appName = readString(args[0]);
            this.cmdLine = readString(args[1]);
            this.workDir = readString(args[11]);
            this.bt = getBacktrace(this.context, 15);
            // STARTUPINFO
            try {
                var si = args[9];
                this.siFlags = si.readU32();
                this.siDesktop = readString(si.add(8));
            } catch (e) { this.siFlags = '?'; this.siDesktop = '?'; }
            // PROCESS_INFORMATION 输出
            this.piPtr = args[10];
        },
        onLeave: function (retval) {
            pushLogNow('\n>>> [CreateProcessA MARK=' + this.marker + '] #' + this.callIdx);
            pushLogNow('    appName  = ' + this.appName);
            pushLogNow('    cmdLine  = ' + this.cmdLine);
            pushLogNow('    workDir  = ' + this.workDir);
            pushLogNow('    siFlags  = 0x' + (this.siFlags || 0).toString(16));
            pushLogNow('    siDesktop= ' + this.siDesktop);
            pushLogNow('    success  = ' + retval);
            if (retval.toInt32() !== 0) {
                try {
                    var hProc = this.piPtr.readU32();
                    var hThread = this.piPtr.add(4).readU32();
                    var pid = this.piPtr.add(8).readU32();
                    var tid = this.piPtr.add(12).readU32();
                    pushLogNow('    pi.hProcess=0x' + hProc.toString(16));
                    pushLogNow('    pi.hThread  =0x' + hThread.toString(16));
                    pushLogNow('    pi.dwPID    =' + pid);
                    pushLogNow('    pi.dwTID    =' + tid);
                } catch (e) {}
            }
            pushLogNow('    backtrace:');
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('      bt[' + i + '] ' + this.bt[i]);
            }
            pushLogNow('<<<\n');
        }
    });
})();

// ====== CreateProcessW ======
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('CreateProcessW'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.createProcessW++;
            callCounter++;
            this.marker = currentMarker || 'UNMARKED';
            this.callIdx = callCounter;
            this.appName = readWString(args[0]);
            this.cmdLine = readWString(args[1]);
            this.workDir = readWString(args[11]);
            this.bt = getBacktrace(this.context, 15);
        },
        onLeave: function (retval) {
            pushLogNow('\n>>> [CreateProcessW MARK=' + this.marker + '] #' + this.callIdx);
            pushLogNow('    appName  = ' + this.appName);
            pushLogNow('    cmdLine  = ' + this.cmdLine);
            pushLogNow('    workDir  = ' + this.workDir);
            pushLogNow('    success  = ' + retval);
            pushLogNow('    backtrace:');
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('      bt[' + i + '] ' + this.bt[i]);
            }
            pushLogNow('<<<\n');
        }
    });
})();

// ====== VirtualAllocEx（检测注入）======
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('VirtualAllocEx'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.vAllocEx++;
            callCounter++;
            this.handle = args[0].toInt32() >>> 0;
            this.addr = args[1];
            this.size = args[2].toInt32() >>> 0;
            this.protect = args[3].toInt32() >>> 0;
            this.isYysls = isYyslsHandle(this.handle);
            this.marker = currentMarker || 'UNMARKED';
            this.callIdx = callCounter;
            this.bt = getBacktrace(this.context, 12);
        },
        onLeave: function (retval) {
            var tag = this.isYysls ? 'yysls' : 'other';
            var protStr = '';
            if (this.protect === 0x40) protStr = 'RWX';
            else if (this.protect === 0x04) protStr = 'RW';
            else if (this.protect === 0x20) protStr = 'RX';
            else protStr = '0x' + this.protect.toString(16);
            pushLogNow('[VAllocEx ' + tag + ' MARK=' + this.marker + '] #' + this.callIdx +
                    ' Addr=' + this.addr +
                    ' Size=0x' + this.size.toString(16) +
                    ' Prot=' + protStr +
                    ' Result=0x' + retval.toString(16));
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('    bt[' + i + '] ' + this.bt[i]);
            }
        }
    });
})();

// ====== WriteProcessMemory（32位写入）======
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('WriteProcessMemory'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.wpm++;
            callCounter++;
            this.handle = args[0].toInt32() >>> 0;
            this.addr = args[1];
            this.buf = args[2];
            this.size = args[3].toInt32() >>> 0;
            this.isYysls = isYyslsHandle(this.handle);
            this.marker = currentMarker || 'UNMARKED';
            this.callIdx = callCounter;
            try {
                var n = Math.min(this.size, 256);
                this.dataHex = hexify(this.buf.readByteArray(n));
            } catch (e) { this.dataHex = '?'; }
            this.bt = getBacktrace(this.context, 12);
        },
        onLeave: function (retval) {
            if (!this.isYysls && this.marker === 'UNMARKED') return;
            var tag = this.isYysls ? 'yysls' : 'other';
            pushLogNow('[WPM ' + tag + ' MARK=' + this.marker + '] #' + this.callIdx +
                    ' Addr=' + this.addr +
                    ' Size=0x' + this.size.toString(16) +
                    ' data=' + this.dataHex);
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('    bt[' + i + '] ' + this.bt[i]);
            }
        }
    });
})();

// ====== CreateRemoteThread ======
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('CreateRemoteThread'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.createRemoteThread++;
            callCounter++;
            this.handle = args[0].toInt32() >>> 0;
            this.startAddr = args[4];
            this.isYysls = isYyslsHandle(this.handle);
            this.marker = currentMarker || 'UNMARKED';
            this.callIdx = callCounter;
            this.bt = getBacktrace(this.context, 12);
        },
        onLeave: function (retval) {
            pushLogNow('[CreateRemoteThread MARK=' + this.marker + '] #' + this.callIdx +
                    ' StartAddr=' + this.startAddr +
                    ' Result=0x' + retval.toString(16));
            for (var i = 0; i < this.bt.length; i++) {
                pushLogNow('    bt[' + i + '] ' + this.bt[i]);
            }
        }
    });
})();

// ====== RPC ======
rpc.exports = {
    setMarker: function (m) {
        currentMarker = m;
        pushLog('\n========== MARKER: ' + m + ' (call#' + callCounter + ') ==========\n');
        flushLog();
        return 'ok';
    },
    clearMarker: function () {
        currentMarker = '';
        return 'ok';
    },
    getStats: function () {
        return JSON.stringify({
            stats: stats,
            callCounter: callCounter,
            currentMarker: currentMarker,
            yyslsHandle: yyslsHandle,
            knownHandles: Object.keys(allHandles).length
        });
    }
};

pushLog('[*] Init-btn-4 hooks installed on PID ' + Process.id + ' arch=' + Process.arch);
flushLog();
"""


def main():
    try:
        session = frida.attach(AAA_PID)
    except Exception as e:
        print(f"[!] 无法附加到 PID {AAA_PID}: {e}")
        sys.exit(1)

    script = session.create_script(JS_SRC)

    log_file = open(LOG_PATH, 'w', encoding='utf-8')

    def on_message(message, data):
        mtype = message.get('type')
        if mtype == 'send':
            payload = message.get('payload')
            if isinstance(payload, dict) and payload.get('type') == 'log':
                text = payload.get('lines', '')
                print(text)
                log_file.write(text + '\n')
                log_file.flush()
            else:
                print('[SEND]', payload)
        elif mtype == 'error':
            print('[JS ERROR]', message.get('description'))
            print(message.get('stack', ''))

    script.on('message', on_message)
    script.load()

    print("=" * 60)
    print("抓取 初始化按钮4 + CreateProcessA 参数")
    print("=" * 60)
    print(f"[*] AAA.exe PID = {AAA_PID}")
    print(f"[*] yysls.exe PID = {YYSLS_PID}")
    print(f"[*] 日志文件: {LOG_PATH}")
    print()

    phases = [
        ('PRE_CLICK',   '不点击 - 记录基线 (10秒)', 10),
        ('INIT_BTN_4',  '点击初始化按钮4 (60秒)', 60),
    ]

    try:
        for i, (marker, desc, duration) in enumerate(phases):
            print("\n" + "=" * 60)
            print(f"阶段 {i+1}/{len(phases)}: {desc}")
            print("=" * 60)
            if marker == 'INIT_BTN_4':
                print(">>> 现在请在 AAA.exe 中点击 [初始化按钮4] <<<")
            print()

            script.exports_sync.set_marker(marker)

            for remaining in range(duration, 0, -5):
                print(f"  剩余 {remaining} 秒...", flush=True)
                time.sleep(5)

            script.exports_sync.clear_marker()
            print(f"[*] 阶段 {i+1} 完成")

            if i < len(phases) - 1:
                print("[*] 3 秒后进入下一阶段...")
                time.sleep(3)

    except KeyboardInterrupt:
        print("\n[*] 用户中断")
    finally:
        script.exports_sync.clear_marker()
        stats = script.exports_sync.get_stats()
        print("\n[*] 最终统计:", stats)
        try:
            session.detach()
        except Exception:
            pass
        log_file.close()
        print(f"[*] 日志已保存到: {LOG_PATH}")


if __name__ == '__main__':
    main()
