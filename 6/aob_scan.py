"""
AOB 扫描 yysls.exe - 查找 0x83f46d8 偏移的引用
================================================
扫描策略：
  1. 在 yysls.exe 代码段搜索 0x83f46d8 作为 32 位立即数
  2. 在整个内存中搜索 0x00000001483f46d8 作为 64 位指针
  3. 在 .rdata/.data 段搜索指向 0x1483f46d8 的指针表
"""
import ctypes
import ctypes.wintypes as w
import struct
import sys

# ========== Windows API ==========
PROCESS_VM_READ = 0x0010
PROCESS_QUERY_INFORMATION = 0x0400
TOKEN_ADJUST_PRIVILEGES = 0x0020
TOKEN_QUERY = 0x0008
SE_PRIVILEGE_ENABLED = 0x00000002

kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
advapi32 = ctypes.WinDLL('advapi32', use_last_error=True)
psapi = ctypes.WinDLL('psapi', use_last_error=True)
ntdll = ctypes.WinDLL('ntdll', use_last_error=True)

kernel32.OpenProcess.restype = w.HANDLE
kernel32.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
kernel32.CloseHandle.argtypes = [w.HANDLE]
kernel32.ReadProcessMemory.restype = w.BOOL
kernel32.ReadProcessMemory.argtypes = [w.HANDLE, w.LPCVOID, w.LPVOID, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
kernel32.VirtualQueryEx.restype = ctypes.c_size_t
kernel32.OpenProcessToken.argtypes = [w.HANDLE, w.DWORD, ctypes.POINTER(w.HANDLE)]
kernel32.GetModuleHandleW.restype = w.HMODULE
kernel32.GetModuleHandleW.argtypes = [w.LPCWSTR]

advapi32.LookupPrivilegeValueA.argtypes = [w.LPCSTR, w.LPCSTR, ctypes.c_void_p]
advapi32.AdjustTokenPrivileges.argtypes = [w.HANDLE, w.BOOL, ctypes.c_void_p, w.DWORD, ctypes.c_void_p, ctypes.c_void_p]

psapi.GetModuleBaseNameW.restype = w.DWORD
psapi.GetModuleBaseNameW.argtypes = [w.HANDLE, w.HMODULE, w.LPWSTR, w.DWORD]
psapi.EnumProcessModulesEx.restype = w.BOOL
psapi.EnumProcessModulesEx.argtypes = [w.HANDLE, ctypes.POINTER(w.HMODULE), w.DWORD, ctypes.POINTER(w.DWORD), w.DWORD]
psapi.GetModuleInformation.restype = w.BOOL
psapi.GetModuleInformation.argtypes = [w.HANDLE, w.HMODULE, ctypes.c_void_p, w.DWORD]

class LUID(ctypes.Structure):
    _fields_ = [("LowPart", w.DWORD), ("HighPart", ctypes.c_long)]

class TOKEN_PRIVILEGES(ctypes.Structure):
    _fields_ = [
        ("PrivilegeCount", w.DWORD),
        ("Luid", LUID),
        ("Attributes", w.DWORD),
    ]

class MEMORY_BASIC_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("BaseAddress", ctypes.c_void_p),
        ("AllocationBase", ctypes.c_void_p),
        ("AllocationProtect", w.DWORD),
        ("RegionSize", ctypes.c_size_t),
        ("State", w.DWORD),
        ("Protect", w.DWORD),
        ("Type", w.DWORD),
    ]

class MODULEINFO(ctypes.Structure):
    _fields_ = [
        ("lpBaseOfDll", ctypes.c_void_p),
        ("SizeOfImage", w.DWORD),
        ("EntryPoint", ctypes.c_void_p),
    ]


def enable_debug_privilege():
    h_token = w.HANDLE()
    if not kernel32.OpenProcessToken(kernel32.GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ctypes.byref(h_token)):
        return False
    luid = LUID()
    if not advapi32.LookupPrivilegeValueA(None, b"SeDebugPrivilege", ctypes.byref(luid)):
        kernel32.CloseHandle(h_token)
        return False
    tp = TOKEN_PRIVILEGES()
    tp.PrivilegeCount = 1
    tp.Luid = luid
    tp.Attributes = SE_PRIVILEGE_ENABLED
    ok = advapi32.AdjustTokenPrivileges(h_token, False, ctypes.byref(tp), ctypes.sizeof(tp), None, None)
    kernel32.CloseHandle(h_token)
    return ok


