using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

class Extract
{
    static readonly string[] Files = new[]
    {
        @"e:\ceshi\1\VVV.exe",
        @"e:\ceshi\2\V2.3.exe",
        @"e:\ceshi\3\燕云学习软件 V2.4.exe",
        @"e:\ceshi\4\燕云十六科技学习_0.3.exe",
        @"e:\ceshi\5\进游戏内用界碑传送一下再启动我.exe",
        @"e:\ceshi\6\AAA.exe",
    };
    static readonly string[] KwGBK = new[]
    {
        "传送","无敌","倍攻","坐标","加速","定怪","去草","一击","必杀","无限","拾取","采集","隐身","体力","界碑","望月","注入","开","关","换皮","变身","皮肤","罪恶","开/关","关闭","开启",
    };
    static readonly string[] KwASCII = new[]
    {
        "yysls","yysl","read","write","open","proc","memory","buff","god","teleport","inject","hook","version","local",
    };

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        var sb = new StringBuilder();
        sb.AppendLine("# 1-5号 辅助真可读字符串提取");
        sb.AppendLine("# 关键词扫描+上下文可读过滤,排除随机碰撞。");
        sb.AppendLine();
        foreach (var f in Files)
        {
            if (!File.Exists(f)) continue;
            byte[] data = File.ReadAllBytes(f);
            var hits = new List<(int off, string text, string kw)>();
            foreach (var k in KwGBK)
            {
                byte[] p = gbk.GetBytes(k);
                for (int i = 0; i <= data.Length - p.Length; i++)
                {
                    bool m = true; for (int q = 0; q < p.Length; q++) if (data[i + q] != p[q]) { m = false; break; }
                    if (m && IsReadableCtx(data, i, p.Length, true)) hits.Add((i, k, "GBK"));
                }
            }
            foreach (var k in KwASCII)
            {
                byte[] p = Encoding.ASCII.GetBytes(k);
                for (int i = 0; i <= data.Length - p.Length; i++)
                {
                    bool m = true; for (int q = 0; q < p.Length; q++) if (data[i + q] != p[q]) { m = false; break; }
                    if (m && IsReadableCtx(data, i, p.Length, false)) hits.Add((i, k, "ASCII"));
                }
            }
            // 收集所有 >= 6 字符的可读 GBK 串
            var longStrs = new HashSet<string>();
            int i0 = 0; var cur = new StringBuilder(); int start = 0;
            for (int j = 0; j < data.Length; j++)
            {
                byte b = data[j];
                if (b >= 0x20 && b < 0x7f) { if (cur.Length == 0) start = j; cur.Append((char)b); }
                else
                {
                    if (cur.Length >= 6) longStrs.Add(cur.ToString());
                    cur.Clear();
                }
            }
            if (cur.Length >= 6) longStrs.Add(cur.ToString());

            sb.AppendLine($"## {Path.GetFileName(f)}  ({data.Length/1024/1024}MB)");
            sb.AppendLine($"关键词命中 {hits.Count} 次, 6+字符可读串 {longStrs.Count} 条");
            sb.AppendLine();
            sb.AppendLine("### 关键词命中片段(命中点前后20字节):");
            foreach (var h in hits.OrderBy(x => x.off).Take(60))
            {
                int s = Math.Max(0, h.off - 20), e = Math.Min(data.Length, h.off + 20);
                sb.AppendLine($"  0x{h.off:X8} [{h.kw}] ctx=\"{ExtractReadable(data, s, e)}\"");
            }
            sb.AppendLine();
            sb.AppendLine("### 含功能关键词的 6+ 字符可读串(去重):");
            var filtered = longStrs.Where(s => KwGBK.Any(k => s.Contains(k)) || KwASCII.Any(k => s.Contains(k, StringComparison.OrdinalIgnoreCase))).Take(150);
            foreach (var s in filtered) sb.AppendLine($"  {s}");
            sb.AppendLine();
            sb.AppendLine("### 随机抽 30 条普通可读串(判断文件类型):");
            foreach (var s in longStrs.OrderBy(_ => Guid.NewGuid()).Take(30)) sb.AppendLine($"  {s}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }
        File.WriteAllText(@"e:\ceshi\analysis_tool\strings_extracted.txt", sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine(sb.ToString());
        Console.WriteLine("写入 strings_extracted.txt");
    }

    static string ExtractReadable(byte[] d, int s, int e)
    {
        var cs = new StringBuilder();
        for (int j = s; j < e; j++) cs.Append(d[j] >= 0x20 && d[j] < 0x7f ? (char)d[j] : '.');
        return cs.ToString();
    }

    static bool IsReadableCtx(byte[] d, int off, int len, bool isGbk)
    {
        int s = Math.Max(0, off - 8), e = Math.Min(d.Length, off + len + 8);
        int readable = 0, total = e - s;
        for (int i = s; i < e; i++)
        {
            byte b = d[i];
            if (b >= 0x20 && b < 0x7f) readable++;
            else if (isGbk && b >= 0x81 && b <= 0xFE) readable++;
        }
        return total > 0 && readable * 10 >= total * 6;
    }
}
