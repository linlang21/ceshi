using System;
using System.IO;
using System.Text;

class Verify
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var files = new[]
        {
            @"e:\ceshi\1\VVV.exe",
            @"e:\ceshi\2\V2.3.exe",
            @"e:\ceshi\3\燕云学习软件 V2.4.exe",
            @"e:\ceshi\6\AAA.exe",
        };
        var gbk = Encoding.GetEncoding("GBK");
        byte[] needle = gbk.GetBytes("传送");
        foreach (var f in files)
        {
            if (!File.Exists(f)) { Console.WriteLine($"缺失: {f}"); continue; }
            byte[] data = File.ReadAllBytes(f);
            Console.WriteLine($"==== {Path.GetFileName(f)} (size={data.Length}) 关键词\"传送\"前3处上下文 ====");
            int found = 0;
            for (int i = 0; i < data.Length - needle.Length && found < 3; i++)
            {
                bool m = true;
                for (int k = 0; k < needle.Length; k++) if (data[i + k] != needle[k]) { m = false; break; }
                if (!m) continue;
                found++;
                int s = Math.Max(0, i - 16), e = Math.Min(data.Length, i + 16);
                var sb = new StringBuilder();
                for (int j = s; j < e; j++)
                {
                    byte b = data[j];
                    sb.Append(b >= 0x20 && b < 0x7f ? (char)b : '.');
                }
                Console.WriteLine($"  off=0x{i:X8} ctx=[{sb}]");
            }
            if (found == 0) Console.WriteLine("  未找到");
        }
    }
}
