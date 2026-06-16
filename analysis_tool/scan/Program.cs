using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Scan
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

    // 短关键词(2-3字节 GBK / 4字节 ASCII) - 任何真实功能模块都会使用
    static readonly string[] KwGBK = new[]
    {
        "传送","无敌","倍攻","坐标","加速","定怪","去草","一击","必杀","无限","拾取","采集","隐身","体力","界碑","望月","注入","开","关","开/关",
    };
    static readonly string[] KwASCII = new[]
    {
        "yysls","yysl","read","write","open","proc","memory","buff","god","godmode","teleport","inject","hook","key","version","local",
    };

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding("GBK");
        Console.WriteLine("统计:对每个文件,扫描全部关键词。'真命中'要求关键词前后各有2-3字节为可读字符(0x20-0x7E 或 GBK连续字)。");
        Console.WriteLine("只统计真命中(排除纯随机碰撞)。\n");
        Console.WriteLine($"{"文件",-32} {"GBK真命中",12} {"ASCII真命中",14} {"总长字符串数(>=6字符可读)",30}");
        Console.WriteLine(new string('-', 95));
        foreach (var f in Files)
        {
            if (!File.Exists(f)) { Console.WriteLine($"{Path.GetFileName(f),-32} 缺失"); continue; }
            byte[] data = File.ReadAllBytes(f);
            int gbkHits = 0, ascHits = 0, longStr = 0;
            foreach (var k in KwGBK)
            {
                byte[] p = gbk.GetBytes(k);
                for (int i = 0; i <= data.Length - p.Length; i++)
                {
                    bool m = true; for (int q = 0; q < p.Length; q++) if (data[i + q] != p[q]) { m = false; break; }
                    if (m && IsReadableCtx(data, i, p.Length, true)) { gbkHits++; i += p.Length - 1; }
                }
            }
            foreach (var k in KwASCII)
            {
                byte[] p = Encoding.ASCII.GetBytes(k);
                for (int i = 0; i <= data.Length - p.Length; i++)
                {
                    bool m = true; for (int q = 0; q < p.Length; q++) if (data[i + q] != p[q]) { m = false; break; }
                    if (m && IsReadableCtx(data, i, p.Length, false)) { ascHits++; i += p.Length - 1; }
                }
            }
            // 寻找>=6字符连续可读 GBK 串
            var cur = new StringBuilder();
            int max = 0, count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                bool ok = data[i] >= 0x20 && data[i] < 0x7f;
                if (ok) cur.Append((char)data[i]);
                else
                {
                    if (cur.Length >= 6) { count++; if (cur.Length > max) max = cur.Length; }
                    cur.Clear();
                }
            }
            if (cur.Length >= 6) count++;
            longStr = count;
            Console.WriteLine($"{Path.GetFileName(f),-32} {gbkHits,12} {ascHits,14} {count,12} 串(最长{max}字符)");
        }
    }

    // 上下文:命中点前后8字节内,至少60%是可读ASCII 或 GBK首字节
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
