"""
AAA.exe 全功能行为提取脚本
==========================
全面 Hook AAA.exe 的所有外部行为，提取每个功能（传送/加速/无敌等）的完整操作链：
  - 内存读写 (NtWow64*VirtualMemory64) — 已知 AAA 用此读写 yysls 内存
  - 按键模拟 (SendInput / keybd_event / PostMessage) — 找出传送防拉回的真实按键序列
  - 鼠标模拟 (mouse_event / SendInput mouse)
  - 窗口操作 (FindWindow / SetForegroundWindow)
  - 进程/线程 (CreateProcess / CreateThread)
  - 键盘钩子 (SetWindowsHookEx)

交互方式：
  1. 启动后自动 attach 到 AAA.exe（或指定 PID）
  2. 在终端输入标记（如 "传送1" "加速" "无敌"）后回车，标记会写入日志
  3. 在游戏中触发对应功能
  4. Ctrl+C 停止，日志保存到 hook_aaa_all_log.txt

用法:
  python hook_aaa_all.py              # 自动查找 AAA.exe
  python hook_aaa_all.py <PID>        # 指定 AAA.exe PID
  python hook_aaa_all.py <PID> <yysls_PID>
"""
import frida
import sys
import time
import os
import threading

# ===== 参数解析 =====
AAA_PID = 0
YYSLS_PID = 0
if len(sys.argv) >= 2:
    AAA_PID = int(sys.argv[1])
if len(sys.argv) >= 3:
    YYSLS_PID = int(sys.argv[2])

LOG_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'hook_aaa_all_log.txt')

# 自动查找 AAA.exe
if AAA_PID == 0:
    import subprocess
    try:
        out = subprocess.check_output(['tasklist', '/FI', 'IMAGENAME eq AAA.exe', '/FO', 'CSV', '/NH'],
                                      shell=True, encoding='gbk', errors='replace')
        for line in out.splitlines():
            if 'AAA.exe' in line:
                parts = line.strip('"').split('","')
                if len(parts) >= 2:
                    AAA_PID = int(parts[1])
                    break
    except Exception as e:
        print(f"[!] 查找 AAA.exe 失败: {e}")

if AAA_PID == 0:
    print("[!] 未找到 AAA.exe 进程，请手动指定 PID: python hook_aaa_all.py <AAA_PID>")
    sys.exit(1)

# 自动查找 yysls.exe（用于识别内存操作目标）
if YYSLS_PID == 0:
    try:
        out = subprocess.check_output(['tasklist', '/FI', 'IMAGENAME eq yysls.exe', '/FO', 'CSV', '/NH'],
                                      shell=True, encoding='gbk', errors='replace')
        for line in out.splitlines():
            if 'yysls.exe' in line:
                parts = line.strip('"').split('","')
                if len(parts) >= 2:
                    YYSLS_PID = int(parts[1])
                    break
    except Exception:
        pass

print(f"[*] AAA.exe PID = {AAA_PID}")
print(f"[*] yysls.exe PID = {YYSLS_PID if YYSLS_PID else '(未检测到)'}")
print(f"[*] 日志文件: {LOG_PATH}")
print()
print("=" * 60)
print("操作流程:")
print("  1. 在游戏中准备好要测试的功能（如站到传送点附近）")
print("  2. 回到此终端，输入功能名称（如 '传送1'）后回车 → 写入事件标记")
print("  3. 立即在 AAA.exe 界面点击对应按钮触发功能")
print("  4. 等待功能生效（约2-3秒），再输入下一个标记")
print("  5. Ctrl+C 停止，查看日志分析每个功能的完整操作链")
print("=" * 60)
print()

