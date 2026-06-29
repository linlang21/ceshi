"""
yysls 传送工具 - 从 AAA.exe 提取的功能
======================================
通过逆向分析 AAA.exe (32位 WOW64) 的 NtWow64ReadVirtualMemory64 /
NtWow64WriteVirtualMemory64 调用，提取出完整的指针链和坐标偏移。

指针链:
  yysls.exe + 0x083F46D8  ->  P1
  P1 + 0x58               ->  P2
  P2 + 0x00               ->  OBJ (玩家/相机对象基址)
  OBJ + 0x340             ->  X (Double, 8 bytes)
  OBJ + 0x348             ->  Z (Double, 8 bytes)
  OBJ + 0x350             ->  Y (Double, 8 bytes)

需要 64 位 Python（直接用 ReadProcessMemory / WriteProcessMemory）。
"""
import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

# ========== Windows API ==========
PROCESS_VM_READ = 0x0010
PROCESS_VM_WRITE = 0x0020
PROCESS_VM_OPERATION = 0x0008
PROCESS_QUERY_INFORMATION = 0x0400
TOKEN_ADJUST_PRIVILEGES = 0x0020
TOKEN_QUERY = 0x0008
SE_PRIVILEGE_ENABLED = 0x00000002

kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
advapi32 = ctypes.WinDLL('advapi32', use_last_error=True)
ntdll = ctypes.WinDLL('ntdll', use_last_error=True)

# Function prototypes
kernel32.OpenProcess.restype = w.HANDLE
kernel32.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
kernel32.CloseHandle.argtypes = [w.HANDLE]
kernel32.ReadProcessMemory.restype = w.BOOL
kernel32.ReadProcessMemory.argtypes = [w.HANDLE, w.LPCVOID, w.LPVOID, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
kernel32.WriteProcessMemory.restype = w.BOOL
kernel32.WriteProcessMemory.argtypes = [w.HANDLE, w.LPVOID, w.LPCVOID, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
kernel32.GetProcessId.argtypes = [w.HANDLE]
kernel32.GetCurrentProcess.restype = w.HANDLE
kernel32.GetCurrentThread.restype = w.HANDLE
kernel32.OpenProcessToken.argtypes = [w.HANDLE, w.DWORD, ctypes.POINTER(w.HANDLE)]
kernel32.GetModuleHandleW.restype = w.HMODULE
kernel32.GetModuleHandleW.argtypes = [w.LPCWSTR]
kernel32.CreateToolhelp32Snapshot.restype = w.HANDLE
kernel32.CreateToolhelp32Snapshot.argtypes = [w.DWORD, w.DWORD]
kernel32.Process32FirstW.argtypes = [w.HANDLE, ctypes.c_void_p]
kernel32.Process32NextW.argtypes = [w.HANDLE, ctypes.c_void_p]
kernel32.Module32FirstW.argtypes = [w.HANDLE, ctypes.c_void_p]
kernel32.Module32NextW.argtypes = [w.HANDLE, ctypes.c_void_p]
kernel32.VirtualProtectEx.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t, w.DWORD, ctypes.POINTER(w.DWORD)]

advapi32.LookupPrivilegeValueA.argtypes = [w.LPCSTR, w.LPCSTR, ctypes.c_void_p]
advapi32.AdjustTokenPrivileges.argtypes = [w.HANDLE, w.BOOL, ctypes.c_void_p, w.DWORD, ctypes.c_void_p, ctypes.c_void_p]


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [
        ("dwSize", w.DWORD),
        ("cntUsage", w.DWORD),
        ("th32ProcessID", w.DWORD),
        ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)),
        ("th32ModuleID", w.DWORD),
        ("cntThreads", w.DWORD),
        ("th32ParentProcessID", w.DWORD),
        ("pcPriClassBase", ctypes.c_long),
        ("dwFlags", w.DWORD),
        ("szExeFile", w.WCHAR * 260),
    ]


class LUID(ctypes.Structure):
    _fields_ = [("LowPart", w.DWORD), ("HighPart", ctypes.c_long)]


class LUID_AND_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Luid", LUID), ("Attributes", w.DWORD)]


