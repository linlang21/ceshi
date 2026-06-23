using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

class PackTool
{
    static string SourceDir = @"E:\ceshi\7\FridaGM";
    static string SourceCode = @"E:\ceshi\7\banyi\FridaGMTool.NetFx.cs";
    static string StubExe = @"E:\ceshi\7\FridaGMTool_build.exe";
    static string OutputExe = @"E:\ceshi\7\FridaGMTool_单文件版.exe";
    // 子进程超时（毫秒）：VMProtect / ConfuserEx 都可能长时间运行
    const int SUBPROC_TIMEOUT_MS = 5 * 60 * 1000;

    static bool PrepareVmprotectMode = false;
    static bool PackFinalMode = false;

    // 跟踪需要在程序退出时清理的临时目录
    static readonly List<string> TempDirs = new List<string>();

    static void Main(string[] args)
    {
        try
        {
            MainImpl(args);
        }
        finally
        {
            CleanupTempDirs();
        }
    }

    static void MainImpl(string[] args)
    {
        // 解析阶段参数，保留旧的位置参数兼容
        var positionalArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg == "--prepare-vmprotect")
                PrepareVmprotectMode = true;
            else if (arg == "--pack-final")
                PackFinalMode = true;
            else
                positionalArgs.Add(arg);
        }

        if (positionalArgs.Count >= 1) SourceDir = positionalArgs[0];
        if (positionalArgs.Count >= 2) OutputExe = positionalArgs[1];

        if (!Directory.Exists(SourceDir))
        {
            Console.WriteLine("源目录不存在: " + SourceDir);
            Environment.Exit(1);
        }

        // 阶段一：编译并准备 app.exe，供用户手动 VMProtect
        if (PrepareVmprotectMode)
        {
            PrepareForVmprotect();
            return;
        }

        // 阶段二/默认：编译外壳 + 打包单文件版
        // 若 app_protected.exe 不存在，会提示用户先执行阶段一和 VMProtect
        CompileStub();
        ObfuscateStub();
        string compressedDll = CompressDllIfAvailable();

        string finalExe = StubExe;
        string vmProtectedExe = Path.Combine(SourceDir, "app_protected.exe");
        if (File.Exists(vmProtectedExe))
        {
            finalExe = vmProtectedExe;
            Console.WriteLine("检测到 VMProtect 保护后的主程序: " + vmProtectedExe);
        }
        else if (PackFinalMode)
        {
            Console.WriteLine("[错误] --pack-final 模式要求存在 " + vmProtectedExe);
            Console.WriteLine("请确认已执行 VMProtect 生成 app_protected.exe。");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine("[提示] 未找到 app_protected.exe。如需 VMProtect 加壳，请先执行：");
            Console.WriteLine("  1. PackTool.exe --prepare-vmprotect");
            Console.WriteLine("  2. 用 VMProtect 处理生成的 app.exe");
            Console.WriteLine("  3. PackTool.exe --pack-final");
        }

        var filesToPack = new List<Tuple<string, string>>();
        filesToPack.Add(Tuple.Create(finalExe, "app.exe"));

        // 递归收集 SourceDir 下所有文件，保留相对路径作为打包条目名
        foreach (string file in Directory.GetFiles(SourceDir, "*", SearchOption.AllDirectories))
        {
            string rel = MakeRelativePath(SourceDir, file).Replace('\\', '/');
            string name = Path.GetFileName(file);
            if (name.StartsWith("FridaGMTool", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.Equals("app.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("app_protected.exe", StringComparison.OrdinalIgnoreCase))
                continue;
            // 排除 VMProtect 调试文件、项目文件和可执行文件（可执行文件只保留上面显式加入的 app.exe）
            if (name.EndsWith(".vmp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                continue;
            // 排除 frida-gadget 原始文件（已改名为 core.dll / core.config）
            if (name.Equals("frida-gadget.dll", StringComparison.OrdinalIgnoreCase) || name.Equals("frida-gadget.config", StringComparison.OrdinalIgnoreCase))
                continue;
            // 排除 Extreme Injector 配置文件（注入器已移除）
            if (name.Equals("settings.xml", StringComparison.OrdinalIgnoreCase))
                continue;
            // 排除开发文档（仅保留在源码目录）
            if (name.EndsWith("开发文档.md", StringComparison.OrdinalIgnoreCase))
                continue;
            // 排除运行时残留文件（不应打包）
            if (name.Equals("gm_cmd_result.txt", StringComparison.OrdinalIgnoreCase) || name.Equals("gm_tool.log", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            // 如果 UPX 可用，用压缩后的 core.dll 替代原始文件
            if (!string.IsNullOrEmpty(compressedDll) && rel.Equals("core.dll", StringComparison.OrdinalIgnoreCase))
            {
                filesToPack.Add(Tuple.Create(compressedDll, rel));
                continue;
            }
            filesToPack.Add(Tuple.Create(file, rel));
        }

        BuildPackedExe(filesToPack);

        try { File.Delete(StubExe); } catch { }
        try { if (!string.IsNullOrEmpty(compressedDll) && File.Exists(compressedDll)) File.Delete(compressedDll); } catch { }
        Console.WriteLine("单文件版已生成: " + OutputExe);
        Console.WriteLine("包含文件数: " + filesToPack.Count);
    }

    static string MakeRelativePath(string baseDir, string fullPath)
    {
        // 简单实现，避免依赖 Path.GetRelativePath（.NET Framework 缺失）
        string b = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string f = Path.GetFullPath(fullPath);
        if (f.StartsWith(b, StringComparison.OrdinalIgnoreCase))
            return f.Substring(b.Length);
        return Path.GetFileName(fullPath);
    }

    static string GetCscPath()
    {
        // 探测多版本 csc.exe；缺省回落到 v4.0.30319
        string[] candidates = new string[] {
            @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
            @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe",
            @"C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe"
        };
        foreach (string p in candidates)
            if (File.Exists(p)) return p;
        return candidates[0];
    }

    // 异步收集 stdout/stderr，避免缓冲填满死锁；带超时强杀
    static int RunProcess(ProcessStartInfo psi, int timeoutMs, out string stdout, out string stderr)
    {
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        using (var outDone = new ManualResetEvent(false))
        using (var errDone = new ManualResetEvent(false))
        using (var proc = new Process())
        {
            proc.StartInfo = psi;
            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) outDone.Set();
                else { lock (outBuf) outBuf.AppendLine(e.Data); }
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) errDone.Set();
                else { lock (errBuf) errBuf.AppendLine(e.Data); }
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            bool exited = proc.WaitForExit(timeoutMs);
            if (!exited)
            {
                try { proc.Kill(); } catch { }
                try { proc.WaitForExit(2000); } catch { }
                outDone.WaitOne(1000);
                errDone.WaitOne(1000);
                stdout = outBuf.ToString();
                stderr = errBuf.ToString() + "\n[PackTool] 子进程超时被终止: " + psi.FileName;
                return -1;
            }
            // 等待异步流读取完毕
            outDone.WaitOne(2000);
            errDone.WaitOne(2000);
            stdout = outBuf.ToString();
            stderr = errBuf.ToString();
            return proc.ExitCode;
        }
    }

    static void CompileStub()
    {
        Console.WriteLine("正在编译主程序...");
        string csc = GetCscPath();
        var psi = new ProcessStartInfo
        {
            FileName = csc,
            Arguments = string.Format("/target:winexe /platform:x64 /out:\"{0}\" \"{1}\"", StubExe, SourceCode)
        };
        string output, error;
        int code = RunProcess(psi, SUBPROC_TIMEOUT_MS, out output, out error);
        if (code != 0)
        {
            Console.WriteLine("编译失败:");
            Console.WriteLine(output);
            Console.WriteLine(error);
            Environment.Exit(1);
        }
    }

    static void ObfuscateStub()
    {
        // 自动调用 ConfuserEx 对主程序进行 .NET 混淆（如果已安装）
        string confuserPath = FindTool(new string[] {
            @"E:\ceshi\ConfuserEx\Confuser.CLI.exe",
            @"E:\ceshi\7\Confuser.CLI.exe",
            @"C:\Tools\ConfuserEx\Confuser.CLI.exe"
        });
        if (string.IsNullOrEmpty(confuserPath))
        {
            Console.WriteLine("[提示] 未找到 ConfuserEx，主程序未混淆。如需混淆，请将 Confuser.CLI.exe 放到 E:\\ceshi\\7\\ 目录。");
            return;
        }

        string workDir = Path.Combine(Path.GetTempPath(), "GMObfuscate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        TempDirs.Add(workDir);
        string inputExe = Path.Combine(workDir, "app.exe");
        string outputDir = Path.Combine(workDir, "output");
        Directory.CreateDirectory(outputDir);
        File.Copy(StubExe, inputExe, true);

        string crproj = Path.Combine(workDir, "obfuscate.crproj");
        // outputDir / workDir 写入 XML 前转义 XML 特殊字符
        string projXml = string.Format(
            "<project outputDir=\"{0}\" baseDir=\"{1}\" xmlns=\"http://confuser.codeplex.com\">\n" +
            "  <module path=\"app.exe\">\n" +
            "    <rule pattern=\"true\">\n" +
            "      <protection id=\"anti ildasm\" />\n" +
            "      <protection id=\"constants\" />\n" +
            "      <protection id=\"ctrl flow\" />\n" +
            "      <protection id=\"ref proxy\" />\n" +
            "      <protection id=\"resources\" />\n" +
            "    </rule>\n" +
            "  </module>\n" +
            "</project>",
            XmlEscape(outputDir), XmlEscape(workDir));
        File.WriteAllText(crproj, projXml, new UTF8Encoding(false));

        Console.WriteLine("正在使用 ConfuserEx 混淆主程序...");
        // 直接调用 Confuser.CLI.exe，避免 cmd /c "echo. | ..." 的引号转义陷阱
        var psi = new ProcessStartInfo
        {
            FileName = confuserPath,
            // 引号转义 crproj 路径（路径含双引号时会失败，但 Path.GetTempPath 不可能含双引号）
            Arguments = "-n \"" + crproj + "\"",
            WorkingDirectory = workDir,
            // ConfuserEx 在结束时 ReadKey()，需重定向 stdin 否则它会阻塞等键
            RedirectStandardInput = true
        };
        // RunProcess 内部会再设置其它 Redirect/UseShellExecute；这里只补 stdin
        string output, error;
        int code;
        // 自定义运行以同时关闭 stdin
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        using (var outDone = new ManualResetEvent(false))
        using (var errDone = new ManualResetEvent(false))
        using (var proc = new Process())
        {
            proc.StartInfo = psi;
            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) outDone.Set();
                else { lock (outBuf) outBuf.AppendLine(e.Data); }
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) errDone.Set();
                else { lock (errBuf) errBuf.AppendLine(e.Data); }
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            try { proc.StandardInput.Close(); } catch { }
            bool exited = proc.WaitForExit(SUBPROC_TIMEOUT_MS);
            if (!exited) { try { proc.Kill(); } catch { } }
            outDone.WaitOne(2000);
            errDone.WaitOne(2000);
            output = outBuf.ToString();
            error = errBuf.ToString();
            code = exited ? proc.ExitCode : -1;
        }
        // ConfuserEx 在 ReadKey 时可能崩溃，但输出文件已生成，通过文件存在性判断成功
        if (code != 0 && !File.Exists(Path.Combine(outputDir, "app.exe")))
        {
            Console.WriteLine("ConfuserEx 混淆失败，将使用未混淆的主程序：");
            Console.WriteLine(output);
            Console.WriteLine(error);
            return;
        }

        string obfuscated = Path.Combine(outputDir, "app.exe");
        if (File.Exists(obfuscated))
        {
            File.Copy(obfuscated, StubExe, true);
            Console.WriteLine("主程序混淆完成。");
        }
        else
        {
            Console.WriteLine("[警告] ConfuserEx 未生成混淆文件，将使用未混淆的主程序。");
        }
    }

    static string XmlEscape(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    static string CompressDllIfAvailable()
    {
        // 自动调用 UPX 压缩 core.dll（如果已安装）
        string upxPath = FindTool(new string[] {
            @"E:\ceshi\7\upx.exe",
            @"C:\Tools\upx.exe",
            @"C:\Program Files\UPX\upx.exe"
        });
        string dllPath = Path.Combine(SourceDir, "core.dll");
        if (string.IsNullOrEmpty(upxPath))
        {
            Console.WriteLine("[提示] 未找到 UPX，core.dll 未加壳压缩。如需压缩，请将 upx.exe 放到 E:\\ceshi\\7\\ 目录。");
            return null;
        }
        if (!File.Exists(dllPath))
        {
            Console.WriteLine("[提示] 未找到 core.dll，跳过 UPX 压缩。");
            return null;
        }

        string tempDll = Path.Combine(Path.GetTempPath(), "core_upx_" + Guid.NewGuid().ToString("N") + ".dll");
        Console.WriteLine("正在使用 UPX 压缩 core.dll...");
        var psi = new ProcessStartInfo
        {
            FileName = upxPath,
            Arguments = string.Format("--best -o \"{0}\" \"{1}\"", tempDll, dllPath)
        };
        string output, error;
        int code = RunProcess(psi, SUBPROC_TIMEOUT_MS, out output, out error);
        if (code != 0)
        {
            Console.WriteLine("UPX 压缩失败，将使用原始 core.dll：");
            Console.WriteLine(output);
            Console.WriteLine(error);
            return null;
        }
        Console.WriteLine("core.dll UPX 压缩完成。");
        return tempDll;
    }

    static void PrepareForVmprotect()
    {
        Console.WriteLine("===== 阶段一：准备 VMProtect 输入文件 =====");
        CompileStub();
        // 准备给 VMProtect 的 app.exe 先不做 .NET 混淆，避免 VMProtect 处理异常；
        // 如需 ConfuserEx，可在 VMProtect 之后、最终打包之前使用。

        string appExePath = Path.Combine(SourceDir, "app.exe");
        File.Copy(StubExe, appExePath, true);
        Console.WriteLine("已复制到: " + appExePath);

        // 自动调用 VMProtect 命令行（如果已安装 Ultimate 版）
        string vmprotectCon = FindTool(new string[] {
            @"E:\ceshi\7\VMProtect3.9.4\VMProtect_Con.exe",
            @"E:\ceshi\7\VMProtect\VMProtect_Con.exe",
            @"E:\ceshi\7\VMProtect_Con.exe"
        });
        string vmpProject = Path.Combine(SourceDir, "protect_app.vmp");
        string vmProtectedExe = Path.Combine(SourceDir, "app_protected.exe");

        if (!string.IsNullOrEmpty(vmprotectCon) && File.Exists(vmpProject))
        {
            Console.WriteLine("正在使用 VMProtect 命令行加壳...");
            var psi = new ProcessStartInfo
            {
                FileName = vmprotectCon,
                Arguments = string.Format("\"{0}\" \"{1}\" -pf \"{2}\"", appExePath, vmProtectedExe, vmpProject)
            };
            string output, error;
            int code = RunProcess(psi, SUBPROC_TIMEOUT_MS, out output, out error);
            Console.WriteLine(output);
            if (!string.IsNullOrEmpty(error)) Console.WriteLine(error);
            if (code != 0)
            {
                Console.WriteLine("[警告] VMProtect 命令行执行失败，请手动用 VMProtect GUI 处理 app.exe。");
                return;
            }
            if (File.Exists(vmProtectedExe))
            {
                Console.WriteLine("VMProtect 加壳完成: " + vmProtectedExe);
                Console.WriteLine();
                Console.WriteLine("可直接运行 PackTool.exe --pack-final 打包成单文件版。");
            }
            else
            {
                Console.WriteLine("[警告] VMProtect 未生成 app_protected.exe，请手动处理。");
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("未找到 VMProtect_Con.exe（Ultimate 版命令行），请手动执行 VMProtect：");
            Console.WriteLine("  1. 打开 VMProtect.exe");
            Console.WriteLine("  2. File -> Open -> 选择 " + vmpProject);
            Console.WriteLine("  3. 确认输入 app.exe，输出 app_protected.exe");
            Console.WriteLine("  4. 勾选 Memory Protection / Anti-Debug / Packing / 压缩 / 移除调试信息");
            Console.WriteLine("  5. Scripts 面板确认随机化节区名脚本已加载");
            Console.WriteLine("  6. 点击 Protect");
            Console.WriteLine();
            Console.WriteLine("VMProtect 完成后，再运行：PackTool.exe --pack-final");
        }
    }

    static string FindTool(string[] candidates)
    {
        foreach (string path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }

    static void CleanupTempDirs()
    {
        foreach (string dir in TempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
        TempDirs.Clear();
    }

    static void BuildPackedExe(List<Tuple<string, string>> filesToPack)
    {
        Console.WriteLine("正在打包单文件 exe...");
        using (var outStream = new FileStream(OutputExe, FileMode.Create, FileAccess.Write))
        {
            byte[] stubBytes = File.ReadAllBytes(StubExe);
            outStream.Write(stubBytes, 0, stubBytes.Length);

            long payloadStart = outStream.Position;

            outStream.Write(BitConverter.GetBytes(filesToPack.Count), 0, 4);

            foreach (var item in filesToPack)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(item.Item2);
                byte[] fileBytes = File.ReadAllBytes(item.Item1);
                outStream.Write(BitConverter.GetBytes(nameBytes.Length), 0, 4);
                outStream.Write(nameBytes, 0, nameBytes.Length);
                outStream.Write(BitConverter.GetBytes(fileBytes.Length), 0, 4);
                outStream.Write(fileBytes, 0, fileBytes.Length);
                Console.WriteLine("  已打包: " + item.Item2 + " (" + fileBytes.Length + " bytes)");
            }

            long payloadSizeLong = outStream.Position - payloadStart;
            // payload 长度仍存为 4 字节，超过 2GB 直接报错而非静默溢出
            if (payloadSizeLong > int.MaxValue)
            {
                Console.WriteLine("[错误] 打包内容超过 2GB，当前格式不支持。请减少打包内容或扩展协议。");
                Environment.Exit(2);
            }
            outStream.Write(BitConverter.GetBytes((int)payloadSizeLong), 0, 4);

            byte[] marker = Encoding.ASCII.GetBytes("SVCPACK___"); // 10 bytes to match reader
            outStream.Write(marker, 0, marker.Length);
        }
    }
}
