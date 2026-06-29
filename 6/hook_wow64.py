"""
Frida hook for AAA.exe (PID 13904) to capture NtWow64ReadVirtualMemory64 /
NtWow64WriteVirtualMemory64 calls — these are the actual APIs a 32-bit WOW64
 process uses to read/write 64-bit yysls.exe memory.

Run with: python hook_wow64.py
"""
import frida
import sys
import time
import os

AAA_PID = 13904
YYSLS_PID = 11072
LOG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'hook_wow64_log.txt')

JS_SRC = r"""
'use strict';

// ----- helpers (Frida 17.x API) -----
function u64FromArgs(args, idx) {
    // 32-bit stdcall: 64-bit args occupy 2 stack slots (low, high)
    var lo = args[idx].toInt32() >>> 0;
    var hi = args[idx + 1].toInt32() >>> 0;
    return { lo: lo, hi: hi, str: '0x' + hi.toString(16).padStart(8, '0') + lo.toString(16).padStart(8, '0') };
}

function readBytesSafe(ptr, n) {
    try { return ptr.readByteArray(n); } catch (e) { return null; }
}

var stats = {
    rpm: 0, wpm: 0,
    ntR64: 0, ntW64: 0,
    ntQIP64: 0,
    ntOpen: 0,
};

var yyslsHandle = null;       // resolved handle of yysls (via NtOpenProcess)
var yyslsPid = 11072;

// Buffer for logging
var logLines = [];
function pushLog(s) {
    logLines.push(s);
    if (logLines.length > 200) { flushLog(); }
}
function flushLog() {
    if (logLines.length === 0) return;
    send({ type: 'log', lines: logLines.join('\n') });
    logLines = [];
}

// ----- Hook NtOpenProcess to capture yysls handle -----
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var ntOpAddr;
    try { ntOpAddr = nt.getExportByName('NtOpenProcess'); } catch (e) { ntOpAddr = nt.getExportByName('ZwOpenProcess'); }
    Interceptor.attach(ntOpAddr, {
        onEnter: function (args) {
            // NtOpenProcess(Handle*, DesiredAccess, ObjectAttrs, ClientId)
            // ClientId.UniqueProcess = *(uint32*)(ClientId + 0)
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
                    yyslsHandle = h;
                    pushLog('[NtOpenProcess] yysls PID=' + this.pid + ' handle=0x' + h.toString(16));
                } catch (e) {}
            }
        }
    });
})();

// ----- Hook NtWow64ReadVirtualMemory64 -----
// NTSTATUS NtWow64ReadVirtualMemory64(HANDLE, ULONGLONG BaseAddr, PVOID Buffer,
//                                     ULONGLONG Size, PULONGLONG NbRead);
// x86 stdcall: args[0]=H, args[1]=BaseAddr.lo, args[2]=BaseAddr.hi,
//              args[3]=Buffer, args[4]=Size.lo, args[5]=Size.hi, args[6]=NbRead*
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64ReadVirtualMemory64'); }
    catch (e) {
        pushLog('[!] NtWow64ReadVirtualMemory64 not found in ntdll');
        return;
    }
    pushLog('[*] Hooking NtWow64ReadVirtualMemory64 @ ' + addr);
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntR64++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.nbRead = args[6];
            this.isYysls = (yyslsHandle !== null && this.handle === yyslsHandle);
        },
        onLeave: function (retval) {
            var status = retval.toInt32() >>> 0;
            var nbRead = 0;
            try {
                // 64-bit NumberOfBytesRead stored as 8 bytes
                var lo = this.nbRead.readU32();
                var hi = this.nbRead.add(4).readU32();
                nbRead = (hi * 0x100000000) + lo;
            } catch (e) {}
            // Only log small reads (likely coordinate / pointer reads) or yysls reads
            var szLo = this.size.lo;
            var interesting = this.isYysls || szLo <= 64;
            if (!interesting) return;

            var tag = this.isYysls ? '[R64 yysls]' : '[R64 other]';
            var msg = tag + ' H=0x' + this.handle.toString(16) +
                      ' Base=' + this.base.str +
                      ' Size=0x' + this.size.lo.toString(16) +
                      ' status=0x' + status.toString(16) +
                      ' nbRead=' + nbRead;
            // Show buffer content for small reads
            if (nbRead > 0 && nbRead <= 64) {
                try {
                    var bytes = this.buf.readByteArray(nbRead);
                    msg += ' data=' + hexify(bytes);
                } catch (e) {}
            }
            pushLog(msg);
        }
    });
})();

// ----- Hook NtWow64WriteVirtualMemory64 -----
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64WriteVirtualMemory64'); }
    catch (e) {
        pushLog('[!] NtWow64WriteVirtualMemory64 not found in ntdll');
        return;
    }
    pushLog('[*] Hooking NtWow64WriteVirtualMemory64 @ ' + addr);
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntW64++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.isYysls = (yyslsHandle !== null && this.handle === yyslsHandle);
            // Capture buffer content for small writes (before syscall)
            if (this.isYysls || this.size.lo <= 64) {
                try {
                    var n = Math.min(this.size.lo, 64);
                    this.dataHex = hexify(this.buf.readByteArray(n));
                } catch (e) { this.dataHex = '?'; }
            }
        },
        onLeave: function (retval) {
            var status = retval.toInt32() >>> 0;
            var interesting = this.isYysls || this.size.lo <= 64;
            if (!interesting) return;
            var tag = this.isYysls ? '[W64 yysls]' : '[W64 other]';
            pushLog(tag + ' H=0x' + this.handle.toString(16) +
                    ' Base=' + this.base.str +
                    ' Size=0x' + this.size.lo.toString(16) +
                    ' status=0x' + status.toString(16) +
                    ' data=' + (this.dataHex || ''));
        }
    });
})();

// ----- Hook NtWow64QueryInformationProcess64 -----
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64QueryInformationProcess64'); }
    catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntQIP64++;
        }
    });
})();

// ----- also hook kernel32!ReadProcessMemory for comparison -----
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('ReadProcessMemory'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.rpm++;
        }
    });
})();

function hexify(arrbuf) {
    var b = new Uint8Array(arrbuf);
    var s = '';
    for (var i = 0; i < b.length; i++) {
        s += b[i].toString(16).padStart(2, '0');
        if ((i & 3) === 3 && i !== b.length - 1) s += ' ';
    }
    return s;
}

// ----- periodic stats -----
setInterval(function () {
    pushLog('=== Stats: rpm=' + stats.rpm +
            ' ntR64=' + stats.ntR64 +
            ' ntW64=' + stats.ntW64 +
            ' ntQIP64=' + stats.ntQIP64 +
            ' ntOpen=' + stats.ntOpen +
            ' yyslsHandle=' + (yyslsHandle !== null ? '0x' + yyslsHandle.toString(16) : 'null') +
            ' ===');
    flushLog();
}, 5000);

pushLog('[*] Hooks installed on PID ' + Process.id + ' arch=' + Process.arch);
flushLog();
"""


def main():
    try:
        session = frida.attach(AAA_PID)
    except Exception as e:
        print(f"[!] Failed to attach to PID {AAA_PID}: {e}")
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

    print(f"[*] Hooking AAA.exe (PID {AAA_PID}) for NtWow64* APIs")
    print(f"[*] Log file: {LOG_PATH}")
    print(f"[*] Press Ctrl+C to stop")

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[*] Stopping...")
    finally:
        try:
            session.detach()
        except Exception:
            pass
        log_file.close()


if __name__ == '__main__':
    main()