class TOKEN_PRIVILEGES(ctypes.Structure):
    _fields_ = [("PrivilegeCount", w.DWORD), ("Privileges", LUID_AND_ATTRIBUTES * 1)]


# ========== yysls constants ==========
YYSLS_PROCESS_NAME = "yysls.exe"
YYSLS_BASE = 0x140000000  # 64-bit module base
STATIC_OFFSET = 0x083F46D8
PTR_STEP1_OFFSET = 0x58
PTR_STEP2_OFFSET = 0x00
OFFSET_X = 0x340
OFFSET_Z = 0x348
OFFSET_Y = 0x350

# Preset teleport destinations extracted from AAA.exe
# (decoded from NtWow64WriteVirtualMemory64 captures)
TELEPORT_PRESETS = [
    # (name, X, Z, Y)
    ("点1 (Numpad?)", -2629.5901, -48.2407, -2372.8899),
    ("点2 (Numpad?)", -2843.0100, -33.6900, -1994.3101),
    ("点3 (Numpad?)", -3061.7600, -48.7749, -2008.5699),
    ("点4 (Numpad?)", -2824.8301, -36.5401, -2052.1201),
]


def enable_se_debug_privilege():
    """Enable SeDebugPrivilege so we can OpenProcess on game processes."""
    h_token = w.HANDLE()
    if not kernel32.OpenProcessToken(kernel32.GetCurrentProcess(),
                                     TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                                     ctypes.byref(h_token)):
        raise OSError("OpenProcessToken failed: %d" % ctypes.get_last_error())

    luid = LUID()
    if not advapi32.LookupPrivilegeValueA(None, b"SeDebugPrivilege", ctypes.byref(luid)):
        raise OSError("LookupPrivilegeValueA failed: %d" % ctypes.get_last_error())

    tp = TOKEN_PRIVILEGES()
    tp.PrivilegeCount = 1
    tp.Privileges[0].Luid = luid
    tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED

    if not advapi32.AdjustTokenPrivileges(h_token, False, ctypes.byref(tp),
                                          ctypes.sizeof(tp), None, None):
        raise OSError("AdjustTokenPrivileges failed: %d" % ctypes.get_last_error())
    kernel32.CloseHandle(h_token)
    print("[+] SeDebugPrivilege enabled")


def find_process(name):
    """Return PID of the first process matching `name` (case-insensitive)."""
    snapshot = kernel32.CreateToolhelp32Snapshot(0x00000002, 0)  # TH32CS_SNAPPROCESS
    pe = PROCESSENTRY32W()
    pe.dwSize = ctypes.sizeof(PROCESSENTRY32W)
    pid = None
    if kernel32.Process32FirstW(snapshot, ctypes.byref(pe)):
        while True:
            if pe.szExeFile.lower() == name.lower():
                pid = pe.th32ProcessID
                break
            if not kernel32.Process32NextW(snapshot, ctypes.byref(pe)):
                break
    kernel32.CloseHandle(snapshot)
    return pid


class MODULEENTRY32W(ctypes.Structure):
    _fields_ = [
        ("dwSize", w.DWORD),
        ("th32ModuleID", w.DWORD),
        ("th32ProcessID", w.DWORD),
        ("GlblcntUsage", w.DWORD),
        ("ProccntUsage", w.DWORD),
        ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)),
        ("modBaseSize", w.DWORD),
        ("hModule", w.HMODULE),
        ("szModule", w.WCHAR * 256),
        ("szExePath", w.WCHAR * 260),
    ]


def get_module_base(h_process, module_name, pid):
    """Find base address of a module loaded in the target process."""
    TH32CS_SNAPMODULE = 0x00000008
    TH32CS_SNAPMODULE32 = 0x00000010
    snapshot = kernel32.CreateToolhelp32Snapshot(
        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid)
    if snapshot == -1 or snapshot == 0:
        # Try without MODULE32 flag (for 64-bit only)
        snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    if snapshot == -1 or snapshot == 0:
        raise OSError("CreateToolhelp32Snapshot failed: %d" %
                      ctypes.get_last_error())
    me = MODULEENTRY32W()
    me.dwSize = ctypes.sizeof(MODULEENTRY32W)
    base = None
    if kernel32.Module32FirstW(snapshot, ctypes.byref(me)):
        while True:
            if me.szModule.lower() == module_name.lower():
                base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                break
            if not kernel32.Module32NextW(snapshot, ctypes.byref(me)):
                break
    kernel32.CloseHandle(snapshot)
    return base