def find_yysls_pid():
    import subprocess
    result = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq yysls.exe', '/FO', 'CSV', '/NH'],
                            capture_output=True, text=True)
    for line in result.stdout.strip().split('\n'):
        if 'yysls.exe' in line.lower():
            parts = line.strip('"').split('","')
            if len(parts) >= 2:
                return int(parts[1])
    return None


def get_yysls_module_info(h_process):
    """获取 yysls.exe 模块基址和大小"""
    hmods = (w.HMODULE * 1024)()
    cb_needed = w.DWORD()
    if not psapi.EnumProcessModulesEx(h_process, hmods, ctypes.sizeof(hmods), ctypes.byref(cb_needed), 3):
        return None, None
    count = cb_needed.value // ctypes.sizeof(w.HMODULE)
    for i in range(count):
        name_buf = ctypes.create_unicode_buffer(260)
        psapi.GetModuleBaseNameW(h_process, hmods[i], name_buf, 260)
        if name_buf.value.lower() == 'yysls.exe':
            mi = MODULEINFO()
            if psapi.GetModuleInformation(h_process, hmods[i], ctypes.byref(mi), ctypes.sizeof(mi)):
                return mi.lpBaseOfDll, mi.SizeOfImage
    return None, None


def read_mem(h_process, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if kernel32.ReadProcessMemory(h_process, addr, buf, size, ctypes.byref(n)):
        return buf.raw[:n.value]
    return None


def scan_memory_for_bytes(h_process, target_bytes, label, max_results=50, region_filter=None, scan_start=0x10000, scan_end=0x7FFFFFFFFFFF):
    """扫描进程内存，搜索目标字节序列"""
    results = []
    addr = scan_start
    mbi = MEMORY_BASIC_INFORMATION()
    
    while addr < scan_end:
        if kernel32.VirtualQueryEx(h_process, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)) == 0:
            addr += 0x1000
            continue
        
        # 只扫描已提交、可读的内存
        if mbi.State != 0x1000:  # MEM_COMMIT
            addr += mbi.RegionSize
            continue
        
        protect = mbi.Protect & 0xFF
        if protect in (0x01, 0x00):  # NOACCESS, 不可读
            addr += mbi.RegionSize
            continue
        
        # 区域过滤
        if region_filter:
            if region_filter == 'code' and protect not in (0x10, 0x20, 0x40, 0x80):
                addr += mbi.RegionSize
                continue
            if region_filter == 'data' and protect not in (0x02, 0x04, 0x08):
                addr += mbi.RegionSize
                continue
        
        # 读取区域
        region_size = mbi.RegionSize
        if region_size > 64 * 1024 * 1024:  # 跳过超过 64MB 的区域
            addr += region_size
            continue
        
        data = read_mem(h_process, mbi.BaseAddress, region_size)
        if not data:
            addr += region_size
            continue
        
        # 搜索
        offset = 0
        while True:
            pos = data.find(target_bytes, offset)
            if pos == -1:
                break
            found_addr = (mbi.BaseAddress or 0) + pos
            results.append((found_addr, mbi.Protect, mbi.Type))
            if len(results) >= max_results:
                return results
            offset = pos + 1
        
        addr += region_size
    
    return results


