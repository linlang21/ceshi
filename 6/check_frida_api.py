"""Diagnose Frida 17.x API availability"""
import frida
import sys
import time

AAA_PID = 13904

JS = r"""
console.log('=== Frida API check ===');
console.log('Process.id = ' + Process.id);
console.log('Process.arch = ' + Process.arch);

console.log('typeof Module = ' + typeof Module);
console.log('typeof Module.findExportByName = ' + typeof Module.findExportByName);
console.log('typeof Module.getExportByName = ' + typeof Module.getExportByName);
console.log('typeof Process.findModuleByName = ' + typeof Process.findModuleByName);
console.log('typeof Process.getModuleByName = ' + typeof Process.getModuleByName);

// List kernel32 exports
try {
    var k32 = Process.findModuleByName('kernel32.dll') || Process.getModuleByName('kernel32.dll');
    console.log('kernel32 base = ' + k32.base + ' size=' + k32.size);
    console.log('k32.findExportByName = ' + typeof k32.findExportByName);
    console.log('k32.getExportByName = ' + typeof k32.getExportByName);

    var rpm = null;
    if (typeof k32.findExportByName === 'function') {
        rpm = k32.findExportByName('ReadProcessMemory');
    } else if (typeof k32.getExportByName === 'function') {
        rpm = k32.getExportByName('ReadProcessMemory');
    }
    console.log('ReadProcessMemory = ' + rpm);

    var wpm = null;
    if (typeof k32.findExportByName === 'function') {
        wpm = k32.findExportByName('WriteProcessMemory');
    } else if (typeof k32.getExportByName === 'function') {
        wpm = k32.getExportByName('WriteProcessMemory');
    }
    console.log('WriteProcessMemory = ' + wpm);

    var op = null;
    if (typeof k32.findExportByName === 'function') {
        op = k32.findExportByName('OpenProcess');
    } else if (typeof k32.getExportByName === 'function') {
        op = k32.getExportByName('OpenProcess');
    }
    console.log('OpenProcess = ' + op);
} catch (e) {
    console.log('Error: ' + e.message);
    console.log('Stack: ' + e.stack);
}

// Check static Module API
try {
    var rpm2 = Module.findExportByName('kernel32.dll', 'ReadProcessMemory');
    console.log('Module.findExportByName static = ' + rpm2);
} catch (e) {
    console.log('Module.findExportByName static error: ' + e.message);
}

try {
    var rpm3 = Module.getExportByName('kernel32.dll', 'ReadProcessMemory');
    console.log('Module.getExportByName static = ' + rpm3);
} catch (e) {
    console.log('Module.getExportByName static error: ' + e.message);
}

console.log('typeof Interceptor = ' + typeof Interceptor);
console.log('typeof Interceptor.attach = ' + typeof Interceptor.attach);
console.log('typeof Memory = ' + typeof Memory);
console.log('typeof Memory.readByteArray = ' + typeof Memory.readByteArray);
console.log('typeof setInterval = ' + typeof setInterval);

console.log('=== Done ===');
"""

def on_msg(msg, data):
    if msg['type'] == 'send':
        print(msg['payload'], flush=True)
    elif msg['type'] == 'error':
        print(f"[ERROR] {msg.get('description', msg)}", flush=True)
        if 'stack' in msg:
            print(msg['stack'], flush=True)
    else:
        print(msg, flush=True)

print(f"[*] Frida version: {frida.__version__}")
print(f"[*] Attaching to PID {AAA_PID}...")
session = frida.attach(AAA_PID)
script = session.create_script(JS)
script.on('message', on_msg)
script.load()
time.sleep(2)
session.detach()
print("[*] Done")