def read_mem(h_process, address, size):
    """Read `size` bytes from `address` in target process."""
    buf = (ctypes.c_ubyte * size)()
    n_read = ctypes.c_size_t(0)
    if not kernel32.ReadProcessMemory(h_process, ctypes.c_void_p(address),
                                       buf, size, ctypes.byref(n_read)):
        raise OSError("ReadProcessMemory failed at 0x%X: %d" %
                      (address, ctypes.get_last_error()))
    return bytes(buf[:n_read.value])


def write_mem(h_process, address, data):
    """Write `data` bytes to `address` in target process."""
    buf = (ctypes.c_ubyte * len(data))(*data)
    n_written = ctypes.c_size_t(0)
    # Change memory protection to PAGE_EXECUTE_READWRITE first
    old_protect = w.DWORD(0)
    kernel32.VirtualProtectEx(h_process, ctypes.c_void_p(address),
                              len(data), 0x40,  # PAGE_EXECUTE_READWRITE
                              ctypes.byref(old_protect))
    ok = kernel32.WriteProcessMemory(h_process, ctypes.c_void_p(address),
                                     buf, len(data), ctypes.byref(n_written))
    if not ok:
        raise OSError("WriteProcessMemory failed at 0x%X: %d" %
                      (address, ctypes.get_last_error()))
    # Restore protection
    kernel32.VirtualProtectEx(h_process, ctypes.c_void_p(address),
                              len(data), old_protect.value,
                              ctypes.byref(old_protect))
    return n_written.value


def read_u64(h_process, address):
    """Read 8-byte little-endian uint64 from process memory."""
    return struct.unpack('<Q', read_mem(h_process, address, 8))[0]


def read_double(h_process, address):
    """Read 8-byte IEEE 754 double from process memory."""
    return struct.unpack('<d', read_mem(h_process, address, 8))[0]


def write_double(h_process, address, value):
    """Write 8-byte IEEE 754 double to process memory."""
    write_mem(h_process, address, struct.pack('<d', value))


def aob_scan_static_offset(h_process, yysls_base, module_size=0x10000000):
    """AOB 扫描 yysls 代码段，自动定位 STATIC_OFFSET。

    扫描模式: mov reg, [rip+disp32] 指向 yysls_base+STATIC_OFFSET 区域。
    特征码: 48 8B 05/0D/15/1D/25/2D/35/3D XX XX XX XX (mov r64,[rip+disp32])
    解析 disp32 计算目标地址，返回所有候选偏移。

    返回: 偏移列表 [(offset, count), ...] 按出现次数排序
    """
    import collections

    # 读取代码段（前 64MB 足够覆盖 .text）
    scan_size = min(module_size, 0x4000000)
    try:
        code = read_mem(h_process, yysls_base, scan_size)
    except OSError:
        return []

    offset_counts = collections.Counter()

    # 搜索 48 8B 05/0D/15/1D/25/2D/35/3D (mov r64, [rip+disp32])
    # 和 48 8D 05/0D/15/1D/25/2D/35/3D (lea r64, [rip+disp32])
    for i in range(len(code) - 7):
        if code[i] != 0x48:
            continue
        if code[i + 1] not in (0x8B, 0x8D):
            continue
        modrm = code[i + 2]
        if (modrm & 0xC7) != 0x05:  # mod=00, rm=101 => [rip+disp32]
            continue
        disp = struct.unpack('<i', code[i + 3:i + 7])[0]
        ins_addr = yysls_base + i
        target = ins_addr + 7 + disp
        # 目标必须在 yysls 模块范围内
        if target < yysls_base or target >= yysls_base + module_size:
            continue
        offset = target - yysls_base
        offset_counts[offset] += 1

    # 返回被多次引用的偏移（排序）
    return [(off, cnt) for off, cnt in offset_counts.most_common(20) if cnt >= 2]