def main():
    if not enable_debug_privilege():
        print("[!] Failed to enable SeDebugPrivilege")
        return
    
    pid = find_yysls_pid()
    if not pid:
        print("[!] yysls.exe not found")
        return
    
    print(f"[*] yysls.exe PID = {pid}")
    
    h_process = kernel32.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, False, pid)
    if not h_process:
        print(f"[!] OpenProcess failed: {ctypes.get_last_error()}")
        return
    
    base, size = get_yysls_module_info(h_process)
    print(f"[*] yysls.exe base = 0x{base:016x}, size = 0x{size:x}")
    
    static_addr = base + 0x83f46d8
    print(f"[*] Target static address = 0x{static_addr:016x} (base + 0x83f46d8)")
    print()
    
    # ===== 扫描 1: 在 yysls 代码段搜索 0x83f46d8 作为 32 位立即数 =====
    print("=" * 80)
    print("扫描 1: 在 yysls 代码段搜索 0x83f46d8 (32位立即数)")
    print("=" * 80)
    target_le = struct.pack('<I', 0x83f46d8)
    results = scan_memory_for_bytes(h_process, target_le, "0x83f46d8", max_results=100, region_filter='code', scan_start=base, scan_end=base+size)
    print(f"  找到 {len(results)} 处匹配")
    for addr, prot, mtype in results[:30]:
        ctx = read_mem(h_process, addr - 8, 24)
        if ctx:
            hex_ctx = ' '.join(f'{b:02x}' for b in ctx)
        else:
            hex_ctx = '?'
        in_module = "yysls" if base <= addr < base + size else "other"
        print(f"  0x{addr:016x} [{in_module}] protect=0x{prot:x} ctx={hex_ctx}")
    
    # ===== 扫描 2: 在整个内存搜索 0x00000001483f46d8 (64位指针) =====
    print()
    print("=" * 80)
    print("扫描 2: 搜索完整地址 0x00000001483f46d8 (64位指针)")
    print("=" * 80)
    target_ptr = struct.pack('<Q', static_addr)
    results2 = scan_memory_for_bytes(h_process, target_ptr, "ptr", max_results=100)
    print(f"  找到 {len(results2)} 处匹配")
    for addr, prot, mtype in results2[:30]:
        in_module = "yysls" if base <= addr < base + size else "other"
        # 判断是否在 .rdata/.data (可读但不可执行)
        seg = "data" if prot in (0x02, 0x04, 0x08) else ("code" if prot in (0x10, 0x20, 0x40) else "other")
        print(f"  0x{addr:016x} [{in_module}/{seg}] protect=0x{prot:x} type=0x{mtype:x}")
    
    # ===== 扫描 3: 在代码段搜索 RIP-relative 引用 =====
    # RIP-relative: disp32 = target - (rip + instruction_len)
    # 常见指令: mov rax,[rip+disp32] (48 8b 05 xx xx xx xx) len=7
    #           lea rax,[rip+disp32] (48 8d 05 xx xx xx xx) len=7
    #           mov rax,[rip+disp32] (48 8b 0d xx xx xx xx) len=7 (rcx)
    print()
    print("=" * 80)
    print("扫描 3: 在代码段搜索 RIP-relative 引用 0x1483f46d8")
    print("=" * 80)
    # 扫描 yysls 代码段，找 mov/lea reg,[rip+disp32] 指向目标地址
    rip_results = []
    addr = base
    mbi = MEMORY_BASIC_INFORMATION()
    while addr < base + size:
        if kernel32.VirtualQueryEx(h_process, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)) == 0:
            addr += 0x1000
            continue
        if mbi.State != 0x1000 or (mbi.Protect & 0xF0) == 0:  # 非可执行
            addr += mbi.RegionSize
            continue
        data = read_mem(h_process, mbi.BaseAddress, mbi.RegionSize)
        if not data:
            addr += mbi.RegionSize
            continue
        # 搜索 48 8B 05/0D/15/1D/25/2D/35/3D (mov reg,[rip+disp32])
        # 和 48 8D 05/0D/15/1D/25/2D/35/3D (lea reg,[rip+disp32])
        for i in range(len(data) - 7):
            if data[i] == 0x48 and data[i+1] in (0x8B, 0x8D):
                modrm = data[i+2]
                if (modrm & 0xC7) == 0x05:  # [rip+disp32]
                    disp = struct.unpack('<i', data[i+3:i+7])[0]
                    ins_addr = (mbi.BaseAddress or 0) + i
                    target = ins_addr + 7 + disp
                    if target == static_addr:
                        reg_idx = (modrm >> 3) & 7
                        regs = ['rax','rcx','rdx','rbx','rsp','rbp','rsi','rdi']
                        op = 'mov' if data[i+1] == 0x8B else 'lea'
                        rip_results.append((ins_addr, op, regs[reg_idx], target))
                        if len(rip_results) >= 50:
                            break
        addr += mbi.RegionSize
    print(f"  找到 {len(rip_results)} 处 RIP-relative 引用")
    for ins_addr, op, reg, target in rip_results[:30]:
        offset_in_module = ins_addr - base
        print(f"  0x{ins_addr:016x} (yysls+0x{offset_in_module:x}): {op} {reg}, [rip+disp] -> 0x{target:016x}")
    
    # ===== 扫描 4: 搜索 0x340/0x348/0x350 偏移在代码段的使用 =====
    print()
    print("=" * 80)
    print("扫描 4: 在 yysls 代码段搜索坐标偏移 0x340/0x348/0x350")
    print("=" * 80)
    for offset_val in [0x340, 0x348, 0x350]:
        target_le = struct.pack('<I', offset_val)
        results = scan_memory_for_bytes(h_process, target_le, f"0x{offset_val:x}", max_results=30, region_filter='code', scan_start=base, scan_end=base+size)
        # 只显示在 yysls 模块内的
        yysls_results = [(a, p, t) for a, p, t in results if base <= a < base + size]
        print(f"  0x{offset_val:x}: {len(yysls_results)} 处在 yysls 代码段")
        for addr, prot, mtype in yysls_results[:10]:
            ctx = read_mem(h_process, addr - 4, 16)
            hex_ctx = ' '.join(f'{b:02x}' for b in ctx) if ctx else '?'
            off = addr - base
            print(f"    yysls+0x{off:x} ctx={hex_ctx}")
    
    # ===== 扫描 5: 在 AAA.exe dump 中搜索 0x83f46d8 =====
    print()
    print("=" * 80)
    print("扫描 5: 在 AAA.exe dump 中搜索 0x83f46d8")
    print("=" * 80)
    dump_path = r'E:\ceshi\6\dump\AAA.bin'
    try:
        with open(dump_path, 'rb') as f:
            dump_data = f.read()
        print(f"  dump 大小: {len(dump_data)} 字节")
        
        # 搜索 0x83f46d8 (32位 LE)
        target_le = struct.pack('<I', 0x83f46d8)
        positions = []
        offset = 0
        while True:
            pos = dump_data.find(target_le, offset)
            if pos == -1:
                break
            positions.append(pos)
            offset = pos + 1
            if len(positions) >= 50:
                break
        print(f"  0x83f46d8 (32位): {len(positions)} 处匹配")
        for pos in positions[:20]:
            ctx = dump_data[max(0,pos-8):pos+12]
            hex_ctx = ' '.join(f'{b:02x}' for b in ctx)
            print(f"    FileOffset=0x{pos:08x} ctx={hex_ctx}")
        
        # 搜索 0x1483f46d8 (64位 LE) - 但注意 AAA 是32位，可能不会存64位
        target_64 = struct.pack('<Q', static_addr)
        positions64 = []
        offset = 0
        while True:
            pos = dump_data.find(target_64, offset)
            if pos == -1:
                break
            positions64.append(pos)
            offset = pos + 1
            if len(positions64) >= 50:
                break
        print(f"  0x1483f46d8 (64位): {len(positions64)} 处匹配")
        for pos in positions64[:10]:
            ctx = dump_data[max(0,pos-8):pos+16]
            hex_ctx = ' '.join(f'{b:02x}' for b in ctx)
            print(f"    FileOffset=0x{pos:08x} ctx={hex_ctx}")
    except FileNotFoundError:
        print(f"  [!] dump 文件不存在: {dump_path}")
    
    # ===== 扫描 6: 在 yysls 数据段搜索 0x83f46d8 =====
    print()
    print("=" * 80)
    print("扫描 6: 在 yysls 数据段(.rdata/.data)搜索 0x83f46d8")
    print("=" * 80)
    target_le = struct.pack('<I', 0x83f46d8)
    results = scan_memory_for_bytes(h_process, target_le, "0x83f46d8", max_results=50, region_filter='data', scan_start=base, scan_end=base+size)
    print(f"  找到 {len(results)} 处匹配")
    for addr, prot, mtype in results[:20]:
        off = addr - base
        ctx = read_mem(h_process, addr - 8, 24)
        hex_ctx = ' '.join(f'{b:02x}' for b in ctx) if ctx else '?'
        print(f"    yysls+0x{off:x} (0x{addr:016x}) protect=0x{prot:x} ctx={hex_ctx}")
    
    kernel32.CloseHandle(h_process)


if __name__ == '__main__':
    main()
