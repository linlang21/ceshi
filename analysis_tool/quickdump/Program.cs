// QuickDump.cs
// 启动一个辅助exe, 在它启动后被版本检查强制退出前, 持续高频抓取进程内存.
// 不需要游戏进程配合. 抓完后用关键词搜索功能串.
//
// 关键策略:
//   1. 启用 SeDebugPrivilege
//   2. CreateProcess 启动目标 (带 CREATE_SUSPENDED? 不行, 那样 VMP 不会解密)
//      用普通 CREATE_NEW_CONSOLE 启动, 然后疯狂循环读内存
//   3. 每 50ms 扫描所有可读区域, 检测是否出现"传送/无敌"等关键词
//      一旦发现, 立即全量 dump 所有 RWX 区域到磁盘
//   4. 同时监控进程退出事件, 退出后做最后一次完整 dump
//
// 目标: 1-5 号 (VVV/V2.3/V2.4/科技0.3/界碑)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class QuickDump
{
    const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    const uint TOKEN_QUERY = 0x0008;
    const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    const uint PROCESS_VM_READ = 0x0010;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_VM_WRITE = 0x0020;
    const uint PROCESS_VM_OPERATION = 0x0008;
    const uint MEM_COMMIT = 0x1000;
    const uint PAGE_GUARD = 0x100;
    const uint PAGE_NOCACHE = 0x200;
    const uint MEM_PRIVATE = 0x20000;
    const uint MEM_IMAGE = 0x1000000;

    [StructLayout(LayoutKind.Sequential)]
    struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)]
    struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    struct TOKEN_PRIVILEGES { public uint PrivilegeCount; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] public LUID_AND_ATTRIBUTES[] Privileges; }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct PROCESSENTRY32 { public uint dwSize; public uint cntUsage; public uint th32ProcessID; public IntPtr th32HeapID; public uint th32ModuleID; public uint cntThreads; public IntPtr th32ParentProcessID; public int pcPriClassBase; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile; }

    [DllImport("kernel32.dll")]
    static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll")]
    static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct STARTUPINFO { public int cb; public IntPtr lpReserved; public IntPtr lpDesktop; public IntPtr lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }

    const uint CREATE_NEW_CONSOLE = 0x00000010;

    static bool EnableDebugPrivilege()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken)) return false;
        LookupPrivilegeValue(null, "SeDebugPrivilege", out LUID luid);
        var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new LUID_AND_ATTRIBUTES[1] };
        tp.Privileges[0].Luid = luid;
        tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
        bool ok = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        CloseHandle(hToken);
        return ok;
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length < 1)
        {
            Console.WriteLine("用法: QuickDump <目标exe路径> [采样间隔ms=50] [总时长ms=15000]");
            Console.WriteLine("示例: QuickDump \"e:\\ceshi\\2\\V2.3.exe\"");
            return 1;
        }
        string target = args[0];
        int interval = args.Length > 1 ? int.Parse(args[1]) : 50;
        int totalMs = args.Length > 2 ? int.Parse(args[2]) : 15000;
        if (!File.Exists(target)) { Console.WriteLine($"文件不存在: {target}"); return 1; }
        if (!EnableDebugPrivilege()) Console.WriteLine("[警告] SeDebugPrivilege 启用失败,部分操作可能失败");

        string outDir = Path.Combine(Path.GetDirectoryName(target) ?? ".", "dump_quick");
        Directory.CreateDirectory(outDir);
        string tag = Path.GetFileNameWithoutExtension(target);
        string logFile = Path.Combine(outDir, $"{tag}_quickdump.log");
        var log = new StreamWriter(logFile, false, new UTF8Encoding(false)) { AutoFlush = true };
        Log(log, $"开始: target={target} interval={interval}ms total={totalMs}ms outDir={outDir}");

        // 启动目标
        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
        string cmd = $"\"{target}\"";
        if (!CreateProcess(null, cmd, IntPtr.Zero, IntPtr.Zero, false, CREATE_NEW_CONSOLE, IntPtr.Zero, Path.GetDirectoryName(target), ref si, out PROCESS_INFORMATION pi))
        {
            Log(log, $"[FATAL] CreateProcess 失败 err={Marshal.GetLastWin32Error()}");
            log.Close();
            return 2;
        }
        Log(log, $"已启动: pid={pi.dwProcessId}");

        IntPtr hProc = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION, false, pi.dwProcessId);
        if (hProc == IntPtr.Zero)
        {
            Log(log, $"[FATAL] OpenProcess 失败 err={Marshal.GetLastWin32Error()}");
            return 3;
        }

        var kwGBK = new[] { "传送","无敌","倍攻","坐标","加速","定怪","去草","一击","必杀","无限","拾取","采集","隐身","体力","界碑","望月","开","关","换皮","变身","皮肤","罪恶","GM","Help","HelpClass" };
        var kwASCII = new[] { "yysls","teleport","godmode","inject","hook","read","write" };
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        var kwBytes = kwGBK.Select(gbk.GetBytes).ToList();
        var kwAscBytes = kwASCII.Select(Encoding.ASCII.GetBytes).ToList();

        int totalRegions = 0;
        long totalBytes = 0;
        int foundHits = 0;
        var foundKw = new HashSet<string>();
        bool saved = false;

        var sw = Stopwatch.StartNew();
        int scanNo = 0;
        IntPtr maxAddr = (IntPtr)0x7FFFFFFF;
        while (sw.ElapsedMilliseconds < totalMs)
        {
            scanNo++;
            // 检查进程是否还活着
            GetExitCodeProcess(pi.hProcess, out uint ec);
            if (ec != 0x103) // STILL_ACTIVE = 259
            {
                Log(log, $"[scan #{scanNo}] 进程已退出 exitCode=0x{ec:X}, 结束扫描");
                break;
            }

            IntPtr addr = IntPtr.Zero;
            int thisRegions = 0;
            int thisHits = 0;
            var scanBuf = new byte[4096];
            while ((long)addr < (long)maxAddr)
            {
                int qret = VirtualQueryEx(hProc, addr, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
                if (qret == 0) break;
                long regionSize = (long)mbi.RegionSize;
                bool readable = (mbi.State & MEM_COMMIT) != 0 && (mbi.Protect & 0xF0) != 0 && (mbi.Protect & PAGE_GUARD) == 0;
                if (readable && regionSize > 0 && regionSize < 200L * 1024 * 1024)
                {
                    thisRegions++;
                    // 先扫前 64KB 找关键词, 命中再决定是否全 dump
                    int scanSize = (int)Math.Min(regionSize, 64L * 1024);
                    if (ReadProcessMemory(hProc, mbi.BaseAddress, scanBuf, scanSize, out int br) && br > 0)
                    {
                        for (int k = 0; k < kwBytes.Count; k++)
                        {
                            if (ContainsAt(scanBuf, kwBytes[k], 0, br)) { thisHits++; foundKw.Add(kwGBK[k]); }
                        }
                        for (int k = 0; k < kwAscBytes.Count; k++)
                        {
                            if (ContainsAt(scanBuf, kwAscBytes[k], 0, br)) { thisHits++; foundKw.Add(kwASCII[k]); }
                        }
                    }
                }
                try { addr = (IntPtr)((long)addr + regionSize); } catch { break; }
            }
            totalRegions = thisRegions;
            if (thisHits > 0)
            {
                foundHits += thisHits;
                Log(log, $"[scan #{scanNo} t={sw.ElapsedMilliseconds}ms] 命中 {thisHits} 次! 区域={thisRegions} 已发现关键词: {string.Join(",", foundKw)}");
                if (!saved)
                {
                    // 第一次命中 → 全量 dump
                    saved = true;
                    DumpAll(hProc, maxAddr, outDir, tag, log);
                }
            }
            else
            {
                if (scanNo % 20 == 0) Log(log, $"[scan #{scanNo} t={sw.ElapsedMilliseconds}ms] 区域={thisRegions} 未命中");
            }
            Thread.Sleep(interval);
        }

        // 退出前最后 dump
        GetExitCodeProcess(pi.hProcess, out uint ec2);
        if (ec2 == 0x103)
        {
            Log(log, "进程仍存活,做最后全量dump");
            DumpAll(hProc, maxAddr, outDir, tag + "_final", log);
        }
        else
        {
            Log(log, $"进程已退出 exitCode=0x{ec2:X}, 最后全量dump已死进程");
            DumpAll(hProc, maxAddr, outDir, tag + "_post", log);
        }
        CloseHandle(hProc);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        Log(log, $"结束: 共扫描 {scanNo} 次, 累计命中 {foundHits} 次, 关键词 {string.Join(",", foundKw)}");
        log.Close();
        Console.WriteLine($"\n=== 完成 === 日志: {logFile}");
        return 0;
    }

    static void DumpAll(IntPtr hProc, IntPtr maxAddr, string outDir, string tag, StreamWriter log)
    {
        string file = Path.Combine(outDir, $"{tag}_memdump.bin");
        var fs = new FileStream(file, FileMode.Create, FileAccess.Write);
        IntPtr addr = IntPtr.Zero;
        int regions = 0;
        long total = 0;
        var hdr = new StreamWriter(file + ".index.txt", false, new UTF8Encoding(false));
        hdr.WriteLine($"# QuickDump index: {file}");
        hdr.WriteLine($"# region_base\tregion_size\tprotect\ttype");
        while ((long)addr < (long)maxAddr)
        {
            int qret = VirtualQueryEx(hProc, addr, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
            if (qret == 0) break;
            long regionSize = (long)mbi.RegionSize;
            bool readable = (mbi.State & MEM_COMMIT) != 0 && (mbi.Protect & 0xF0) != 0 && (mbi.Protect & PAGE_GUARD) == 0;
            if (readable && regionSize > 0 && regionSize < 100L * 1024 * 1024)
            {
                var buf = new byte[regionSize];
                if (ReadProcessMemory(hProc, mbi.BaseAddress, buf, (int)regionSize, out int br) && br > 0)
                {
                    fs.Write(buf, 0, br);
                    hdr.WriteLine($"0x{(long)mbi.BaseAddress:X}\t{regionSize}\t0x{mbi.Protect:X}\t0x{mbi.Type:X}");
                    regions++;
                    total += br;
                }
            }
            try { addr = (IntPtr)((long)addr + regionSize); } catch { break; }
        }
        fs.Close();
        hdr.Close();
        Log(log, $"  dump完成: {file} regions={regions} total={total/1024}KB");
    }

    static bool ContainsAt(byte[] data, byte[] pat, int start, int end)
    {
        if (pat.Length == 0) return false;
        int last = end - pat.Length;
        for (int i = start; i <= last; i++)
        {
            bool m = true; for (int j = 0; j < pat.Length; j++) if (data[i + j] != pat[j]) { m = false; break; }
            if (m) return true;
        }
        return false;
    }

    static void Log(StreamWriter w, string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        w.WriteLine(line);
        Console.WriteLine(line);
    }
}