def verify_static_offset(h_process, yysls_base, offset):
    """验证偏移是否有效：读取指针链第一步，检查返回值是否为合理指针。"""
    try:
        p1 = read_u64(h_process, yysls_base + offset)
        if p1 == 0:
            return False
        # P1 应该是一个堆地址（不是模块地址，不是 NULL）
        if p1 < 0x10000 or (yysls_base <= p1 < yysls_base + 0x10000000):
            return False
        # 尝试读取 P1+0x58
        p2 = read_u64(h_process, p1 + PTR_STEP1_OFFSET)
        if p2 == 0:
            return False
        # 尝试读取 P2+0x00
        obj = read_u64(h_process, p2 + PTR_STEP2_OFFSET)
        if obj == 0:
            return False
        # 尝试读取坐标（应该是合理的 double 值）
        x = read_double(h_process, obj + OFFSET_X)
        if abs(x) > 100000:  # 坐标值应该在合理范围内
            return False
        return True
    except (OSError, ValueError):
        return False


def find_static_offset(h_process, yysls_base, module_size=0x10000000):
    """查找 STATIC_OFFSET：优先使用硬编码，失败则 AOB 扫描。"""
    # 1. 先尝试硬编码偏移
    if verify_static_offset(h_process, yysls_base, STATIC_OFFSET):
        return STATIC_OFFSET, "hardcoded"

    # 2. AOB 扫描
    print("[*] 硬编码偏移失效，启动 AOB 扫描...")
    candidates = aob_scan_static_offset(h_process, yysls_base, module_size)
    for off, cnt in candidates:
        if verify_static_offset(h_process, yysls_base, off):
            print(f"[+] AOB 扫描找到有效偏移: 0x{off:X} (引用 {cnt} 次)")
            return off, "aob_scan"

    raise ValueError("无法定位 STATIC_OFFSET（硬编码和 AOB 扫描均失败）")


def resolve_player_object(h_process, yysls_base, static_offset=None):
    """Follow the pointer chain and return the player object base address."""
    if static_offset is None:
        static_offset = STATIC_OFFSET
    # Step 1: [yysls_base + static_offset] -> P1
    addr1 = yysls_base + static_offset
    p1 = read_u64(h_process, addr1)
    if p1 == 0:
        raise ValueError("Pointer chain step 1 returned NULL at 0x%X" % addr1)

    # Step 2: [P1 + 0x58] -> P2
    addr2 = p1 + PTR_STEP1_OFFSET
    p2 = read_u64(h_process, addr2)
    if p2 == 0:
        raise ValueError("Pointer chain step 2 returned NULL at 0x%X" % addr2)

    # Step 3: [P2 + 0x00] -> OBJ
    addr3 = p2 + PTR_STEP2_OFFSET
    obj = read_u64(h_process, addr3)
    if obj == 0:
        raise ValueError("Pointer chain step 3 returned NULL at 0x%X" % addr3)

    return obj


def read_coords(h_process, obj):
    """Read current X/Z/Y coordinates from player object."""
    x = read_double(h_process, obj + OFFSET_X)
    z = read_double(h_process, obj + OFFSET_Z)
    y = read_double(h_process, obj + OFFSET_Y)
    return x, z, y


def teleport(h_process, obj, x, z, y):
    """Write new X/Z/Y coordinates to player object."""
    write_double(h_process, obj + OFFSET_X, x)
    write_double(h_process, obj + OFFSET_Z, z)
    write_double(h_process, obj + OFFSET_Y, y)