JS_SRC = r"""
'use strict';

// ===== 辅助函数 =====
function u64FromArgs(args, idx) {
    var lo = args[idx].toInt32() >>> 0;
    var hi = args[idx + 1].toInt32() >>> 0;
    return { lo: lo, hi: hi, str: '0x' + hi.toString(16).padStart(8, '0') + lo.toString(16).padStart(8, '0') };
}

function hexify(arrbuf) {
    if (!arrbuf) return '';
    var b = new Uint8Array(arrbuf);
    var s = '';
    for (var i = 0; i < b.length; i++) {
        s += b[i].toString(16).padStart(2, '0');
        if ((i & 3) === 3 && i !== b.length - 1) s += ' ';
    }
    return s;
}

// 尝试以多种格式解码字节（Double/Float/uint32/uint64）
function decodeBytes(arrbuf, n) {
    if (!arrbuf || n < 4) return '';
    var b = new Uint8Array(arrbuf);
    var parts = [];
    // Double (8字节)
    if (n >= 8) {
        try {
            var buf = new ArrayBuffer(8);
            var view = new DataView(buf);
            for (var i = 0; i < 8; i++) view.setUint8(i, b[i]);
            var d = view.getFloat64(0, true);
            if (d !== 0 && Math.abs(d) < 1e9 && !isNaN(d)) parts.push('dbl=' + d.toFixed(3));
        } catch (e) {}
    }
    // Float (4字节)
    if (n >= 4) {
        try {
            var buf4 = new ArrayBuffer(4);
            var view4 = new DataView(buf4);
            for (var i = 0; i < 4; i++) view4.setUint8(i, b[i]);
            var f = view4.getFloat32(0, true);
            if (f !== 0 && Math.abs(f) < 1e9 && !isNaN(f)) parts.push('flt=' + f.toFixed(3));
        } catch (e) {}
    }
    // uint32
    try {
        var u32 = b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24);
        u32 = u32 >>> 0;
        if (u32 > 0 && u32 < 0xFFFFFFFF) parts.push('u32=' + u32 + '(0x' + u32.toString(16) + ')');
    } catch (e) {}
    return parts.length ? ' [' + parts.join(' ') + ']' : '';
}

// 获取调用栈（精简，只取前5帧）
function backtraceStr(ctx) {
    try {
        var bt = Thread.backtrace(ctx, Backtracer.ACCURATE).slice(0, 5);
        var lines = [];
        for (var i = 0; i < bt.length; i++) {
            var sym = DebugSymbol.fromAddress(bt[i]);
            if (sym.name) lines.push(sym.name + '@0x' + bt[i].toString(16));
            else lines.push('0x' + bt[i].toString(16));
        }
        return bt.length ? ' bt=[' + lines.join(', ') + ']' : '';
    } catch (e) { return ''; }
}

// 虚拟键码 → 名称（常见按键）
var VK_NAMES = {
    0x01: 'LBUTTON', 0x02: 'RBUTTON', 0x04: 'MBUTTON',
    0x08: 'BACK', 0x09: 'TAB', 0x0D: 'RETURN', 0x10: 'SHIFT', 0x11: 'CTRL',
    0x12: 'ALT', 0x14: 'CAPITAL', 0x1B: 'ESCAPE', 0x20: 'SPACE',
    0x21: 'PRIOR', 0x22: 'NEXT', 0x23: 'END', 0x24: 'HOME',
    0x25: 'LEFT', 0x26: 'UP', 0x27: 'RIGHT', 0x28: 'DOWN',
    0x2D: 'INSERT', 0x2E: 'DELETE',
    0x30: '0', 0x31: '1', 0x32: '2', 0x33: '3', 0x34: '4',
    0x35: '5', 0x36: '6', 0x37: '7', 0x38: '8', 0x39: '9',
    0x41: 'A', 0x42: 'B', 0x43: 'C', 0x44: 'D', 0x45: 'E', 0x46: 'F',
    0x47: 'G', 0x48: 'H', 0x49: 'I', 0x4A: 'J', 0x4B: 'K', 0x4C: 'L',
    0x4D: 'M', 0x4E: 'N', 0x4F: 'O', 0x50: 'P', 0x51: 'Q', 0x52: 'R',
    0x53: 'S', 0x54: 'T', 0x55: 'U', 0x56: 'V', 0x57: 'W', 0x58: 'X',
    0x59: 'Y', 0x5A: 'Z',
    0x60: 'NUMPAD0', 0x61: 'NUMPAD1', 0x62: 'NUMPAD2', 0x63: 'NUMPAD3',
    0x64: 'NUMPAD4', 0x65: 'NUMPAD5', 0x66: 'NUMPAD6', 0x67: 'NUMPAD7',
    0x68: 'NUMPAD8', 0x69: 'NUMPAD9',
    0x70: 'F1', 0x71: 'F2', 0x72: 'F3', 0x73: 'F4', 0x74: 'F5',
    0x75: 'F6', 0x76: 'F7', 0x77: 'F8', 0x78: 'F9', 0x79: 'F10',
    0x7A: 'F11', 0x7B: 'F12',
    0xA0: 'LSHIFT', 0xA1: 'RSHIFT', 0xA2: 'LCTRL', 0xA3: 'RCTRL',
    0xA4: 'LALT', 0xA5: 'RALT',
};
function vkName(vk) {
    return VK_NAMES[vk] || ('VK_0x' + vk.toString(16));
}

// ===== 统计 =====
var stats = { ntOpen: 0, ntR64: 0, ntW64: 0, ntW: 0, sendInput: 0, keybd: 0, mouseEv: 0, postMsg: 0, sendMsg: 0 };
var yyslsHandle = null;
var yyslsPid = """ + str(YYSLS_PID) + r""";
// 句柄→PID 缓存, 用 NtQueryInformationProcess 反查 (不依赖 NtOpenProcess 捕获)
var handlePidCache = {};
var ntQueryInfoProcess = null;
try { ntQueryInfoProcess = Process.getModuleByName('ntdll.dll').getExportByName('NtQueryInformationProcess'); } catch (e) {}

// 通过 NtQueryInformationProcess(ProcessBasicInformation) 反查句柄对应 PID
function pidFromHandle(h) {
    var hVal = (typeof h === 'number') ? h : (h.toInt32 ? (h.toInt32() >>> 0) : 0);
    if (handlePidCache[hVal] !== undefined) return handlePidCache[hVal];
    if (!ntQueryInfoProcess) return 0;
    var pid = 0;
    try {
        // PROCESS_BASIC_INFORMATION 64位: UniqueProcessId 偏移 0x20, 48字节
        var buf = Memory.alloc(48);
        var retLen = Memory.alloc(8);
        var status = ntQueryInfoProcess(ptr(hVal), 0, buf, 48, retLen);
        if (status.toInt32() === 0) {
            pid = buf.add(0x20).readU64().toNumber() >>> 0;
        }
    } catch (e) {}
    handlePidCache[hVal] = pid;
    return pid;
}

// 判断句柄是否指向 yysls.exe
function isYyslsHandle(h) {
    if (h === null || h === undefined) return false;
    var hVal = (typeof h === 'number') ? h : (h.toInt32 ? (h.toInt32() >>> 0) : 0);
    if (hVal === 0) return false;
    if (yyslsHandle !== null && hVal === yyslsHandle) return true;
    var pid = pidFromHandle(hVal);
    if (pid === yyslsPid) {
        yyslsHandle = hVal;  // 缓存识别到的 yysls 句柄
        return true;
    }
    return false;
}

// ===== 日志缓冲 =====
var logLines = [];
function pushLog(s) {
    logLines.push(s);
    if (logLines.length > 100) flushLog();
}
function flushLog() {
    if (logLines.length === 0) return;
    send({ type: 'log', lines: logLines.join('\n') });
    logLines = [];
}

// 当前事件标记（由 Python 端通过 setEvent 注入）
var currentEvent = '';
rpc.exports = {
    setEvent: function (name) {
        currentEvent = name;
        pushLog('\n========================================');
        pushLog('=== 事件标记: ' + name + ' === ts=' + Date.now());
        pushLog('========================================');
        return true;
    }
};

// ===== Hook: NtOpenProcess (识别 yysls 句柄) =====
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtOpenProcess'); } catch (e) { addr = nt.getExportByName('ZwOpenProcess'); }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            try {
                var cid = args[3];
                if (cid.isNull()) return;
                this.pid = cid.readU32();
                this.phandle = args[0];
            } catch (e) {}
        },
        onLeave: function (retval) {
            stats.ntOpen++;
            if (this.pid && this.pid === yyslsPid) {
                try {
                    var h = this.phandle.readU32();
                    yyslsHandle = h;
                    pushLog('[*] NtOpenProcess 捕获 yysls 句柄=0x' + h.toString(16));
                } catch (e) {}
            }
        }
    });
})();

// ===== Hook: NtWow64ReadVirtualMemory64 (内存读) =====
// 改进: 用 isYyslsHandle 识别, 不再依赖单一 yyslsHandle 变量
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64ReadVirtualMemory64'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntR64++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.nbRead = args[6];
            this.isYysls = isYyslsHandle(this.handle);
        },
        onLeave: function (retval) {
            var nbRead = 0;
            try {
                var lo = this.nbRead.readU32();
                var hi = this.nbRead.add(4).readU32();
                nbRead = (hi * 0x100000000) + lo;
            } catch (e) {}
            // 只记录小读取（坐标/指针）或 yysls 读取
            var interesting = this.isYysls || this.size.lo <= 64;
            if (!interesting) return;
            var tag = this.isYysls ? '[R yysls]' : '[R]';
            var msg = tag + ' Base=' + this.base.str + ' Sz=0x' + this.size.lo.toString(16) + ' nb=' + nbRead;
            if (nbRead > 0 && nbRead <= 64) {
                try {
                    var bytes = this.buf.readByteArray(nbRead);
                    msg += ' data=' + hexify(bytes) + decodeBytes(bytes, nbRead);
                } catch (e) {}
            }
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: NtWow64WriteVirtualMemory64 (内存写 — 关键!) =====
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWow64WriteVirtualMemory64'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntW64++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = u64FromArgs(args, 1);
            this.buf = args[3];
            this.size = u64FromArgs(args, 4);
            this.isYysls = isYyslsHandle(this.handle);
            this.ctx = this.context;
            if (this.isYysls || this.size.lo <= 64) {
                try {
                    var n = Math.min(this.size.lo, 64);
                    this.dataHex = hexify(this.buf.readByteArray(n));
                    this.dataDecoded = decodeBytes(this.buf.readByteArray(n), n);
                } catch (e) { this.dataHex = '?'; this.dataDecoded = ''; }
            }
        },
        onLeave: function (retval) {
            var status = retval.toInt32() >>> 0;
            var interesting = this.isYysls || this.size.lo <= 64;
            if (!interesting) return;
            var tag = this.isYysls ? '[W yysls]' : '[W]';
            var msg = tag + ' Base=' + this.base.str + ' Sz=0x' + this.size.lo.toString(16) +
                      ' st=0x' + status.toString(16) +
                      ' data=' + (this.dataHex || '') + (this.dataDecoded || '');
            // 写入操作记录调用栈，定位 AAA 内部触发点
            msg += backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: NtWriteVirtualMemory (非 Wow64 版, 微调传送可能用此) =====
// NTSTATUS NtWriteVirtualMemory(HANDLE, PVOID, PVOID, SIZE_T, PSIZE_T);
(function () {
    var nt = Process.getModuleByName('ntdll.dll');
    var addr;
    try { addr = nt.getExportByName('NtWriteVirtualMemory'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.ntW++;
            this.handle = args[0].toInt32() >>> 0;
            this.base = args[1];
            this.buf = args[2];
            this.size = args[3].toInt32() >>> 0;
            this.isYysls = isYyslsHandle(this.handle);
            this.ctx = this.context;
            if (this.isYysls || this.size <= 64) {
                try {
                    var n = Math.min(this.size, 64);
                    this.dataHex = hexify(this.buf.readByteArray(n));
                    this.dataDecoded = decodeBytes(this.buf.readByteArray(n), n);
                } catch (e) { this.dataHex = '?'; this.dataDecoded = ''; }
            }
        },
        onLeave: function (retval) {
            var status = retval.toInt32() >>> 0;
            var interesting = this.isYysls || this.size <= 64;
            if (!interesting) return;
            var tag = this.isYysls ? '[WV yysls]' : '[WV]';
            var msg = tag + ' Base=0x' + this.base.toString(16) + ' Sz=0x' + this.size.toString(16) +
                      ' st=0x' + status.toString(16) +
                      ' data=' + (this.dataHex || '') + (this.dataDecoded || '');
            msg += backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: WriteProcessMemory (kernel32 包装层, 也可能被调用) =====
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    var addr;
    try { addr = k32.getExportByName('WriteProcessMemory'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            this.handle = args[0].toInt32() >>> 0;
            this.base = args[1];
            this.buf = args[2];
            this.size = args[3].toInt32() >>> 0;
            this.isYysls = isYyslsHandle(this.handle);
            this.ctx = this.context;
            this.interesting = this.isYysls || this.size <= 64;
            if (this.interesting) {
                try {
                    var n = Math.min(this.size, 64);
                    this.dataHex = hexify(this.buf.readByteArray(n));
                    this.dataDecoded = decodeBytes(this.buf.readByteArray(n), n);
                } catch (e) { this.dataHex = '?'; this.dataDecoded = ''; }
            }
        },
        onLeave: function (retval) {
            if (!this.interesting) return;
            var tag = this.isYysls ? '[WPM yysls]' : '[WPM]';
            var msg = tag + ' Base=0x' + this.base.toString(16) + ' Sz=0x' + this.size.toString(16) +
                      ' ret=' + retval +
                      ' data=' + (this.dataHex || '') + (this.dataDecoded || '');
            msg += backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: SendInput (键盘/鼠标模拟 — 找传送防拉回按键!) =====
// UINT SendInput(UINT cInputs, LPINPUT pInputs, int cbSize);
// INPUT.type: 0=mouse, 1=keyboard, 2=hardware
// KEYBDINPUT: wVk(2), wScan(2), dwFlags(4), time(4), dwExtraInfo(4) — 偏移 4
(function () {
    var u32 = Process.getModuleByName('user32.dll');
    var addr;
    try { addr = u32.getExportByName('SendInput'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.sendInput++;
            var cInputs = args[0].toInt32() >>> 0;
            var pInputs = args[1];
            var cbSize = args[2].toInt32();
            this.ctx = this.context;
            var descs = [];
            for (var i = 0; i < cInputs && i < 10; i++) {
                try {
                    var p = pInputs.add(i * cbSize);
                    var type = p.readU32();
                    if (type === 1) {
                        // keyboard: wVk@+4, wScan@+6, dwFlags@+8
                        var wVk = p.add(4).readU16();
                        var wScan = p.add(6).readU16();
                        var dwFlags = p.add(8).readU32();
                        var isUp = (dwFlags & 2) !== 0;
                        descs.push('KEY{' + vkName(wVk) + (isUp ? '↑' : '↓') + '}');
                    } else if (type === 0) {
                        // mouse: dwFlags@+4, dx@+8, dy@+12
                        var mFlags = p.add(4).readU32();
                        var dx = p.add(8).readU32();
                        var dy = p.add(12).readU32();
                        descs.push('MOUSE{fl=0x' + mFlags.toString(16) + ' dx=' + dx + ' dy=' + dy + '}');
                    } else {
                        descs.push('HW{type=' + type + '}');
                    }
                } catch (e) { descs.push('ERR'); }
            }
            this.descs = descs;
        },
        onLeave: function (retval) {
            var msg = '[SendInput] n=' + stats.sendInput + ' ret=' + retval + ' ops=[' + this.descs.join(', ') + ']';
            msg += backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: keybd_event (旧式按键模拟) =====
// void keybd_event(BYTE bVk, BYTE bScan, DWORD dwFlags, ULONG_PTR dwExtraInfo);
(function () {
    var u32 = Process.getModuleByName('user32.dll');
    var addr;
    try { addr = u32.getExportByName('keybd_event'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.keybd++;
            var bVk = args[0].toInt32() & 0xFF;
            var bScan = args[1].toInt32() & 0xFF;
            var dwFlags = args[2].toInt32() >>> 0;
            var isUp = (dwFlags & 2) !== 0;
            this.ctx = this.context;
            this.desc = 'KEY{' + vkName(bVk) + (isUp ? '↑' : '↓') + ' scan=0x' + bScan.toString(16) + ' fl=0x' + dwFlags.toString(16) + '}';
        },
        onLeave: function (retval) {
            var msg = '[keybd_event] ' + this.desc + backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: mouse_event (旧式鼠标模拟) =====
(function () {
    var u32 = Process.getModuleByName('user32.dll');
    var addr;
    try { addr = u32.getExportByName('mouse_event'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            stats.mouseEv++;
            var dwFlags = args[0].toInt32() >>> 0;
            var dx = args[1].toInt32() >>> 0;
            var dy = args[2].toInt32() >>> 0;
            this.ctx = this.context;
            this.desc = 'MOUSE{fl=0x' + dwFlags.toString(16) + ' dx=' + dx + ' dy=' + dy + '}';
        },
        onLeave: function (retval) {
            var msg = '[mouse_event] ' + this.desc + backtraceStr(this.ctx);
            if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
            pushLog(msg);
        }
    });
})();

// ===== Hook: PostMessageW / PostMessageA (直接给窗口发消息) =====
// BOOL PostMessageW(HWND hWnd, UINT Msg, WPARAM wParam, LPARAM lParam);
(function () {
    var u32 = Process.getModuleByName('user32.dll');
    ['PostMessageW', 'PostMessageA'].forEach(function (fname) {
        var addr;
        try { addr = u32.getExportByName(fname); } catch (e) { return; }
        Interceptor.attach(addr, {
            onEnter: function (args) {
                stats.postMsg++;
                var hWnd = args[0].toInt32() >>> 0;
                var Msg = args[1].toInt32() >>> 0;
                var wParam = args[2].toInt32() >>> 0;
                var lParam = args[3].toInt32() >>> 0;
                // 只记录键盘/鼠标相关消息
                if (Msg >= 0x100 && Msg <= 0x109) {
                    // WM_KEYDOWN/UP/SYSKEYDOWN/UP/CHAR/DEADCHAR/SYSCHAR/DEADCHAR
                    var vk = wParam & 0xFF;
                    var isUp = (Msg === 0x101 || Msg === 0x105);
                    this.desc = fname + '{KEY ' + vkName(vk) + (isUp ? '↑' : '↓') + ' hWnd=0x' + hWnd.toString(16) + '}';
                    this.ctx = this.context;
                    this.interesting = true;
                } else if (Msg >= 0x200 && Msg <= 0x209) {
                    // 鼠标消息
                    this.desc = fname + '{MOUSE Msg=0x' + Msg.toString(16) + ' wParam=0x' + wParam.toString(16) + ' hWnd=0x' + hWnd.toString(16) + '}';
                    this.ctx = this.context;
                    this.interesting = true;
                } else {
                    this.interesting = false;
                }
            },
            onLeave: function (retval) {
                if (!this.interesting) return;
                var msg = '[' + this.desc + '] ret=' + retval + backtraceStr(this.ctx);
                if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
                pushLog(msg);
            }
        });
    });
})();

// ===== Hook: CreateProcessA / CreateProcessW (初始化按钮已知用此) =====
(function () {
    var k32 = Process.getModuleByName('kernel32.dll');
    ['CreateProcessA', 'CreateProcessW'].forEach(function (fname) {
        var addr;
        try { addr = k32.getExportByName(fname); } catch (e) { return; }
        Interceptor.attach(addr, {
            onEnter: function (args) {
                this.ctx = this.context;
                var cmdLine;
                try {
                    if (fname === 'CreateProcessA') cmdLine = args[1].readAnsiString();
                    else cmdLine = args[1].readUtf16String();
                } catch (e) { cmdLine = '(null)'; }
                this.cmdLine = cmdLine;
            },
            onLeave: function (retval) {
                var msg = '[' + fname + '] cmd="' + this.cmdLine + '" ret=' + retval + backtraceStr(this.ctx);
                if (currentEvent) msg = '({' + currentEvent + '}) ' + msg;
                pushLog(msg);
            }
        });
    });
})();

// ===== Hook: SetWindowsHookExW (键盘钩子 — 可能用于全局按键监听) =====
(function () {
    var u32 = Process.getModuleByName('user32.dll');
    var addr;
    try { addr = u32.getExportByName('SetWindowsHookExW'); } catch (e) { return; }
    Interceptor.attach(addr, {
        onEnter: function (args) {
            var idHook = args[0].toInt32();
            // WH_KEYBOARD=2, WH_KEYBOARD_LL=13, WH_MOUSE=7, WH_MOUSE_LL=14
            var hookNames = {2:'WH_KEYBOARD', 13:'WH_KEYBOARD_LL', 7:'WH_MOUSE', 14:'WH_MOUSE_LL'};
            var name = hookNames[idHook] || ('idHook=' + idHook);
            pushLog('[SetWindowsHookExW] ' + name + ' proc=' + args[1] + ' hMod=' + args[2] + ' tid=' + args[3]);
        }
    });
})();

// ===== 定期输出统计 =====
setInterval(function () {
    pushLog('--- Stats: ntOpen=' + stats.ntOpen + ' ntR=' + stats.ntR64 + ' ntW64=' + stats.ntW64 + ' ntW=' + stats.ntW +
            ' sendInput=' + stats.sendInput + ' keybd=' + stats.keybd + ' mouse=' + stats.mouseEv +
            ' postMsg=' + stats.postMsg + ' yyslsH=' + (yyslsHandle !== null ? '0x' + yyslsHandle.toString(16) : 'null') +
            ' ---');
    flushLog();
}, 3000);

pushLog('[*] 全功能 Hook 已安装 PID=' + Process.id + ' arch=' + Process.arch);
pushLog('[*] 监控: NtWow64R/W, SendInput, keybd_event, mouse_event, PostMessage, CreateProcess, SetWindowsHookEx');
flushLog();
"""


def main():
    try:
        session = frida.attach(AAA_PID)
    except Exception as e:
        print(f"[!] attach PID {AAA_PID} 失败: {e}")
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

    print(f"[*] Hook 已注入 AAA.exe (PID {AAA_PID})")
    print(f"[*] 日志: {LOG_PATH}")
    print(f"[*] 运行 120 秒后自动停止")
    print(f"[*] 请现在开始操作：导入坐标 → 点击传送")
    print()

    # 运行 120 秒后自动停止（无交互，纯后台收集）
    DURATION = 120
    try:
        for i in range(DURATION):
            time.sleep(1)
            remaining = DURATION - i - 1
            if remaining > 0 and remaining % 30 == 0:
                print(f"[*] 剩余 {remaining} 秒...")
        print(f"\n[*] {DURATION} 秒已到，自动停止 Hook...")
    except KeyboardInterrupt:
        print("\n[*] 手动停止 Hook...")
    finally:
        try:
            session.detach()
        except Exception:
            pass
        log_file.close()
        print(f"[*] 日志已保存: {LOG_PATH}")


if __name__ == '__main__':
    main()
