using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class PeReport
{
    static string[] Targets = new[]
    {
        @"e:\ceshi\1\VVV.exe",
        @"e:\ceshi\2\V2.3.exe",
        @"e:\ceshi\3\燕云学习软件 V2.4.exe",
        @"e:\ceshi\4\燕云十六科技学习_0.3.exe",
        @"e:\ceshi\5\进游戏内用界碑传送一下再启动我.exe",
    };

    static string[] Keywords = new[]
    {
        "yysls", "无敌", "倍攻", "传送", "坐标", "加速", "无限", "一击", "必杀",
        "定怪", "去草", "采集", "拾取", "隐身", "体力", "buff", "BUFF", "godmode",
        "注入", "界碑", "望月", "罪恶", "换肤", "变身", "ReadProcessMemory",
        "WriteProcessMemory", "NtWow64", "OpenProcess", "VirtualAlloc",
        "lua", "Lua", "hexm", "set_bf", "set_gm", "set_niubility", ".sys", "Orange",
    };

    static void Main()
    {
        var sb = new StringBuilder();
        foreach (var path in Targets)
        {
            try { Analyze(path, sb); }
            catch (Exception ex) { sb.AppendLine($"[ERROR] {path}: {ex.Message}"); }
            sb.AppendLine(new string('=', 70));
        }
        string outPath = @"e:\ceshi\analysis_tool\report.txt";
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(sb.ToString());
        Console.WriteLine($"\n报告已写入: {outPath}");
    }

    static void Analyze(string path, StringBuilder sb)
    {
        sb.AppendLine($"文件: {path}");
        if (!File.Exists(path)) { sb.AppendLine("  [不存在]"); return; }
        byte[] data = File.ReadAllBytes(path);
        sb.AppendLine($"  大小: {data.Length / 1024.0 / 1024.0:F2} MB ({data.Length} bytes)");

        // DOS / PE headers
        if (data.Length < 0x40 || data[0] != 'M' || data[1] != 'Z') { sb.AppendLine("  非PE文件"); return; }
        int peOff = BitConverter.ToInt32(data, 0x3C);
        if (peOff + 6 > data.Length || data[peOff] != 'P' || data[peOff + 1] != 'E') { sb.AppendLine("  PE头无效"); return; }
        ushort machine = BitConverter.ToUInt16(data, peOff + 4);
        ushort numSections = BitConverter.ToUInt16(data, peOff + 6);
        ushort optSize = BitConverter.ToUInt16(data, peOff + 20);
        sb.AppendLine($"  架构: {(machine == 0x8664 ? "x64" : machine == 0x14c ? "x86" : "0x" + machine.ToString("X"))}");

        // Sections
        int secStart = peOff + 24 + optSize;
        var secInfo = new List<(string name, uint vsize, uint rsize)>();
        for (int i = 0; i < numSections; i++)
        {
            int s = secStart + i * 40;
            if (s + 40 > data.Length) break;
            string name = Encoding.ASCII.GetString(data, s, 8).TrimEnd('\0', ' ');
            uint vsize = BitConverter.ToUInt32(data, s + 8);
            uint rsize = BitConverter.ToUInt32(data, s + 16);
            secInfo.Add((name, vsize, rsize));
        }
        sb.AppendLine($"  节区({numSections}):");
        foreach (var sec in secInfo)
            sb.AppendLine($"    {sec.name,-12} VSize={sec.vsize / 1024.0:F1}KB  RawSize={sec.rsize / 1024.0:F1}KB");

        // Packer detection
        var secNames = secInfo.Select(x => x.name).ToList();
        var packers = new List<string>();
        if (secNames.Any(n => n.StartsWith("UPX"))) packers.Add("UPX");
        if (secNames.Any(n => n.Contains("vmp") || n.Contains("VMP"))) packers.Add("VMProtect(命名节区)");
        if (secNames.Any(n => n.Contains("enigma") || n.Contains("Enigma"))) packers.Add("Enigma");
        if (secNames.Any(n => n.Contains("ChongZi") || n.Contains("chongzi"))) packers.Add(".ChongZi");
        if (secNames.Any(n => n.Contains(".themida") || n.Contains("Themida"))) packers.Add("Themida");
        // VMP 隐式判断: .rdata 异常巨大
        var rdata = secInfo.FirstOrDefault(x => x.name == ".rdata");
        if (rdata.vsize > 5 * 1024 * 1024) packers.Add($"疑似VMP(.rdata={rdata.vsize / 1024.0 / 1024.0:F1}MB异常大)");
        sb.AppendLine($"  加壳特征: {(packers.Count > 0 ? string.Join(", ", packers) : "无明显壳特征(可能未加壳)")}");

        // 熵估算 (前段 vs 整体)，判断加密程度
        double entropy = Entropy(data);
        sb.AppendLine($"  整体熵: {entropy:F3} ({(entropy > 7.5 ? "高度加密/压缩" : entropy > 6.5 ? "部分加密" : "低,大量明文")})");

        // 易语言特征
        bool isEpl = ContainsAscii(data, "krnln") || ContainsAscii(data, "系统核心支持库") || ContainsUtf16(data, "系统核心支持库");
        sb.AppendLine($"  易语言(EPL): {(isEpl ? "是" : "未检测到")}");

        // 关键词扫描 (ASCII + UTF16LE)
        sb.AppendLine("  功能关键词命中:");
        int totalHits = 0;
        foreach (var kw in Keywords)
        {
            int a = CountAscii(data, kw);
            int u = CountUtf16(data, kw);
            if (a + u > 0)
            {
                sb.AppendLine($"    {kw,-22} ASCII={a}  UTF16={u}");
                totalHits += a + u;
            }
        }
        if (totalHits == 0) sb.AppendLine("    (0命中 — 全部加密或无明文功能串)");
        sb.AppendLine($"  关键词总命中: {totalHits}");
    }

    static double Entropy(byte[] data)
    {
        int sample = Math.Min(data.Length, 4 * 1024 * 1024);
        var freq = new long[256];
        for (int i = 0; i < sample; i++) freq[data[i]]++;
        double e = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = (double)freq[i] / sample;
            e -= p * Math.Log(p, 2);
        }
        return e;
    }

    static bool ContainsAscii(byte[] data, string s) => CountAscii(data, s) > 0;
    static bool ContainsUtf16(byte[] data, string s) => CountUtf16(data, s) > 0;

    static int CountAscii(byte[] data, string s)
    {
        byte[] pat = Encoding.ASCII.GetBytes(s);
        return CountPattern(data, pat, 200);
    }
    static int CountUtf16(byte[] data, string s)
    {
        byte[] pat = Encoding.Unicode.GetBytes(s);
        return CountPattern(data, pat, 200);
    }

    static int CountPattern(byte[] data, byte[] pat, int cap)
    {
        if (pat.Length == 0) return 0;
        int count = 0;
        int end = data.Length - pat.Length;
        for (int i = 0; i <= end; i++)
        {
            if (data[i] != pat[0]) continue;
            int j = 1;
            for (; j < pat.Length; j++) if (data[i + j] != pat[j]) break;
            if (j == pat.Length) { count++; if (count >= cap) return count; i += pat.Length - 1; }
        }
        return count;
    }
}