def main():
    print("=" * 60)
    print("yysls 传送工具 (从 AAA.exe 提取)")
    print("=" * 60)

    # Enable SeDebugPrivilege
    enable_se_debug_privilege()

    # Find yysls.exe
    pid = find_process(YYSLS_PROCESS_NAME)
    if not pid:
        print("[!] yysls.exe not found. Is the game running?")
        sys.exit(1)
    print("[+] Found yysls.exe PID=%d" % pid)

    # Open process
    access = (PROCESS_VM_READ | PROCESS_VM_WRITE |
              PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION)
    h_process = kernel32.OpenProcess(access, False, pid)
    if not h_process:
        print("[!] OpenProcess failed: %d" % ctypes.get_last_error())
        sys.exit(1)
    print("[+] Opened process handle 0x%X" % h_process)

    # Get yysls.exe module base
    yysls_base = get_module_base(h_process, YYSLS_PROCESS_NAME, pid)
    if not yysls_base:
        print("[!] Could not find yysls.exe module base")
        sys.exit(1)
    print("[+] yysls.exe base = 0x%016X" % yysls_base)

    # 查找 STATIC_OFFSET（硬编码优先，失败则 AOB 扫描）
    try:
        static_offset, method = find_static_offset(h_process, yysls_base)
        print("[+] STATIC_OFFSET = 0x%X (method: %s)" % (static_offset, method))
    except Exception as e:
        print("[!] 定位 STATIC_OFFSET 失败: %s" % e)
        sys.exit(1)

    # Resolve player object via pointer chain
    try:
        obj = resolve_player_object(h_process, yysls_base, static_offset)
    except Exception as e:
        print("[!] Pointer chain resolution failed: %s" % e)
        sys.exit(1)
    print("[+] Player object = 0x%016X" % obj)

    # Read and display current coordinates
    x, z, y = read_coords(h_process, obj)
    print("[+] Current position: X=%.4f  Z=%.4f  Y=%.4f" % (x, z, y))

    # Interactive menu
    print("\n" + "=" * 60)
    print("Available teleport destinations:")
    print("=" * 60)
    for i, (name, tx, tz, ty) in enumerate(TELEPORT_PRESETS, 1):
        print("  %d. %s  (X=%.2f Z=%.2f Y=%.2f)" % (i, name, tx, tz, ty))
    print("  c. Show current coordinates")
    print("  r. Refresh pointer chain (re-read player object)")
    print("  s. Set custom coordinates (input X Z Y)")
    print("  q. Quit")
    print("=" * 60)

    while True:
        try:
            choice = input("\nChoice> ").strip().lower()
        except EOFError:
            break

        if choice == 'q':
            break
        elif choice == 'c':
            try:
                x, z, y = read_coords(h_process, obj)
                print("[+] Current: X=%.4f  Z=%.4f  Y=%.4f" % (x, z, y))
            except Exception as e:
                print("[!] Read failed: %s" % e)
        elif choice == 'r':
            try:
                static_offset, method = find_static_offset(h_process, yysls_base)
                obj = resolve_player_object(h_process, yysls_base, static_offset)
                print("[+] Refreshed. STATIC_OFFSET=0x%X (%s) Player object = 0x%016X" %
                      (static_offset, method, obj))
            except Exception as e:
                print("[!] Refresh failed: %s" % e)
        elif choice == 's':
            try:
                vals = input("Enter X Z Y (space-separated): ").strip().split()
                if len(vals) != 3:
                    print("[!] Need 3 values")
                    continue
                tx, tz, ty = float(vals[0]), float(vals[1]), float(vals[2])
                teleport(h_process, obj, tx, tz, ty)
                print("[+] Teleported to X=%.4f Z=%.4f Y=%.4f" % (tx, tz, ty))
            except Exception as e:
                print("[!] Teleport failed: %s" % e)
        elif choice.isdigit():
            idx = int(choice) - 1
            if 0 <= idx < len(TELEPORT_PRESETS):
                name, tx, tz, ty = TELEPORT_PRESETS[idx]
                try:
                    teleport(h_process, obj, tx, tz, ty)
                    print("[+] Teleported to %s: X=%.4f Z=%.4f Y=%.4f" %
                          (name, tx, tz, ty))
                except Exception as e:
                    print("[!] Teleport failed: %s" % e)
            else:
                print("[!] Invalid choice")
        else:
            print("[?] Unknown command")

    kernel32.CloseHandle(h_process)
    print("[*] Bye")


if __name__ == '__main__':
    main()
