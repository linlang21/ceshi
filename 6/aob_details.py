"""
查看 17 处 RIP-relative 引用的具体指令和周围代码
"""
import ctypes
import ctypes.wintypes as w
import struct

PROCESS_VM_READ = 0x0010
PROCESS_QUERY_INFORMATION = 0x0400
TOKEN_ADJUST_PRIVILEGES = 0x0020
TOKEN_QUERY = 0x0008
SE_PRIVILEGE_ENABLED = 0x00000002

kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)
advapi32 = ctypes.WinDLL('advapi32', use_last_error=True)
psapi = ctypes.WinDLL('psapi', use_last_error=True)

kernel32.OpenProcess.restype = w.HANDLE
kernel32.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
kernel32.CloseHandle.argtypes = [w.HANDLE]
kernel32.ReadProcessMemory.restype = w.BOOL
kernel32.ReadProcessMemory.argtypes = [w.HANDLE, w.LPCVOID, w.LPVOID, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
kernel32.OpenProcessToken.argtypes = [w.HANDLE, w.DWORD, ctypes.POINTER(w.HANDLE)]

advapi32.LookupPrivilegeValueA.argtypes = [w.LPCSTR, w.LPCSTR, ctypes.c_void_p]
advapi32.AdjustTokenPrivileges.argtypes = [w.HANDLE, w.BOOL, ctypes.c_void_p, w.DWORD, ctypes.c_void_p, ctypes.c_void_p]

class LUID(ctypes.Structure):
    _fields_ = [("LowPart", w.DWORD), ("HighPart", ctypes.c_long)]

class TOKEN_PRIVILEGES(ctypes.Structure):
    _fields_ = [("PrivilegeCount", w.DWORD), ("Luid", LUID), ("Attributes", w.DWORD)]


def enable_debug_privilege():
    h_token = w.HANDLE()
    kernel32.OpenProcessToken(kernel32.GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ctypes.byref(h_token))
    luid = LUID()
    advapi32.LookupPrivilegeValueA(None, b"SeDebugPrivilege", ctypes.byref(luid))
    tp = TOKEN_PRIVILEGES()
    tp.PrivilegeCount = 1
    tp.Luid = luid
    tp.Attributes = SE_PRIVILEGE_ENABLED
    advapi32.AdjustTokenPrivileges(h_token, False, ctypes.byref(tp), ctypes.sizeof(tp), None, None)
    kernel32.CloseHandle(h_token)


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if kernel32.ReadProcessMemory(h, addr, buf, size, ctypes.byref(n)):
        return buf.raw[:n.value]
    return None


def main():
    enable_debug_privilege()
    pid = 1900
    base = 0x140000000
    static_addr = base + 0x83f46d8

    h = kernel32.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, False, pid)

    # 17 处 RIP-relative 引用（从扫描3结果）
    refs = [
        0x00000001441a3860,
        0x00000001441be49e,
        0x00000001441c159f,
        0x00000001441c1aed,
        0x00000001442c38f7,
        0x00000001442c43dc,
        0x000000014430c8b0,
        0x000000014431b5cc,
        0x000000014438e761,
        0x000000014438ed40,
        0x000000014438f368,
        0x000000014438f5ec,
        0x000000014438f767,
        0x000000014438f867,
        0x000000014438f967,
        0x000000014438fa67,
        0x000000014438fb67,
    ]

    print("=" * 90)
    print("17 处 RIP-relative 引用的指令详情")
    print("=" * 90)

    for i, addr in enumerate(refs):
        off = addr - base
        # 读取指令前16字节 + 指令7字节 + 后32字节
        ctx = read_mem(h, addr - 16, 55)
        if ctx:
            pre = ' '.join(f'{b:02x}' for b in ctx[:16])
            ins = ' '.join(f'{b:02x}' for b in ctx[16:23])
            post = ' '.join(f'{b:02x}' for b in ctx[23:55])
            # 解析指令
            if ctx[16] == 0x48 and ctx[17] in (0x8B, 0x8D):
                modrm = ctx[18]
                reg_idx = (modrm >> 3) & 7
                regs = ['rax','rcx','rdx','rbx','rsp','rbp','rsi','rdi']
                op = 'mov' if ctx[17] == 0x8B else 'lea'
                disp = struct.unpack('<i', ctx[19:23])[0]
                target = addr + 7 + disp
                ins_str = f"{op} {regs[reg_idx]}, [rip+0x{disp & 0xFFFFFFFF:08x}] -> 0x{target:016x}"
            else:
                ins_str = "??"
            print(f"\n[{i+1}] yysls+0x{off:x} (0x{addr:016x})")
            print(f"    pre : {pre}")
            print(f"    ins : {ins}  | {ins_str}")
            print(f"    post: {post}")

    # 特别分析密集区域 yysls+0x438e000 ~ 0x4390000
    print("\n" + "=" * 90)
    print("密集引用区域分析: yysls+0x438e761 ~ 0x438fb67")
    print("=" * 90)

    # 读取 yysls+0x438e700 附近的代码块
    block_start = 0x000000014438e700
    block_data = read_mem(h, block_start, 0x1500)
    if block_data:
        print(f"\n代码块 0x{block_start:016x} ~ 0x{block_start+0x1500:016x}")
        print(f"（搜索 48 8B 05/0D/15/1D/25/2D/35/3D 和 48 8D 05/0D/15/1D/25/2D/35/3D 模式）")
        for i in range(len(block_data) - 7):
            if block_data[i] == 0x48 and block_data[i+1] in (0x8B, 0x8D):
                modrm = block_data[i+2]
                if (modrm & 0xC7) == 0x05:
                    disp = struct.unpack('<i', block_data[i+3:i+7])[0]
                    ins_addr = block_start + i
                    target = ins_addr + 7 + disp
                    if target == static_addr:
                        reg_idx = (modrm >> 3) & 7
                        regs = ['rax','rcx','rdx','rbx','rsp','rbp','rsi','rdi']
                        op = 'mov' if block_data[i+1] == 0x8B else 'lea'
                        # 读取前后上下文
                        pre_start = max(0, i - 8)
                        pre = ' '.join(f'{b:02x}' for b in block_data[pre_start:i])
                        ins_bytes = ' '.join(f'{b:02x}' for b in block_data[i:i+7])
                        post = ' '.join(f'{b:02x}' for b in block_data[i+7:i+7+16])
                        print(f"\n  yysls+0x{ins_addr-base:x}: {op} {regs[reg_idx]},[rip+0x{disp & 0xFFFFFFFF:08x}]")
                        print(f"    pre : {pre}")
                        print(f"    ins : {ins_bytes}")
                        print(f"    post: {post}")

    # AOB 特征码提取：查看能否找到一个独特的字节模式
    print("\n" + "=" * 90)
    print("AOB 特征码提取（用于 AAA.exe 风格的扫描）")
    print("=" * 90)
    # 取第一处引用 yysls+0x41a3860 附近的代码作为 AOB 模式
    aob_addr = refs[0]
    aob_data = read_mem(h, aob_addr - 16, 48)
    if aob_data:
        # 提取指令前后的特征字节
        pattern = aob_data[8:31]  # 前8字节 + 指令7字节 + 后8字节
        hex_pattern = ' '.join(f'{b:02x}' for b in pattern)
        print(f"\n  第一处引用 yysls+0x{refs[0]-base:x}:")
        print(f"  AOB 模式（24字节）: {hex_pattern}")
        print(f"  其中 [8:15] 是 mov rcx,[rip+disp] 指令")

    kernel32.CloseHandle(h)


if __name__ == '__main__':
    main()
