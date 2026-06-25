using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FridaGMTool
{
    class ThinScrollPanel : Panel
    {
        [DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr hWnd, int nBar, ref SCROLLINFO lpsi);
        [DllImport("user32.dll")] static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        const int SB_VERT = 1;
        const int SIF_RANGE = 1;
        const int SIF_PAGE = 2;
        const int SIF_POS = 4;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_VSCROLL = 0x0115;
        const int SB_LINEUP = 0;
        const int SB_LINEDOWN = 1;
        const int SB_PAGEUP = 2;
        const int SB_PAGEDOWN = 3;
        const int SB_THUMBTRACK = 5;

        [StructLayout(LayoutKind.Sequential)]
        struct SCROLLINFO
        {
            public int cbSize; public uint fMask; public int nMin; public int nMax;
            public uint nPage; public int nPos; public int nTrackPos;
        }

        int thumbTop = 0, thumbH = 0;
        bool dragging = false;
        int dragStartY, dragStartPos;

        public ThinScrollPanel()
        {
            AutoScroll = true;
            DoubleBuffered = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ShowScrollBar(Handle, SB_VERT, false);
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            ShowScrollBar(Handle, SB_VERT, false);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ShowScrollBar(Handle, SB_VERT, false);
        }

        void UpdateThumb()
        {
            var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_RANGE | SIF_PAGE | SIF_POS };
            GetScrollInfo(Handle, SB_VERT, ref si);
            int range = si.nMax - si.nMin + 1;
            if (range <= 0 || si.nPage <= 0) { thumbTop = 0; thumbH = 0; return; }
            int trackH = Height - 4;
            thumbH = Math.Max(18, (int)((float)si.nPage / range * trackH));
            thumbTop = 2 + (int)((float)(si.nPos - si.nMin) / (range - si.nPage) * (trackH - thumbH));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            UpdateThumb();
            if (thumbH <= 0) return;
            using (var b = new SolidBrush(Color.FromArgb(180, 180, 180)))
                e.Graphics.FillRectangle(b, Width - 5, thumbTop, 3, thumbH);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int dy = e.Y - dragStartY;
            var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_RANGE | SIF_PAGE | SIF_POS };
            GetScrollInfo(Handle, SB_VERT, ref si);
            int trackH = Height - 4;
            int range = si.nMax - si.nMin - (int)si.nPage + 1;
            if (range <= 0) return;
            int newPos = dragStartPos + (int)((float)dy / (trackH) * range);
            newPos = Math.Max(si.nMin, Math.Min(si.nMax - (int)si.nPage + 1, newPos));
            AutoScrollPosition = new Point(0, newPos);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X >= Width - 6 && thumbH > 0)
            {
                dragging = true;
                dragStartY = e.Y;
                var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_POS };
                GetScrollInfo(Handle, SB_VERT, ref si);
                dragStartPos = si.nPos;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_VSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                ShowScrollBar(Handle, SB_VERT, false);
                Invalidate();
            }
        }
    }

    class GMForm : Form
    {
        // Paths
        static string ToolDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static string WorkDir = ToolDir;
        static string PayloadJs = Path.Combine(WorkDir, "gm_payload.js");
        static string ConfigFile = Path.Combine(ToolDir, "config.txt");
        static string BuffConfigFile = Path.Combine(ToolDir, "buff_config.txt");
        static readonly int[] OutfitIds = new int[] { 1570003, 400148, 1470000, 30335, 310218, 310219, 310221, 30524, 310224, 30384, 30385, 30386, 30387, 30388, 30389, 30390, 30391, 30392, 30393, 30394, 30395, 30396, 30397, 30398, 50103, 50104, 80000, 109623, 1007004, 1030000, 1230011, 1230012, 1240000, 1240001, 1240002, 1240003, 1240004, 1460000, 1460001, 1460002, 1460003, 1460004, 1460005, 1460006, 1460007, 1460008, 1460009, 1460010, 1460021, 1460024, 1460025, 1460026, 1460027, 1460028, 1460043, 1460045, 1460046, 1460047, 1700000, 1760001, 1330001, 1450010, 1450011, 1420001, 1310000, 1250016, 1250002, 1010007, 1100000, 1170007, 1471009, 1740002, 1475007, 1820000, 1920001, 1660020, 1660021, 1500120, 1400000, 1400004, 1770000, 1500170, 1540010, 50014, 1290000, 1290001, 1410050, 1410051, 1360005, 1560000, 1750000, 1050001, 1050013, 1040006, 1040008, 1020013, 1020014, 1020015, 1020017, 1020018, 1010000, 1070032, 1070052, 1080003, 1080015, 1080021, 1080054, 1080067, 1080073, 1080074, 910324, 910325, 910328, 910329, 910330, 910332, 910446, 960001, 440020, 440093, 450009, 310211 };
        static readonly string[] OutfitNames = new string[] {
            "变成猪头", "瞬间秒杀一切", "江叔", "发光特效", "变小", "变小近距", "变小可操作", "变大", "滑冰", "穿心者", "一刀", "天鹰", "沉睡道士", "木鹰", "郑厄", "地煞", "龙王", "虚王", "十七", "蛇医", "河主", "十七 (P2)", "道君", "傀儡师", "讲经人 (男)", "讲经人 (女)", "安西军", "全身发光", "刺客", "漂流者套装", "少冬瓜", "变身 L", "发疯 - 狗", "发疯 - 驴", "发疯 - 鹿", "发疯 - 熊", "发疯 - 鸟", "人间仙", "无相", "花瓣挽歌", "春回大地", "红幕", "平静生活", "换装 6", "换装 7", "沧海威严", "九尾传承", "雅乐风华", "笔", "大鹅", "公鸡", "土拨鼠", "驴道人的驴", "冲天炮", "陨石", "独眼老鼠", "爆炸桶", "踢鞠小人", "浴衣", "大宋提刑官", "少年猪脚", "高冬瓜人", "矮冬瓜人", "绿衣公公", "衣服女", "火眼金睛-特效", "移动麻布袋", "战斗麻布袋", "美女", "白衣小哥", "兵", "老虎", "苹果精", "玫瑰花快速", "赵二", "超级战斗鸡", "超级战斗鹅", "巨子青", "无头鬼新娘", "有头鬼新娘", "女npc", "小蓝猫", "大猫猫", "大鹅", "机械蚂蚁", "好看女npc", "好看女npc2", "可以取消的狼王", "可以取消的鹰", "韩通", "韩守谅", "mini苹果", "马儿", "台柱子", "红色短裤", "黄色短裤", "白发", "面具", "白发面具", "盾牌", "方天画戟", "鹿角", "超好看特效", "超好看特效2", "武器带水特效", "角面具", "萤火虫特效", "红叶特效", "欲火焚身特效", "白鹤特效", "水汽特效", "地狱火特效", "地狱火白特效", "地狱武器附火", "头顶花瓣", "第一人称", "花瓣墙特效", "光圈特效", "帽子小哥", "闪电特效", "闪电特效2", "脚底白云特效", "不良人"
        };

        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const long GLOBAL_BASE_OFFSET = 0x07C04698;
        const string DefaultMmfName = "Global\\WinSvcSharedMem";
        const string CurrentVersionText = "1.1";
        const string DefaultNoticeText = "当前版本：v1.1\n测试版本，仅限当前使用。";
        const int ManifestLoadTimeoutMs = 5000;
        const int ManifestCacheFreshHours = 6;
        const SecurityProtocolType Tls11SecurityProtocol = (SecurityProtocolType)768;
        const SecurityProtocolType Tls12SecurityProtocol = (SecurityProtocolType)3072;
        static readonly Version CurrentVersion = ParseVersionText(CurrentVersionText);
        const string EmbeddedManifestUrl = "https://fz.wk110.top/fridagm/version_manifest.txt";
        static readonly string LocalVersionManifestFile = Path.Combine(ToolDir, "version_manifest.txt");
        static readonly string VersionManifestSourceFile = Path.Combine(ToolDir, "version_manifest_url.txt");
        static readonly string SharedManifestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FridaGM");
        static readonly string SharedVersionManifestFile = Path.Combine(SharedManifestDir, "version_manifest.txt");
        static readonly TimeSpan ManifestCacheFreshWindow = TimeSpan.FromHours(ManifestCacheFreshHours);
        static VersionManifestInfo StartupManifest = CreateDefaultManifest();
        static bool modernTlsConfigured;

        class VersionManifestInfo
        {
            public string LatestVersion;
            public string MinSupportedVersion;
            public string Notice;
            public string DownloadUrl;
            public string BlockMessage;
            public bool BlockOnManifestError;
            public bool RemoteLoaded;
            public string ManifestSource;
            public string ManifestError;
        }

        sealed class TimeoutWebClient : WebClient
        {
            readonly int timeoutMs;

            public TimeoutWebClient(int timeoutMs)
            {
                this.timeoutMs = timeoutMs;
                Encoding = Encoding.UTF8;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                request.Timeout = timeoutMs;
                HttpWebRequest httpRequest = request as HttpWebRequest;
                if (httpRequest != null)
                {
                    httpRequest.ReadWriteTimeout = timeoutMs;
                    httpRequest.UserAgent = "FridaGMTool/" + CurrentVersionText;
                }
                return request;
            }
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesWritten);
        // 内嵌注入所需API
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);
        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
        [DllImport("ntdll.dll")]
        static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, byte[] processInformation, int processInformationLength, out int returnLength);
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;
        const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint INFINITE = 0xFFFFFFFF;

        struct MemorySpeedEntry
        {
            public string Name;
            public long[] Offsets;
            public float Value;
        }

        MemorySpeedEntry[] memorySpeedEntries = new MemorySpeedEntry[]
        {
            new MemorySpeedEntry { Name = "新速1", Offsets = new long[] { 0x68, 0x0, 0x40, 0x40, 0x90, 0x230 }, Value = 1.6f },
            new MemorySpeedEntry { Name = "新速2", Offsets = new long[] { 0x624, 0x2E0, 0x30, 0x0, 0x60, 0x230 }, Value = 1.6f },
            new MemorySpeedEntry { Name = "世界速度2", Offsets = new long[] { 0x68, 0x40, 0xA8, 0x120, 0xB8, 0x230 }, Value = 1.6f },
            new MemorySpeedEntry { Name = "单速", Offsets = new long[] { 0x7E8, 0x18, 0x250, 0x98, 0x20, 0x2B8, 0x28, 0x58, 0x2E0 }, Value = 3.0f },
            new MemorySpeedEntry { Name = "人高速", Offsets = new long[] { 0x68, 0x40, 0x8, 0x448, 0x230 }, Value = 3.0f },
            new MemorySpeedEntry { Name = "世界1", Offsets = new long[] { 0x68, 0x218, 0x98, 0x40, 0x90, 0x230 }, Value = 1.6f },
        };

        static string GameSubPath = @"yysls_fast\Engine\Binaries\Win64r";
        static string GameSubPathAlt = @"yysls_medium\Engine\Binaries\Win64r";
        static string GameExeName = "yysls.exe";

        Label lblStatus;
        Panel grpGM;
        Panel tabGM;
        ThinScrollPanel tabInit;
        Button btnBrowse;
        Button btnStartGame, btnInject;
        Button btnRefresh;
        Button btnStamina;
        Button btnAtkBuff, btnDefBuff, btnMinBuff, btnStealthFlags;
        Button btnYyAutoLoot, btnYyRecover;
        Button btnLoopBuff, btnLoopLoot, btnLoopRecover, btnLoopDefense;
        Button btnGatherBuff, btnAuxBuff, btnUnknownBuff;
        Button btnRhythmGame, btnChessWin;
        CheckBox chkGod, chkInvis, chkNpcDumb, chkSuperDodge, chkOneHit;
        bool suppressCheckboxEvents = true;
        Button btnStaminaDive, btnStaminaEmpty, btnStaminaResetAll, btnPitchPot, btnApplyAtkMul, btnResetAtkMul, btnApplyDialogSpeed, btnResetDialogSpeed, btnCutsceneKill, btnApplyAtkSpeed, btnResetAtkSpeed;
        ComboBox cmbAtkMul, cmbAtkSpeed;
        TextBox txtDialogSpeed;
        TextBox txtCoordInput;
        Label lblNotice;
        System.Windows.Forms.Timer readyPollTimer;
        System.Windows.Forms.Timer injectionDiagTimer;
        System.Windows.Forms.Timer commandResultTimer;

        string gameRootPath = "";
        string gameBinPath = "";
        bool isReady = false;
        bool gameLaunched = false;
        bool commandPending = false;

        string CmdFile = "";
        string GadgetLogFile = "";
        string AuxLogFile = "";
        string CmdResultFile = "";
        string ToolResultFile = Path.Combine(ToolDir, "gm_tool_result.txt");
        string ToolResultCompatFile = Path.Combine(WorkDir, "gm_tool_result.txt");
        string UnifiedLogFile = Path.Combine(ToolDir, "gm_tool.log");

        // 共享内存通信
        string MMF_NAME = DefaultMmfName; // 默认回退名；优先读取运行时随机配置
        const int MMF_SIZE = 262144; // 256KB
        const int MMF_RESULT_OFFSET = 131072; // 128KB
        const int MMF_HALF_SIZE = 131072;
        MemoryMappedFile mmf = null;
        MemoryMappedViewAccessor mmfAccessor = null;
        int mmfSeq = 0;
        int mmfResultSeq = 0;

        // 随机化通信文件名 (使用单例 Random 避免同一时钟刻度产生相同种子)
        static readonly Random _sharedRandom = new Random();
        static readonly object _randLock = new object();
        string RandomHex8() { lock (_randLock) { return _sharedRandom.Next(0x10000000, int.MaxValue).ToString("x8"); } }
        string RandFileName(string ext) { return "svc_" + RandomHex8() + ext; }
        string RandMmfName() { return "Global\\" + RandomHex8() + RandomHex8(); }

        static void SafeDisposeProcess(Process process)
        {
            if (process == null) return;
            try { process.Dispose(); }
            catch (Exception ex) { Debug.WriteLine("Dispose process failed: " + ex.Message); }
        }

        static void SafeDisposeProcesses(IEnumerable<Process> processes)
        {
            if (processes == null) return;
            foreach (var process in processes) SafeDisposeProcess(process);
        }

        static void SafeDeleteFile(string path, string context)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(context + ": " + ex.Message);
            }
        }

        // Lua 字符串转义：转义反斜杠、引号、换行、回车以及右方括号防止 [[..]] 逃逸
        static string EscapeLuaString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\0': sb.Append("\\0"); break;
                    case ']': sb.Append("\\]"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // 生成并保存随机化配置
        void GenerateRandomCommConfig()
        {
            string cfgPath = Path.Combine(ToolDir, "svc.cfg");
            string mmfName = RandMmfName();
            string cmdName = RandFileName(".dat");
            string resultName = RandFileName(".dat");
            string logName = RandFileName(".log");
            string cfg = mmfName + "\n" + cmdName + "\n" + resultName + "\n" + logName;
            File.WriteAllText(cfgPath, cfg, new UTF8Encoding(false));
            MMF_NAME = mmfName;
            CmdFile = Path.Combine(ToolDir, cmdName);
            CmdResultFile = Path.Combine(ToolDir, resultName);
            UnifiedLogFile = Path.Combine(ToolDir, logName);
            GadgetLogFile = UnifiedLogFile;
            AuxLogFile = UnifiedLogFile;
            ToolResultFile = Path.Combine(ToolDir, "gm_tool_result.txt");
            ToolResultCompatFile = Path.Combine(WorkDir, "gm_tool_result.txt");
        }

        static Version ParseVersionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new Version(0, 0, 0, 0);
            string cleaned = text.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(1).Trim();

            MatchCollection matches = Regex.Matches(cleaned, @"\d+");
            int[] parts = new int[] { 0, 0, 0, 0 };
            for (int i = 0; i < matches.Count && i < parts.Length; i++)
            {
                int value;
                if (int.TryParse(matches[i].Value, out value)) parts[i] = value;
            }

            try { return new Version(parts[0], parts[1], parts[2], parts[3]); }
            catch { return new Version(0, 0, 0, 0); }
        }

        static void EnsureModernTls()
        {
            if (modernTlsConfigured) return;
            try
            {
                ServicePointManager.SecurityProtocol |= Tls11SecurityProtocol | Tls12SecurityProtocol;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Enable TLS 1.1/1.2 failed: " + ex.Message);
            }
            modernTlsConfigured = true;
        }

        static void CleanupLegacyManifestFiles()
        {
            string[] legacyPaths = new string[]
            {
                LocalVersionManifestFile,
                VersionManifestSourceFile
            };

            foreach (string path in legacyPaths)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Cleanup legacy manifest file failed: " + ex.Message);
                }
            }
        }

        static VersionManifestInfo CreateDefaultManifest()
        {
            return new VersionManifestInfo
            {
                LatestVersion = CurrentVersionText,
                MinSupportedVersion = CurrentVersionText,
                Notice = DefaultNoticeText,
                DownloadUrl = "",
                BlockMessage = "当前版本已停用，请更新到最新版后再使用。",
                BlockOnManifestError = false,
                RemoteLoaded = false,
                ManifestSource = EmbeddedManifestUrl,
                ManifestError = ""
            };
        }

        static string NormalizeManifestText(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("\\n", Environment.NewLine).Trim();
        }

        static string LoadManifestTextFromSource(string source)
        {
            Uri uri;
            if (Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                {
                    using (var client = new TimeoutWebClient(ManifestLoadTimeoutMs))
                        return client.DownloadString(uri);
                }
                if (uri.Scheme == Uri.UriSchemeFile)
                    return File.ReadAllText(uri.LocalPath, new UTF8Encoding(false));
            }
            return File.ReadAllText(source, new UTF8Encoding(false));
        }

        static VersionManifestInfo ParseManifestText(string text, string source, bool remoteLoaded)
        {
            VersionManifestInfo info = CreateDefaultManifest();
            info.RemoteLoaded = remoteLoaded;
            info.ManifestSource = source;

            if (string.IsNullOrWhiteSpace(text)) return info;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                int index = line.IndexOf('=');
                if (index <= 0) continue;

                string key = line.Substring(0, index).Trim().ToLowerInvariant();
                string value = line.Substring(index + 1).Trim();
                switch (key)
                {
                    case "latest_version":
                        info.LatestVersion = value;
                        break;
                    case "min_supported_version":
                        info.MinSupportedVersion = value;
                        break;
                    case "notice":
                        info.Notice = NormalizeManifestText(value);
                        break;
                    case "download_url":
                        info.DownloadUrl = value;
                        break;
                    case "block_message":
                        info.BlockMessage = NormalizeManifestText(value);
                        break;
                    case "block_on_manifest_error":
                        bool blockOnError;
                        if (bool.TryParse(value, out blockOnError)) info.BlockOnManifestError = blockOnError;
                        break;
                }
            }
            return info;
        }

        static VersionManifestInfo MergeManifest(VersionManifestInfo baseInfo, VersionManifestInfo overrideInfo)
        {
            VersionManifestInfo merged = CreateDefaultManifest();
            VersionManifestInfo source = baseInfo ?? CreateDefaultManifest();
            merged.LatestVersion = source.LatestVersion;
            merged.MinSupportedVersion = source.MinSupportedVersion;
            merged.Notice = source.Notice;
            merged.DownloadUrl = source.DownloadUrl;
            merged.BlockMessage = source.BlockMessage;
            merged.BlockOnManifestError = source.BlockOnManifestError;
            merged.RemoteLoaded = source.RemoteLoaded;
            merged.ManifestSource = source.ManifestSource;
            merged.ManifestError = source.ManifestError;

            if (overrideInfo == null) return merged;
            if (!string.IsNullOrWhiteSpace(overrideInfo.LatestVersion)) merged.LatestVersion = overrideInfo.LatestVersion;
            if (!string.IsNullOrWhiteSpace(overrideInfo.MinSupportedVersion)) merged.MinSupportedVersion = overrideInfo.MinSupportedVersion;
            if (!string.IsNullOrWhiteSpace(overrideInfo.Notice)) merged.Notice = overrideInfo.Notice;
            if (!string.IsNullOrWhiteSpace(overrideInfo.DownloadUrl)) merged.DownloadUrl = overrideInfo.DownloadUrl;
            if (!string.IsNullOrWhiteSpace(overrideInfo.BlockMessage)) merged.BlockMessage = overrideInfo.BlockMessage;
            merged.BlockOnManifestError = overrideInfo.BlockOnManifestError;
            merged.RemoteLoaded = overrideInfo.RemoteLoaded;
            if (!string.IsNullOrWhiteSpace(overrideInfo.ManifestSource)) merged.ManifestSource = overrideInfo.ManifestSource;
            merged.ManifestError = overrideInfo.ManifestError;
            return merged;
        }

        static VersionManifestInfo LoadStartupManifest()
        {
            VersionManifestInfo manifest = CreateDefaultManifest();
            try
            {
                string remoteText = LoadManifestTextFromSource(EmbeddedManifestUrl);
                manifest = MergeManifest(manifest, ParseManifestText(remoteText, EmbeddedManifestUrl, true));
                manifest.ManifestSource = EmbeddedManifestUrl;
                manifest.ManifestError = "";
                PersistSharedVersionManifest(manifest);
            }
            catch (Exception ex)
            {
                VersionManifestInfo cachedManifest;
                string cacheFailure;
                if (TryLoadFreshCachedManifest(out cachedManifest, out cacheFailure))
                    return cachedManifest;

                manifest.ManifestSource = EmbeddedManifestUrl;
                manifest.ManifestError = ex.Message + (string.IsNullOrWhiteSpace(cacheFailure) ? "" : "\n缓存回退失败: " + cacheFailure);
                manifest.BlockOnManifestError = true;
            }
            return manifest;
        }

        static bool TryLoadFreshCachedManifest(out VersionManifestInfo manifest, out string failureReason)
        {
            manifest = null;
            failureReason = "";
            try
            {
                if (!File.Exists(SharedVersionManifestFile))
                {
                    failureReason = "未找到共享缓存";
                    return false;
                }

                DateTime cacheWriteTimeUtc = File.GetLastWriteTimeUtc(SharedVersionManifestFile);
                TimeSpan cacheAge = DateTime.UtcNow - cacheWriteTimeUtc;
                if (cacheAge < TimeSpan.Zero) cacheAge = TimeSpan.Zero;
                if (cacheAge > ManifestCacheFreshWindow)
                {
                    failureReason = "共享缓存已过期，最后更新时间距今 " + Math.Round(cacheAge.TotalMinutes) + " 分钟";
                    return false;
                }

                string cachedText = File.ReadAllText(SharedVersionManifestFile, new UTF8Encoding(false));
                manifest = MergeManifest(CreateDefaultManifest(), ParseManifestText(cachedText, SharedVersionManifestFile, false));
                manifest.ManifestSource = SharedVersionManifestFile + " (fresh-cache)";
                manifest.ManifestError = "";
                return true;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                return false;
            }
        }

        static void PersistSharedVersionManifest(VersionManifestInfo manifest)
        {
            try
            {
                if (manifest == null) return;
                Directory.CreateDirectory(SharedManifestDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 自动生成的共享版本清单");
                sb.AppendLine("latest_version=" + (string.IsNullOrWhiteSpace(manifest.LatestVersion) ? CurrentVersionText : manifest.LatestVersion));
                sb.AppendLine("min_supported_version=" + (string.IsNullOrWhiteSpace(manifest.MinSupportedVersion) ? CurrentVersionText : manifest.MinSupportedVersion));
                sb.AppendLine("notice=" + (string.IsNullOrWhiteSpace(manifest.Notice) ? DefaultNoticeText : manifest.Notice).Replace(Environment.NewLine, "\\n"));
                sb.AppendLine("download_url=" + (manifest.DownloadUrl ?? ""));
                sb.AppendLine("block_message=" + (string.IsNullOrWhiteSpace(manifest.BlockMessage) ? "当前版本已停用，请更新到最新版后再使用。" : manifest.BlockMessage).Replace(Environment.NewLine, "\\n"));
                sb.AppendLine("block_on_manifest_error=" + manifest.BlockOnManifestError.ToString().ToLowerInvariant());
                File.WriteAllText(SharedVersionManifestFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Persist shared version manifest failed: " + ex.Message);
            }
        }

        static bool EnsureVersionAllowed(VersionManifestInfo manifest)
        {
            VersionManifestInfo effectiveManifest = manifest ?? CreateDefaultManifest();
            if (!string.IsNullOrWhiteSpace(effectiveManifest.ManifestError) && effectiveManifest.BlockOnManifestError)
            {
                MessageBox.Show(
                    "版本清单读取失败，当前配置要求停止启动。"
                    + "\n\n错误信息： " + effectiveManifest.ManifestError
                    + "\n清单来源： " + effectiveManifest.ManifestSource,
                    "版本校验失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            Version minSupported = ParseVersionText(effectiveManifest.MinSupportedVersion);
            if (CurrentVersion.CompareTo(minSupported) >= 0) return true;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.IsNullOrWhiteSpace(effectiveManifest.BlockMessage) ? "当前版本已停用，请更新到最新版后再使用。" : effectiveManifest.BlockMessage);
            sb.AppendLine();
            sb.AppendLine("当前版本：v" + CurrentVersionText);
            sb.AppendLine("最低可用：v" + effectiveManifest.MinSupportedVersion);
            if (!string.IsNullOrWhiteSpace(effectiveManifest.LatestVersion))
                sb.AppendLine("最新版本：v" + effectiveManifest.LatestVersion);
            if (!string.IsNullOrWhiteSpace(effectiveManifest.DownloadUrl))
                sb.AppendLine("更新地址：" + effectiveManifest.DownloadUrl);
            MessageBox.Show(sb.ToString().TrimEnd(), "版本已停用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        static string BuildAnnouncementText(VersionManifestInfo manifest)
        {
            VersionManifestInfo effectiveManifest = manifest ?? CreateDefaultManifest();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("当前版本：v" + CurrentVersionText);
            if (!string.IsNullOrWhiteSpace(effectiveManifest.Notice))
            {
                string normalized = effectiveManifest.Notice.Trim();
                if (!normalized.StartsWith("当前版本：", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(normalized);
                }
                else
                {
                    string[] lines = normalized.Replace("\r\n", "\n").Split('\n');
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        public GMForm()
        {
            Text = "FridaGM 工具 v" + CurrentVersionText;
            Size = new Size(560, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 4;

            // === GM 命令 ===
            grpGM = new Panel { Location = new Point(8, y), Size = new Size(536, 424), BackColor = Color.White };
            const int rowStep = 34;
            const int sectionGap = 8;
            chkGod = new CheckBox { Text = "无敌", Size = new Size(110, 26) };
            chkGod.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkGod.Text, BuildCombatExperimentLua(chkGod.Checked ? "god" : "god_off")); };
            btnStamina = new Button { Text = "锁体力消耗", Size = new Size(110, 26) };
            btnStamina.Click += (s, e) => SendExperimentCommand("锁体力消耗", BuildCombatExperimentLua("stamina_lock"));
            btnStaminaDive = new Button { Text = "无限潜水资源", Size = new Size(110, 26) };
            btnStaminaDive.Click += (s, e) => SendExperimentCommand("无限潜水资源", BuildCombatExperimentLua("stamina_dive"));
            chkInvis = new CheckBox { Text = "隐身", Size = new Size(110, 26) };
            chkInvis.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkInvis.Text, BuildCombatExperimentLua(chkInvis.Checked ? "invis" : "invis_off")); };

            btnStaminaEmpty = new Button { Text = "清空战斗资源", Size = new Size(110, 26) };
            btnStaminaEmpty.Click += (s, e) => SendExperimentCommand("清空战斗资源", BuildCombatExperimentLua("stamina_empty"));
            btnStaminaResetAll = new Button { Text = "恢复体力设置", Size = new Size(110, 26) };
            btnStaminaResetAll.Click += (s, e) => SendExperimentCommand("恢复体力设置", BuildCombatExperimentLua("stamina_reset_all"));

            chkNpcDumb = new CheckBox { Text = "NPC变笨", Size = new Size(110, 26) };
            chkNpcDumb.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkNpcDumb.Text, BuildYyLaoLiuLua(chkNpcDumb.Checked ? "yy_npcdumb" : "yy_npcdumb_off")); };
            chkSuperDodge = new CheckBox { Text = "超级闪避", Size = new Size(110, 26) };
            chkSuperDodge.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkSuperDodge.Text, BuildCombatExperimentLua(chkSuperDodge.Checked ? "super_dodge" : "super_dodge_off")); };

            btnAtkBuff = new Button { Text = "攻击Buff", Size = new Size(110, 26) };
            btnAtkBuff.Click += (s, e) => SendExperimentCommand("攻击Buff整合", BuildCombatExperimentLua("atkbuff_combo"));
            btnDefBuff = new Button { Text = "防御Buff", Size = new Size(110, 26) };
            btnDefBuff.Click += (s, e) => SendExperimentCommand("防御Buff", BuildCombatExperimentLua("defbuff"));
            btnMinBuff = new Button { Text = "最小Buff", Size = new Size(110, 26), BackColor = Color.LightGreen };
            btnMinBuff.Click += (s, e) => SendExperimentCommand("最小Buff", BuildLoopFeatureLua("minimal_buff"));

            btnGatherBuff = new Button { Text = "采集Buff", Size = new Size(110, 26) };
            btnGatherBuff.Click += (s, e) => SendExperimentCommand("采集Buff", BuildLoopFeatureLua("gather_buff"));

            btnAuxBuff = new Button { Text = "辅助Buff", Size = new Size(110, 26) };
            btnAuxBuff.Click += (s, e) => SendExperimentCommand("辅助Buff", BuildLoopFeatureLua("aux_buff"));
            btnUnknownBuff = new Button { Text = "未知Buff", Size = new Size(110, 26) };
            btnUnknownBuff.Click += (s, e) => SendExperimentCommand("未知Buff", BuildLoopFeatureLua("unknown_buff"));
            var btnRemoveAllBuffs = new Button { Text = "移除全部Buff", Size = new Size(110, 26) };
            btnRemoveAllBuffs.Click += (s, e) => SendExperimentCommand("移除全部Buff", BuildLoopFeatureLua("remove_all_buffs"));
            btnStealthFlags = new Button { Text = "关闭安全标志", Size = new Size(110, 26) };
            btnStealthFlags.Click += (s, e) => SendExperimentCommand("关闭安全标志", BuildLoopFeatureLua("stealth_flags"));
            var btnYyRemoveBuff = new Button { Text = "备用移除Buff", Size = new Size(110, 26) };
            btnYyRemoveBuff.Click += (s, e) => SendExperimentCommand("备用移除Buff", BuildYyLaoLiuBuffToolLua("yy_remove_buffs"));

            btnLoopBuff = new Button { Text = "循环强力Buff", Size = new Size(110, 26) };
            btnLoopBuff.Click += (s, e) => SendExperimentCommand("循环强力Buff", BuildLoopFeatureLua("loop_buff"));
            btnLoopDefense = new Button { Text = "循环防御Buff", Size = new Size(110, 26) };
            btnLoopDefense.Click += (s, e) => SendExperimentCommand("循环防御Buff", BuildLoopFeatureLua("loop_defense"));
            btnLoopLoot = new Button { Text = "循环自动拾取", Size = new Size(110, 26), BackColor = Color.FromArgb(255, 200, 200) };
            btnLoopLoot.Click += (s, e) => SendExperimentCommand("循环自动拾取", BuildLoopFeatureLua("loop_loot"));
            btnLoopRecover = new Button { Text = "循环自动恢复", Size = new Size(110, 26) };
            btnLoopRecover.Click += (s, e) => SendExperimentCommand("循环自动恢复", BuildLoopFeatureLua("loop_recover"));

            btnYyAutoLoot = new Button { Text = "自动拾取", Size = new Size(110, 26) };
            btnYyAutoLoot.Click += (s, e) => SendExperimentCommand("自动拾取", BuildLoopFeatureLua("loot_once"));
            btnYyRecover = new Button { Text = "一键恢复", Size = new Size(110, 26) };
            btnYyRecover.Click += (s, e) => SendExperimentCommand("一键恢复", BuildYyLaoLiuLua("yy_recover"));

            btnRhythmGame = new Button { Text = "NPC节奏游戏", Size = new Size(110, 26) };
            btnRhythmGame.Click += (s, e) => SendExperimentCommand("NPC节奏游戏", BuildGameFeatureLua("rhythm_game"));
            btnChessWin = new Button { Text = "象棋秒赢", Size = new Size(110, 26) };
            btnChessWin.Click += (s, e) => SendExperimentCommand("象棋秒赢", BuildGameFeatureLua("chess_win"));
            btnPitchPot = new Button { Text = "投壶圈变大", Size = new Size(110, 26) };
            btnPitchPot.Click += (s, e) => SendExperimentCommand("投壶圈变大", BuildGameFeatureLua("pitch_pot_easy"));

            chkOneHit = new CheckBox { Text = "一击必杀", Size = new Size(110, 26) };
            chkOneHit.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) { if (chkOneHit.Checked) SendExperimentCommand("一击必杀", BuildCombatExperimentLua("onehit")); else SendExperimentCommand("还原一击必杀", BuildLoopFeatureLua("onehit_off")); } };

            cmbAtkMul = new ComboBox { Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAtkMul.Items.AddRange(new object[] { "x2", "x4", "x8" });
            cmbAtkMul.SelectedIndex = 0;
            btnApplyAtkMul = new Button { Text = "应用倍率", Size = new Size(100, 26) };
            btnApplyAtkMul.Click += (s, e) => ApplyAttackMultiplierSelection();
            btnResetAtkMul = new Button { Text = "还原倍率", Size = new Size(100, 26) };
            btnResetAtkMul.Click += (s, e) => SendExperimentCommand("还原攻击倍率", BuildCombatExperimentLua("atk_mul_reset"));

            txtDialogSpeed = new TextBox { Size = new Size(120, 24), Text = "80" };
            btnApplyDialogSpeed = new Button { Text = "应用速度", Size = new Size(100, 26) };
            btnApplyDialogSpeed.Click += (s, e) => ApplyDialogSpeedSelection();
            btnResetDialogSpeed = new Button { Text = "还原速度", Size = new Size(100, 26) };
            btnResetDialogSpeed.Click += (s, e) => SendExperimentCommand("还原速度", BuildLoopFeatureLua("dialog_speed_reset"));

            cmbAtkSpeed = new ComboBox { Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAtkSpeed.Items.AddRange(new object[] { "x1.5", "x3", "x5", "x7.5", "x10", "x30" });
            cmbAtkSpeed.SelectedIndex = 0;
            btnApplyAtkSpeed = new Button { Text = "应用速度", Size = new Size(100, 26) };
            btnApplyAtkSpeed.Click += (s, e) => ApplyAtkSpeedSelection();
            btnResetAtkSpeed = new Button { Text = "还原攻击速度", Size = new Size(100, 26) };
            btnResetAtkSpeed.Click += (s, e) => SendExperimentCommand("还原攻击速度", BuildCombatExperimentLua("atk_speed_reset"));
            grpGM.Size = new Size(536, 424);
            tabGM = new Panel { Location = new Point(6, 6), Size = new Size(524, 412), BackColor = Color.White };
            var tabNav = new Panel { Location = new Point(0, 0), Size = new Size(524, 42), BackColor = Color.White };
            var btnTabInit = new Button { Text = "快速启动", Location = new Point(0, 6), Size = new Size(112, 28), Tag = "nav" };
            var btnTabBattle = new Button { Text = "功能", Location = new Point(118, 6), Size = new Size(112, 28), Tag = "nav" };
            var btnTabBuff = new Button { Text = "Buff", Location = new Point(236, 6), Size = new Size(112, 28), Tag = "nav" };
            var btnTabTools = new Button { Text = "工具", Location = new Point(354, 6), Size = new Size(112, 28), Tag = "nav" };
            tabNav.Controls.Add(btnTabInit);
            tabNav.Controls.Add(btnTabBattle);
            tabNav.Controls.Add(btnTabBuff);
            tabNav.Controls.Add(btnTabTools);
            tabGM.Controls.Add(tabNav);

            tabInit = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 370), BackColor = Color.White };
            var tabBattle = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 370), BackColor = Color.White };
            var tabBuff = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 370), BackColor = Color.White };
            var tabTools = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 370), BackColor = Color.White };
            tabGM.Controls.Add(tabInit);
            tabGM.Controls.Add(tabBattle);
            tabGM.Controls.Add(tabBuff);
            tabGM.Controls.Add(tabTools);

            Button[] tabButtons = new Button[] { btnTabInit, btnTabBattle, btnTabBuff, btnTabTools };
            Panel[] tabPages = new Panel[] { tabInit, tabBattle, tabBuff, tabTools };
            Action<Button, bool> styleNavButton = (button, active) => {
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = active ? Color.FromArgb(219, 234, 254) : Color.FromArgb(248, 250, 252);
                button.ForeColor = active ? Color.FromArgb(30, 64, 175) : Color.FromArgb(75, 85, 99);
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = active ? Color.FromArgb(219, 234, 254) : Color.FromArgb(226, 232, 240);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(219, 234, 254);
            };
            Action<Panel, Button> activateTab = null;
            activateTab = (page, button) => {
                for (int i = 0; i < tabPages.Length; i++)
                {
                    tabPages[i].Visible = tabPages[i] == page;
                    styleNavButton(tabButtons[i], tabButtons[i] == button);
                }
                page.BringToFront();
            };
            btnTabInit.Click += (s, e) => activateTab(tabInit, btnTabInit);
            btnTabBattle.Click += (s, e) => activateTab(tabBattle, btnTabBattle);
            btnTabBuff.Click += (s, e) => activateTab(tabBuff, btnTabBuff);
            btnTabTools.Click += (s, e) => activateTab(tabTools, btnTabTools);

            // ── 初始 tab content ──
            int[] yInit = new int[] { 4 };
            Func<int, int, Point> posInit = (col, yy) => new Point(12 + col * 124, yy);
            tabInit.Controls.Add(new Label { Text = "── 启动 ──", Location = new Point(12, yInit[0]), Size = new Size(500, 14), ForeColor = Color.FromArgb(120, 120, 120), Font = new Font("Microsoft YaHei", 7, FontStyle.Bold) });
            yInit[0] += 18;
            btnBrowse = new Button { Text = "选择游戏目录", Size = new Size(118, 28) };
            btnBrowse.Click += BtnBrowse_Click;
            btnStartGame = new Button { Text = "启动游戏", Size = new Size(118, 28) };
            btnStartGame.Click += BtnStartGame_Click;
            btnInject = new Button { Text = "注入工具", Size = new Size(118, 28) };
            btnInject.Click += BtnInject_Click_B;
            var btnTopMost = new Button { Text = "窗口置顶", Size = new Size(118, 28) };
            btnTopMost.Click += (s, e) => { TopMost = !TopMost; btnTopMost.BackColor = TopMost ? Color.LightGreen : Color.White; btnTopMost.Text = TopMost ? "取消置顶" : "窗口置顶"; };
            btnRefresh = new Button { Text = "刷新状态", Size = new Size(118, 28) };
            btnRefresh.Click += (s, e) => CheckState();
            var btnOpenDir = new Button { Text = "打开工具目录", Size = new Size(118, 28) };
            btnOpenDir.Click += (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", ToolDir); } catch (Exception ex) { AppendLog("打开目录失败: " + ex.Message); } };
            var btnOpenLog = new Button { Text = "打开日志", Size = new Size(118, 28) };
            btnOpenLog.Click += (s, e) => { try { if (File.Exists(UnifiedLogFile)) System.Diagnostics.Process.Start(UnifiedLogFile); else MessageBox.Show("日志文件不存在"); } catch (Exception ex) { MessageBox.Show("打开失败: " + ex.Message); } };
            var btnClearLog = new Button { Text = "清除日志", Size = new Size(118, 28) };
            btnClearLog.Click += (s, e) => { try { if (File.Exists(UnifiedLogFile)) File.WriteAllText(UnifiedLogFile, ""); AppendLog("日志已清除"); } catch (Exception ex) { AppendLog("清除日志失败: " + ex.Message); } };
            tabInit.Controls.Add(btnStartGame); btnStartGame.Location = posInit(0, yInit[0]);
            tabInit.Controls.Add(btnInject); btnInject.Location = posInit(1, yInit[0]);
            tabInit.Controls.Add(btnTopMost); btnTopMost.Location = posInit(2, yInit[0]);
            tabInit.Controls.Add(btnRefresh); btnRefresh.Location = posInit(3, yInit[0]);
            yInit[0] += rowStep;
            tabInit.Controls.Add(btnBrowse); btnBrowse.Location = posInit(0, yInit[0]);
            tabInit.Controls.Add(btnOpenDir); btnOpenDir.Location = posInit(1, yInit[0]);
            tabInit.Controls.Add(btnOpenLog); btnOpenLog.Location = posInit(2, yInit[0]);
            tabInit.Controls.Add(btnClearLog); btnClearLog.Location = posInit(3, yInit[0]);
            yInit[0] += rowStep + 6;
            lblStatus = new Label { Text = "状态: 未初始化", Location = posInit(0, yInit[0]), Size = new Size(492, 18), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
            tabInit.Controls.Add(lblStatus);
            yInit[0] += 22;
            tabInit.Controls.Add(new Label { Text = "── 公告 ──", Location = new Point(12, yInit[0]), Size = new Size(500, 14), ForeColor = Color.FromArgb(120, 120, 120), Font = new Font("Microsoft YaHei", 7, FontStyle.Bold) });
            yInit[0] += 18;
            var noticePanel = new Panel { Location = new Point(12, yInit[0]), Size = new Size(492, 76), BackColor = Color.FromArgb(248, 250, 252) };
            lblNotice = new Label { Text = BuildAnnouncementText(StartupManifest), Location = new Point(12, 10), Size = new Size(468, 56), ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Microsoft YaHei UI", 9F) };
            noticePanel.Controls.Add(lblNotice);
            tabInit.Controls.Add(noticePanel);

            int[] yBattle = new int[] { 4 };
            int[] yBuff = new int[] { 4 };
            int[] yTools = new int[] { 4 };
            Func<int, int, Point> pos = (col, yy) => new Point(12 + col * 124, yy);
            Action<Control, string, int[]> addTabSection = (parent, title, yref) => {
                if (parent.Controls.Count > 0) yref[0] += rowStep + sectionGap;
                parent.Controls.Add(new Label { Text = "── " + title + " ──", Location = new Point(12, yref[0]), Size = new Size(500, 14), ForeColor = Color.FromArgb(80, 80, 80), Font = new Font("Microsoft YaHei", 7, FontStyle.Bold) });
                yref[0] += 18;
            };
            Action<Control, Control, int, int[]> place = (parent, control, col, yref) => {
                control.Location = pos(col, yref[0]);
                parent.Controls.Add(control);
            };

            addTabSection(tabBattle, "功能", yBattle);
            place(tabBattle, chkGod, 0, yBattle);
            place(tabBattle, chkInvis, 1, yBattle);
            place(tabBattle, chkNpcDumb, 2, yBattle);
            yBattle[0] += rowStep;
            place(tabBattle, chkSuperDodge, 0, yBattle);
            place(tabBattle, chkOneHit, 1, yBattle);

            addTabSection(tabBattle, "辅助", yBattle);
            place(tabBattle, btnYyAutoLoot, 0, yBattle);
            place(tabBattle, btnYyRecover, 1, yBattle);
            place(tabBattle, btnRhythmGame, 2, yBattle);
            place(tabBattle, btnChessWin, 3, yBattle);
            yBattle[0] += rowStep;
            place(tabBattle, btnPitchPot, 0, yBattle);
            btnCutsceneKill = new Button { Text = "终止过场动画", Size = new Size(110, 26) };
            btnCutsceneKill.Click += (s, e) => SendExperimentCommand("终止过场动画", BuildCombatExperimentLua("cutscene_kill"));
            place(tabBattle, btnCutsceneKill, 1, yBattle);
            place(tabBattle, btnStealthFlags, 2, yBattle);
            place(tabBattle, btnAuxBuff, 3, yBattle);

            addTabSection(tabBattle, "剧情速度", yBattle);
            tabBattle.Controls.Add(new Label { Text = "倍率", Location = new Point(12, yBattle[0] + 6), Size = new Size(40, 20) });
            txtDialogSpeed.Location = new Point(74, yBattle[0] + 3);
            tabBattle.Controls.Add(txtDialogSpeed);
            btnApplyDialogSpeed.Location = new Point(206, yBattle[0]);
            tabBattle.Controls.Add(btnApplyDialogSpeed);
            btnResetDialogSpeed.Location = new Point(316, yBattle[0]);
            tabBattle.Controls.Add(btnResetDialogSpeed);

            addTabSection(tabBuff, "Buff 施加", yBuff);
            place(tabBuff, btnAtkBuff, 0, yBuff);
            place(tabBuff, btnDefBuff, 1, yBuff);
            place(tabBuff, btnMinBuff, 2, yBuff);
            place(tabBuff, btnGatherBuff, 3, yBuff);
            yBuff[0] += rowStep;
            place(tabBuff, btnUnknownBuff, 0, yBuff);
            place(tabBuff, btnRemoveAllBuffs, 1, yBuff);
            place(tabBuff, btnYyRemoveBuff, 2, yBuff);

            addTabSection(tabBuff, "循环功能 (再点停止)", yBuff);
            place(tabBuff, btnLoopBuff, 0, yBuff);
            place(tabBuff, btnLoopDefense, 1, yBuff);
            place(tabBuff, btnLoopLoot, 2, yBuff);
            place(tabBuff, btnLoopRecover, 3, yBuff);

            addTabSection(tabTools, "资源控制", yTools);
            place(tabTools, btnStamina, 0, yTools);
            place(tabTools, btnStaminaDive, 1, yTools);
            place(tabTools, btnStaminaEmpty, 2, yTools);
            place(tabTools, btnStaminaResetAll, 3, yTools);

            addTabSection(tabTools, "速度 / 倍率", yTools);
            tabTools.Controls.Add(new Label { Text = "攻击倍率", Location = new Point(12, yTools[0] + 6), Size = new Size(80, 20) });
            cmbAtkMul.Location = new Point(96, yTools[0] + 3);
            tabTools.Controls.Add(cmbAtkMul);
            btnApplyAtkMul.Location = new Point(224, yTools[0]);
            tabTools.Controls.Add(btnApplyAtkMul);
            btnResetAtkMul.Location = new Point(334, yTools[0]);
            tabTools.Controls.Add(btnResetAtkMul);

            yTools[0] += rowStep;
            tabTools.Controls.Add(new Label { Text = "攻击速度", Location = new Point(12, yTools[0] + 6), Size = new Size(80, 20) });
            cmbAtkSpeed.Location = new Point(96, yTools[0] + 3);
            tabTools.Controls.Add(cmbAtkSpeed);
            btnApplyAtkSpeed.Location = new Point(224, yTools[0]);
            tabTools.Controls.Add(btnApplyAtkSpeed);
            btnResetAtkSpeed.Location = new Point(334, yTools[0]);
            tabTools.Controls.Add(btnResetAtkSpeed);

            addTabSection(tabTools, "坐标", yTools);
            Button btnLogPos = new Button { Text = "记录坐标", Size = new Size(118, 28) };
            btnLogPos.Click += (s, ev) => SendExperimentCommand("记录坐标", BuildLogPositionLua());
            place(tabTools, btnLogPos, 0, yTools);
            tabTools.Controls.Add(new Label { Text = "坐标", Location = new Point(138, yTools[0] + 6), Size = new Size(40, 20) });
            txtCoordInput = new TextBox { Location = new Point(178, yTools[0] + 3), Size = new Size(178, 24), Text = "" };
            tabTools.Controls.Add(txtCoordInput);
            Button btnTeleportTo = new Button { Text = "传送", Location = new Point(366, yTools[0]), Size = new Size(74, 28) };
            btnTeleportTo.Click += (s, ev) => {
                string coordText = txtCoordInput.Text.Trim();
                if (string.IsNullOrEmpty(coordText)) { MessageBox.Show("请输入坐标，格式: x,y,z"); return; }
                MatchCollection matches = Regex.Matches(coordText, @"[-+]?\d+(?:\.\d+)?");
                if (matches.Count < 3) { MessageBox.Show("请输入三个数字坐标，格式: x,y,z"); return; }
                SendExperimentCommand("传送到坐标", BuildTeleportToLua(coordText));
            };
            tabTools.Controls.Add(btnTeleportTo);

            grpGM.Controls.Add(tabGM);
            Controls.Add(grpGM);
            ClientSize = new Size(552, grpGM.Bottom + 8);
            UpdateCommPaths();
            EnsureBuffConfigFile();
            LoadConfig();
            ApplyCleanStyle();
            activateTab(tabInit, btnTabInit);
            suppressCheckboxEvents = false;
            SetGMEnabled(false);
            CheckState();
        }

        void BtnBrowse_Click(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog { Description = "选择游戏主目录（包含 yysls_fast 或 yysls_medium 的目录）" };
            if (!string.IsNullOrEmpty(gameRootPath) && Directory.Exists(gameRootPath)) dlg.SelectedPath = gameRootPath;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string binPath = Path.Combine(dlg.SelectedPath, GameSubPath);
                if (!File.Exists(Path.Combine(binPath, GameExeName)))
                    binPath = Path.Combine(dlg.SelectedPath, GameSubPathAlt);
                string exePath = Path.Combine(binPath, GameExeName);
                if (File.Exists(exePath))
                {
                    gameRootPath = dlg.SelectedPath;
                    gameBinPath = binPath;
                    UpdateCommPaths();
                    SaveConfig();
                    UpdateStatus();
                }
                else MessageBox.Show("未找到游戏客户端:\n" + exePath, "路径错误");
            }
        }

        bool TryApplyCommConfig(string cfgPath)
        {
            try
            {
                string[] lines = File.ReadAllLines(cfgPath, new UTF8Encoding(false));
                if (lines.Length < 4) return false;

                string mmfName = lines[0].Trim();
                string cmdName = lines[1].Trim();
                string resultName = lines[2].Trim();
                string logName = lines[3].Trim();
                if (string.IsNullOrEmpty(mmfName) || string.IsNullOrEmpty(cmdName) || string.IsNullOrEmpty(resultName) || string.IsNullOrEmpty(logName))
                    return false;

                MMF_NAME = mmfName;
                CmdFile = Path.Combine(ToolDir, cmdName);
                CmdResultFile = Path.Combine(ToolDir, resultName);
                UnifiedLogFile = Path.Combine(ToolDir, logName);
                GadgetLogFile = UnifiedLogFile;
                AuxLogFile = UnifiedLogFile;
                ToolResultFile = Path.Combine(ToolDir, "gm_tool_result.txt");
                ToolResultCompatFile = Path.Combine(WorkDir, "gm_tool_result.txt");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Load svc.cfg failed: " + ex);
                return false;
            }
        }

        void UpdateCommPaths()
        {
            // 尝试读取已有的随机化配置
            string cfgPath = Path.Combine(ToolDir, "svc.cfg");
            if (File.Exists(cfgPath) && TryApplyCommConfig(cfgPath)) return;

            // 无配置则用默认名（兼容旧版）
            MMF_NAME = DefaultMmfName;
            CmdFile = Path.Combine(ToolDir, "gm_cmd.txt");
            CmdResultFile = Path.Combine(ToolDir, "gm_cmd_result.txt");
            ToolResultFile = Path.Combine(ToolDir, "gm_tool_result.txt");
            ToolResultCompatFile = Path.Combine(WorkDir, "gm_tool_result.txt");
            UnifiedLogFile = Path.Combine(ToolDir, "gm_tool.log");
            GadgetLogFile = UnifiedLogFile;
            AuxLogFile = UnifiedLogFile;
        }

        static void EnsurePayloadToolDir(string targetDir)
        {
            string payloadPath = Path.Combine(targetDir, "gm_payload.js");
            if (!File.Exists(payloadPath)) return;
            string text = File.ReadAllText(payloadPath, new UTF8Encoding(false));
            string dirLiteral = targetDir.Replace("\\", "\\\\");
            string newLine = "var TOOL_DIR = \"" + dirLiteral + "\";";
            // 查找已有的 TOOL_DIR 行并替换
            int idx = text.IndexOf("var TOOL_DIR =");
            if (idx >= 0)
            {
                int end = text.IndexOf(';', idx);
                if (end > idx)
                    text = text.Substring(0, idx) + newLine + text.Substring(end + 1);
                else
                    text = text.Substring(0, idx) + newLine + "\n" + text.Substring(idx);
            }
            else
            {
                // 没有TOOL_DIR行，在文件开头插入
                text = newLine + "\n" + text;
            }
            File.WriteAllText(payloadPath, text, new UTF8Encoding(false));
        }

        string GetGameFilePath(string fileName)
        {
            return string.IsNullOrEmpty(gameBinPath) ? "" : Path.Combine(gameBinPath, fileName);
        }

        string GetPreferredExistingPath(string primaryPath, string fallbackPath)
        {
            if (!string.IsNullOrEmpty(primaryPath) && File.Exists(primaryPath)) return primaryPath;
            if (!string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath)) return fallbackPath;
            return primaryPath;
        }

        string BuildGadgetConfig()
        {
            return "{\r\n  \"interaction\": {\r\n    \"type\": \"script\",\r\n    \"path\": \"gm_payload.js\"\r\n  }\r\n}\r\n";
        }

        // 内嵌DLL注入：不依赖外部注入器，直接用Win32 API完成
        bool InternalInjectDll(int processId, string dllPath)
        {
            AppendLog("[内嵌注入] 开始注入 PID=" + processId);
            IntPtr hProcess = IntPtr.Zero;
            IntPtr remoteMem = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    AppendLog("[内嵌注入] OpenProcess 失败 (Error=" + Marshal.GetLastWin32Error() + ")");
                    return false;
                }

                // 获取 LoadLibraryW 地址（W 版以支持 Unicode/中文路径）
                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                if (hKernel32 == IntPtr.Zero)
                {
                    AppendLog("[内嵌注入] GetModuleHandle(kernel32) 失败");
                    return false;
                }
                IntPtr loadLibAddr = GetProcAddress(hKernel32, "LoadLibraryW");
                if (loadLibAddr == IntPtr.Zero)
                {
                    AppendLog("[内嵌注入] 获取API地址失败");
                    return false;
                }
                AppendLog("[内嵌注入] API地址已获取");

                // 在目标进程分配内存存放DLL路径 (Unicode + 末尾 NUL，按字节算 = (len+1)*2)
                byte[] dllPathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                remoteMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPathBytes.Length, MEM_COMMIT, PAGE_READWRITE);
                if (remoteMem == IntPtr.Zero)
                {
                    AppendLog("[内嵌注入] VirtualAllocEx 失败 (Error=" + Marshal.GetLastWin32Error() + ")");
                    return false;
                }
                AppendLog("[内嵌注入] 远程内存已分配");

                // 写入DLL路径
                IntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, remoteMem, dllPathBytes, (IntPtr)dllPathBytes.Length, out bytesWritten))
                {
                    AppendLog("[内嵌注入] WriteProcessMemory 失败 (Error=" + Marshal.GetLastWin32Error() + ")");
                    return false;
                }
                AppendLog("[内嵌注入] DLL路径已写入 " + bytesWritten + " 字节");

                // 创建远程线程执行 LoadLibraryA(dllPath)
                uint threadId;
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibAddr, remoteMem, 0, out threadId);
                if (hThread == IntPtr.Zero)
                {
                    AppendLog("[内嵌注入] CreateRemoteThread 失败 (Error=" + Marshal.GetLastWin32Error() + ")");
                    return false;
                }
                AppendLog("[内嵌注入] 远程线程已创建");

                // 等待线程完成（最多10秒）
                uint waitResult = WaitForSingleObject(hThread, 10000);
                uint exitCode;
                GetExitCodeThread(hThread, out exitCode);
                AppendLog("[内嵌注入] 线程结束 wait=" + waitResult);

                if (exitCode == 0)
                {
                    AppendLog("[内嵌注入] LoadLibrary返回空，注入失败");
                    return false;
                }

                AppendLog("[内嵌注入] 注入成功");

                // 从PEB摘除模块，使core.dll不在模块列表中可见
                // 注意：GetExitCodeThread返回uint在x64下会截断模块基址，因此按文件名在PEB中查找
                try
                {
                    bool hidden = HideModuleFromPEB(hProcess, "core.dll");
                    if (hidden)
                        AppendLog("[内嵌注入] 模块已隐藏");
                    else
                        AppendLog("[内嵌注入] 隐藏未成功（不影响功能）");
                }
                catch (Exception ex)
                {
                    AppendLog("[内嵌注入] 隐藏异常: " + ex.Message);
                }

                // ErasePE暂时禁用（擦除PE头会导致Frida内部状态损坏）

                return true;
            }
            catch (Exception ex)
            {
                AppendLog("[内嵌注入] 异常: " + ex.Message);
                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero) CloseHandle(hThread);
                if (remoteMem != IntPtr.Zero) VirtualFreeEx(hProcess, remoteMem, 0, MEM_RELEASE);
                if (hProcess != IntPtr.Zero) CloseHandle(hProcess);
            }
        }

        // 从PEB的LDR模块链表中按文件名查找并摘除模块（x64目标进程）
        bool HideModuleFromPEB(IntPtr hProcess, string targetDllName)
        {
            // 通过NtQueryInformationProcess获取PEB地址
            byte[] pbi = new byte[48]; // PROCESS_BASIC_INFORMATION x64 = 48 bytes
            int retLen;
            int status = NtQueryInformationProcess(hProcess, 0 /* ProcessBasicInformation */, pbi, pbi.Length, out retLen);
            if (status != 0)
            {
                AppendLog("[HideModule] NtQueryInformationProcess 失败 status=" + status);
                return false;
            }
            // PEB地址在PBI偏移8（x64）
            long pebAddr = BitConverter.ToInt64(pbi, 8);
            if (pebAddr == 0) return false;

            // 读取PEB.Ldr (PEB+0x18)
            byte[] ldrBuf = new byte[8];
            IntPtr bytesRead;
            if (!ReadProcessMemory(hProcess, new IntPtr(pebAddr + 0x18), ldrBuf, (IntPtr)8, out bytesRead)) return false;
            long ldrAddr = BitConverter.ToInt64(ldrBuf, 0);
            if (ldrAddr == 0) return false;

            // 使用InMemoryOrderModuleList遍历（Ldr+0x20），此链表中BaseDllName在entry+0x58
            byte[] flinkBuf = new byte[8];
            if (!ReadProcessMemory(hProcess, new IntPtr(ldrAddr + 0x20), flinkBuf, (IntPtr)8, out bytesRead)) return false;
            long headFlink = BitConverter.ToInt64(flinkBuf, 0);

            long current = headFlink;
            int safety = 0;
            while (current != 0 && safety < 2048)
            {
                safety++;
                // InMemoryOrderLinks的Flink指向下一个节点的InMemoryOrderLinks字段
                // LDR_DATA_TABLE_ENTRY中InMemoryOrderLinks在偏移0x10
                // 所以entry基地址 = current - 0x10
                long entryBase = current - 0x10;

                // UNICODE_STRING x64: Length(2) + MaximumLength(2) + Padding(4) + Buffer(8)
                byte[] uniBuf = new byte[16];
                if (!ReadProcessMemory(hProcess, new IntPtr(entryBase + 0x58), uniBuf, (IntPtr)16, out bytesRead)) break;
                ushort nameLen = BitConverter.ToUInt16(uniBuf, 0);
                long nameBuf = BitConverter.ToInt64(uniBuf, 8);

                if (nameLen > 0 && nameLen <= 520 && nameBuf != 0)
                {
                    byte[] nameBytes = new byte[nameLen];
                    if (ReadProcessMemory(hProcess, new IntPtr(nameBuf), nameBytes, (IntPtr)nameLen, out bytesRead))
                    {
                        string dllName = System.Text.Encoding.Unicode.GetString(nameBytes);
                        if (dllName.Equals(targetDllName, StringComparison.OrdinalIgnoreCase))
                        {
                            // 找到目标模块，从三个链表中摘除
                            bool unlinked = true;

                            // 摘除 InLoadOrderLinks (entry+0x00)
                            unlinked &= UnlinkListEntry(hProcess, new IntPtr(entryBase + 0x00));

                            // 摘除 InMemoryOrderLinks (entry+0x10)
                            unlinked &= UnlinkListEntry(hProcess, new IntPtr(entryBase + 0x10));

                            // 摘除 InInitializationOrderLinks (entry+0x20)
                            unlinked &= UnlinkListEntry(hProcess, new IntPtr(entryBase + 0x20));

                            return unlinked;
                        }
                    }
                }

                // 读取下一个节点的InMemoryOrderLinks.Flink
                if (!ReadProcessMemory(hProcess, new IntPtr(current), flinkBuf, (IntPtr)8, out bytesRead)) break;
                current = BitConverter.ToInt64(flinkBuf, 0);

                // 回到链表头则结束
                if (current == headFlink) break;
            }
            AppendLog("[HideModule] 未在PEB中找到目标模块");
            return false;
        }

        // 摘除一个LIST_ENTRY：prev->Flink = next, next->Blink = prev
        bool UnlinkListEntry(IntPtr hProcess, IntPtr listEntryAddr)
        {
            byte[] buf = new byte[16]; // Flink(8) + Blink(8)
            IntPtr bytesRead, bytesWritten;
            if (!ReadProcessMemory(hProcess, listEntryAddr, buf, (IntPtr)16, out bytesRead)) return false;
            long flink = BitConverter.ToInt64(buf, 0);
            long blink = BitConverter.ToInt64(buf, 8);
            if (flink == 0 || blink == 0) return false;

            // prev->Flink = next  (blink+0x00 = flink)
            byte[] flinkBytes = BitConverter.GetBytes(flink);
            if (!WriteProcessMemory(hProcess, new IntPtr(blink), flinkBytes, (IntPtr)8, out bytesWritten)) return false;

            // next->Blink = prev  (flink+0x08 = blink)
            byte[] blinkBytes = BitConverter.GetBytes(blink);
            if (!WriteProcessMemory(hProcess, new IntPtr(flink + 8), blinkBytes, (IntPtr)8, out bytesWritten)) return false;

            return true;
        }

        void BtnInject_Click_B(object sender, EventArgs e)
        {
            string gadgetPath = Path.Combine(ToolDir, "core.dll");
            string payloadPath = Path.Combine(ToolDir, "gm_payload.js");
            string gadgetConfigPath = Path.Combine(ToolDir, "core.config");
            if (!File.Exists(gadgetPath)) { MessageBox.Show("组件文件不存在:\n" + gadgetPath); return; }
            if (!File.Exists(payloadPath) || !File.Exists(gadgetConfigPath))
            {
                MessageBox.Show("工具目录文件不完整，请确认以下文件存在:\n" + ToolDir);
                return;
            }
            var procs = Process.GetProcessesByName("yysls");
            if (procs.Length == 0) { MessageBox.Show("游戏未运行！请先启动游戏。"); return; }
            int targetPid = procs[0].Id;
            SafeDisposeProcesses(procs);

            // 注入前生成随机化通信配置
            GenerateRandomCommConfig();

            // 诊断：显示通信配置信息
            AppendLog("[配置] ToolDir: " + ToolDir);
            AppendLog("[配置] 日志文件: " + Path.GetFileName(UnifiedLogFile));
            AppendLog("[配置] MMF名称: " + MMF_NAME);
            AppendLog("[配置] gm_payload.js TOOL_DIR: " + ToolDir.Replace("\\", "\\\\"));

            // 注入前先创建共享内存，让Frida端能打开
            EnsureSharedMemory();

            // 优先使用内嵌注入（无外部注入器特征）
            AppendLog("=== 尝试内嵌注入（无注入器特征）===");
            bool injected = InternalInjectDll(targetPid, gadgetPath);
            if (injected)
            {
                AppendLog("内嵌注入成功！等待初始化...");
                UpdateStatus();
                // 注入后延迟检查日志，诊断初始化问题
                if (injectionDiagTimer != null)
                {
                    injectionDiagTimer.Stop();
                    injectionDiagTimer.Dispose();
                }
                injectionDiagTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                injectionDiagTimer.Tick += (s2, e2) =>
                {
                    injectionDiagTimer.Stop();
                    injectionDiagTimer.Dispose();
                    injectionDiagTimer = null;
                    DiagnoseInjectionResult();
                };
                injectionDiagTimer.Start();
                return;
            }

            MessageBox.Show("内嵌注入失败，请检查游戏进程权限。");
            AppendLog("内嵌注入失败");
        }

        void DiagnoseInjectionResult()
        {
            try
            {
                // 检查随机化日志文件
                string logPath = UnifiedLogFile;
                bool logExists = !string.IsNullOrEmpty(logPath) && File.Exists(logPath);

                // 也检查游戏目录下是否有日志
                string gameLogPath = !string.IsNullOrEmpty(gameBinPath) ? Path.Combine(gameBinPath, Path.GetFileName(UnifiedLogFile)) : "";
                bool gameLogExists = !string.IsNullOrEmpty(gameLogPath) && File.Exists(gameLogPath);

                // 检查旧默认名日志
                string oldLogPath = Path.Combine(ToolDir, "gm_tool.log");
                bool oldLogExists = File.Exists(oldLogPath);

                // 检查svc.cfg是否存在
                string cfgPath = Path.Combine(ToolDir, "svc.cfg");
                bool cfgExists = File.Exists(cfgPath);

                if (logExists)
                {
                    string content = File.ReadAllText(logPath);
                    if (content.Contains("Ready. Mode:") && content.Contains("CAPTURED L="))
                    {
                        AppendLog("[诊断] 初始化成功！Ready 和 CAPTURED L 已出现。");
                        return;
                    }
                    // 显示日志内容帮助诊断
                    string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int showLines = Math.Min(lines.Length, 15);
                    AppendLog("[诊断] 日志文件存在但未完成初始化，最近 " + showLines + " 行:");
                    for (int i = Math.Max(0, lines.Length - showLines); i < lines.Length; i++)
                        AppendLog("  " + lines[i]);
                    if (content.Contains("ERROR: entry points not found"))
                        AppendLog("[诊断] 签名扫描失败，游戏可能已更新，需要更新特征码。");
                    else if (content.Contains("Signature not found"))
                        AppendLog("[诊断] 部分签名未找到，检查日志中的签名扫描结果。");
                    else if (!content.Contains("CAPTURED L="))
                        AppendLog("[诊断] Hook已附加但未捕获到L指针，游戏可能未调用lua_pcall。尝试在游戏中操作触发Lua调用。");
                }
                else if (gameLogExists)
                {
                    AppendLog("[诊断] 日志在游戏目录而非工具目录！路径: " + gameLogPath);
                    string content = File.ReadAllText(gameLogPath);
                    string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int showLines = Math.Min(lines.Length, 10);
                    for (int i = Math.Max(0, lines.Length - showLines); i < lines.Length; i++)
                        AppendLog("  " + lines[i]);
                }
                else if (oldLogExists)
                {
                    AppendLog("[诊断] 旧默认日志存在(gm_tool.log)但随机化日志不存在，svc.cfg可能未被JS端读取。");
                }
                else
                {
                    AppendLog("[诊断] 未找到任何日志文件。可能原因:");
                    AppendLog("  1. core.dll加载后Frida初始化失败（字符串替换可能破坏了内部功能）");
                    AppendLog("  2. core.config或gm_payload.js路径无法访问");
                    AppendLog("  3. 游戏反作弊阻止了Frida运行");
                    AppendLog("  预期日志路径: " + (logPath ?? "(空)"));
                    AppendLog("  svc.cfg存在: " + cfgExists);
                    AppendLog("  ToolDir: " + ToolDir);
                }
            }
            catch (Exception ex)
            {
                AppendLog("[诊断] 检查异常: " + ex.Message);
            }
        }

        void EnsureBuffConfigFile()
        {
            try
            {
                if (!File.Exists(BuffConfigFile))
                {
                    File.WriteAllText(BuffConfigFile, BuildDefaultBuffConfig(), new UTF8Encoding(false));
                }
            }
            catch (Exception ex)
            {
                AppendLog("创建 buff_config.txt 失败: " + ex.Message);
            }
        }

        string BuildDefaultBuffConfig()
        {
            return
@"# Buff 配置文件
# 放在工具同目录，支持中文注释。
# 修改后重新点按钮即可生效，不需要重新编译。
#
# 注意：
# 1. 当前解析器按行读取，每个分类必须单独写成一整行。
# 2. 正确写法示例：ATTACK = {30302, 30314, 107031}
# 3. 行首 # 或 -- 都是注释。
# 4. 本文件为去重版，默认按按钮主归属分类。
# 5. REMOVE 为清理组，允许和其他分类重复。
# 6. 中文说明请查看工具同目录的 buff分类说明.md。
#
# 自动Buff按钮
AUTO = {30302, 30314, 107031, 1053070, 102400, 102401, 102402, 102404, 102450, 102451, 102452, 109014, 109015, 109016, 109920, 109921, 109922, 109923}
#
# 最小Buff按钮（少量核心ID，降低行为特征）
# 30372 无敌(减90%真伤)  70063 满状态恢复  1053070 伤害提升
MINIMAL = {30372, 70063, 1053070}
#
# 移除Buff按钮
REMOVE = {108010, 380013, 70063, 70141}
#
# 攻击Buff按钮
ATTACK = {1053026, 30302, 30314, 107031, 1053070, 102400, 102401, 102402, 102404, 102405, 102406, 102450, 102451, 102452, 102455, 102456, 109003, 109009, 109014, 109015, 109016, 109021, 109901, 109903, 109905, 109908, 109909, 109910, 109911, 109912, 109920, 109921, 109922, 109923, 109926, 102423, 102425, 109501, 109503, 109505, 109507, 109509, 109511, 70003, 70004, 109512}
#
# 防御Buff按钮
DEFENSE = {30372, 30310, 70184, 200071, 200059, 200083, 200099, 200086, 30366, 30303, 30333, 30334, 30376, 30379, 30406, 102707, 70005, 102408, 102458, 102703, 102704, 200031}
#
# 循环防御Buff按钮
LOOP_DEFENSE = {30372, 30310, 30333, 30334, 30376, 30379, 30406, 70005, 102703, 102704, 200031}
#
# 循环强力Buff按钮
LOOP_STRONG = {1053027, 1053026, 109927, 200005, 70141, 70063, 109506, 30302, 30314, 107031, 1053070, 102404, 102454, 109914, 109917, 109923, 109515, 10532, 200035, 200036, 102400, 102401, 102402, 102450, 102451, 102452, 109014, 109015, 109016, 109920, 109921, 109922, 102407, 102457, 102408, 102458}
#
# 采集/拾取相关Buff
GATHER = {104002, 104025, 104027, 104031, 104033, 104036, 104037, 104038, 104039}
#
# 辅助/恢复/特殊状态
AUX = {30005, 70110, 70025, 102701, 102702, 109602, 109603}
#
# 用途待确认Buff
UNKNOWN = {200102, 107201, 107202, 107203, 107204, 70182, 70183, 70186, 70187, 200095, 1053027, 109927, 200005, 109506, 70141, 70063}
#
# 战斗技能类Buff
COMBAT_SKILL = {102502, 102503, 102505, 102508, 102705, 102706, 103007, 103008, 102605, 102606, 102607}
#
# 卷轴/心法类Buff
SCROLL = {109604, 109605, 109606, 109607, 109608, 109609}
#
# 属性类Buff
ATTR = {104009, 104010, 104011, 104012, 104001}";
        }

        void ApplyAttackMultiplierSelection()
        {
            string selected = cmbAtkMul != null && cmbAtkMul.SelectedItem != null ? cmbAtkMul.SelectedItem.ToString() : "x2";
            string mode = "atk_mul_2";
            string label = "攻击倍率x2";
            if (selected == "x4") { mode = "atk_mul_4"; label = "攻击倍率x4"; }
            else if (selected == "x8") { mode = "atk_mul_8"; label = "攻击倍率x8"; }
            SendExperimentCommand(label, BuildCombatExperimentLua(mode));
        }

        void ApplyDialogSpeedSelection()
        {
            string text = txtDialogSpeed != null ? txtDialogSpeed.Text.Trim() : "80";
            double speed;
            if (!double.TryParse(text, out speed) || speed <= 0) speed = 80;
            string mode = "dialog_speed_" + speed.ToString("0.##");
            string label = "剧情速度x" + speed.ToString("0.##");
            SendExperimentCommand(label, BuildLoopFeatureLua(mode));
        }

        void ApplyAtkSpeedSelection()
        {
            string selected = cmbAtkSpeed != null && cmbAtkSpeed.SelectedItem != null ? cmbAtkSpeed.SelectedItem.ToString() : "x1.5";
            string mode = "atk_speed_1.5";
            string label = "攻击速度x1.5";
            if (selected == "x3") { mode = "atk_speed_3"; label = "攻击速度x3"; }
            else if (selected == "x5") { mode = "atk_speed_5"; label = "攻击速度x5"; }
            else if (selected == "x7.5") { mode = "atk_speed_7.5"; label = "攻击速度x7.5"; }
            else if (selected == "x10") { mode = "atk_speed_10"; label = "攻击速度x10"; }
            else if (selected == "x30") { mode = "atk_speed_30"; label = "攻击速度x30"; }
            SendExperimentCommand(label, BuildCombatExperimentLua(mode));
        }

        bool StartGameExecutable()
        {
            string exePath = Path.Combine(gameBinPath, GameExeName);
            if (!File.Exists(exePath)) { MessageBox.Show("未找到游戏客户端:\n" + exePath); return false; }
            var psi = new ProcessStartInfo { FileName = exePath, WorkingDirectory = gameBinPath, UseShellExecute = true };
            Process.Start(psi);
            return true;
        }

        void BtnStartGame_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(gameBinPath)) { MessageBox.Show("请先选择游戏目录！"); return; }
            try
            {
                AppendLog("启动游戏: " + Path.Combine(gameBinPath, GameExeName));
                if (!StartGameExecutable()) return;
                gameLaunched = true;
                AppendLog("游戏已启动！到角色选择界面后点[2.注入]");
                UpdateStatus();
            }
            catch (Exception ex) { MessageBox.Show("启动失败: " + ex.Message); AppendLog("ERROR: " + ex.Message); }
        }

        void SendCommand(string luaCode)
        {
            if (!isReady) { MessageBox.Show("请先等待就绪！"); return; }
            try
            {
                // 优先写共享内存
                bool mmfOk = WriteToSharedMemory(luaCode);
                // 同时写文件作为降级备用（强制 UTF-8 无 BOM，避免平台默认 GBK 与 Frida 端 UTF-8 解码不一致）
                File.WriteAllText(CmdFile, luaCode, new UTF8Encoding(false));
                AppendLog("发送" + (mmfOk ? "(MMF)" : "(文件)") + ": " + luaCode.Substring(0, Math.Min(60, luaCode.Length)) + (luaCode.Length > 60 ? "..." : ""));
            }
            catch (Exception ex) { MessageBox.Show("发送失败: " + ex.Message); }
        }

        void EnsureSharedMemory()
        {
            try
            {
                if (mmf == null || mmfAccessor == null)
                {
                    mmf = MemoryMappedFile.CreateOrOpen(MMF_NAME, MMF_SIZE, MemoryMappedFileAccess.ReadWrite);
                    mmfAccessor = mmf.CreateViewAccessor();
                    AppendLog("共享内存已创建");
                }
            }
            catch (Exception ex)
            {
                AppendLog("共享内存创建失败: " + ex.Message + " (将使用文件通信)");
                mmf = null;
                mmfAccessor = null;
            }
        }

        bool WriteToSharedMemory(string luaCode)
        {
            try
            {
                if (mmf == null || mmfAccessor == null)
                {
                    mmf = MemoryMappedFile.CreateOrOpen(MMF_NAME, MMF_SIZE, MemoryMappedFileAccess.ReadWrite);
                    mmfAccessor = mmf.CreateViewAccessor();
                }
                var bytes = Encoding.UTF8.GetBytes(luaCode);
                if (bytes.Length > MMF_HALF_SIZE - 8) return false;
                int newSeq = mmfSeq + 1;
                // 防撕裂：先写 payload + len，最后才发布 seq
                mmfAccessor.WriteArray(8, bytes, 0, bytes.Length);
                mmfAccessor.Write(4, bytes.Length);
                Thread.MemoryBarrier();
                mmfAccessor.Write(0, newSeq);
                mmfSeq = newSeq;
                return true;
            }
            catch
            {
                mmf = null;
                mmfAccessor = null;
                return false;
            }
        }

        string ReadResultFromSharedMemory()
        {
            try
            {
                if (mmfAccessor == null) return null;
                int seq = mmfAccessor.ReadInt32(MMF_RESULT_OFFSET);
                if (seq == mmfResultSeq) return null;
                mmfResultSeq = seq;
                int len = mmfAccessor.ReadInt32(MMF_RESULT_OFFSET + 4);
                if (len <= 0 || len > MMF_HALF_SIZE - 8) return null;
                var bytes = new byte[len];
                mmfAccessor.ReadArray(MMF_RESULT_OFFSET + 8, bytes, 0, len);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex) { Debug.WriteLine("Read shared result failed: " + ex.Message); return null; }
        }

        void SendExperimentCommand(string name, string luaCode)
        {
            if (!isReady) { MessageBox.Show("请先等待 Lua 就绪"); return; }
            if (commandPending) { AppendLog("上一条命令还在执行，请稍等: " + name); return; }
            commandPending = true;
            SafeDeleteFile(ToolResultFile, "清理功能结果文件失败");
            if (!string.Equals(ToolResultCompatFile, ToolResultFile, StringComparison.OrdinalIgnoreCase))
                SafeDeleteFile(ToolResultCompatFile, "清理兼容结果文件失败");
            SendCommand(luaCode);
            AppendLog("实验功能已发送: " + name);
            if (commandResultTimer != null)
            {
                commandResultTimer.Stop();
                commandResultTimer.Dispose();
            }
            commandResultTimer = new System.Windows.Forms.Timer { Interval = 1200 };
            int ticks = 0;
            const int maxPollTicks = 60;
            EventHandler tickHandler = null;
            tickHandler = (s, e) => {
                ticks++;
                bool finished = false;
                try
                {
                    // 优先从共享内存读结果
                    string mmfResult = ReadResultFromSharedMemory();
                    if (mmfResult != null)
                    {
                        var lines = mmfResult.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("---")) continue;
                            if (line.StartsWith("DONE") || line.StartsWith("LOAD_FAIL") || line.StartsWith("EXCEPTION") || line.StartsWith("RUNNING"))
                                AppendLog(name + " 状态: " + line.Trim());
                            else if (line.StartsWith("seq=") || line.StartsWith("loadbufferx=") || line.StartsWith("lua_call="))
                                continue;
                            else
                                AppendLog("  " + line.Trim());
                        }
                        finished = !mmfResult.StartsWith("RUNNING", StringComparison.Ordinal);
                        if (finished)
                            ReadFeatureResult(name);
                    }
                    else
                    {
                        // 降级：从文件读结果
                        ReadCommandResult(name);
                        if (IsTerminalResultFile(CmdResultFile) || File.Exists(ToolResultFile) || File.Exists(ToolResultCompatFile) || ticks >= maxPollTicks)
                            finished = true;
                    }
                }
                finally
                {
                    if (finished)
                    {
                        commandPending = false;
                        commandResultTimer.Stop();
                        commandResultTimer.Tick -= tickHandler;
                        commandResultTimer.Dispose();
                        commandResultTimer = null;
                    }
                }
            };
            commandResultTimer.Tick += tickHandler;
            commandResultTimer.Start();
        }

        static bool IsTerminalCommandStatusLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            string trimmed = line.Trim();
            return trimmed.StartsWith("DONE", StringComparison.Ordinal)
                || trimmed.StartsWith("LOAD_FAIL", StringComparison.Ordinal)
                || trimmed.StartsWith("EXCEPTION", StringComparison.Ordinal)
                || trimmed.StartsWith("DIAGNOSTIC", StringComparison.Ordinal);
        }

        static bool IsTerminalResultFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (IsTerminalCommandStatusLine(line))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("IsTerminalResultFile failed: " + ex.Message);
            }
            return false;
        }

        void ReadFeatureResult(string name)
        {
            string resultPath = File.Exists(ToolResultFile) ? ToolResultFile : (File.Exists(ToolResultCompatFile) ? ToolResultCompatFile : "");
            if (string.IsNullOrEmpty(resultPath)) return;

            var lines = File.ReadAllLines(resultPath);
            if (lines.Length == 0) return;
            int start = Math.Max(0, lines.Length - 16);
            AppendLog(name + " 返回结果:");
            for (int i = start; i < lines.Length; i++) AppendLog("  " + lines[i]);
        }

        void ReadCommandResult(string name)
        {
            try
            {
                if (!string.IsNullOrEmpty(CmdResultFile) && File.Exists(CmdResultFile))
                {
                    var lines = File.ReadAllLines(CmdResultFile);
                    if (lines.Length > 0) AppendLog(name + " 执行状态: " + string.Join(" | ", lines));
                }
                ReadFeatureResult(name);
            }
            catch (Exception ex) { AppendLog(name + " 读取结果失败: " + ex.Message); }
        }

        string LuaString(string text) { return "[[" + text.Replace("]]", "] ]") + "]]"; }

        string BuildLuaEnvelope(string action, string body)
        {
            string resultPath = ToolResultFile.Replace("\\", "/");
            return @"
local __out = {}
local function __add(...)
  local parts = {}
  for i = 1, select('#', ...) do parts[#parts + 1] = tostring(select(i, ...)) end
  __out[#__out + 1] = table.concat(parts, '\t')
end
local function __try(label, fn)
  local ret = { pcall(fn) }
  local parts = { label }
  for i = 1, math.min(#ret, 8) do parts[#parts + 1] = tostring(ret[i]) end
  __out[#__out + 1] = table.concat(parts, '\t')
end
__add('ACTION', " + LuaString(action) + @")
" + body + @"
local function __write_result(path)
  local __f, __err = io.open(path, 'w')
  if __f then
    __f:write(table.concat(__out, '\n'))
    __f:write('\n')
    __f:close()
    return true
  end
  return false, __err
end
local __ok, __err = __write_result(" + LuaString(resultPath) + @")
if not __ok then
  print('[GM_Tool] result write failed', __err)
end
";
        }

        string BuildDiagnosticLua()
        {
            return BuildLuaEnvelope("diagnostic", @"
__add('G', type(G), G)
__add('main_player', type(G) == 'table' and type(G.main_player) or 'nil', type(G) == 'table' and tostring(G.main_player) or 'nil')
__try('require.monitor_misc', function() local m = require('hexm.client.ui.windows.gm.gm_monitor.monitor_misc'); __add('monitor_misc', type(m), m) end)
__try('require.p2pgm_consts', function() local p = require('hexm.common.consts.p2pgm_consts'); __add('p2pgm_consts', type(p), p) end)
__try('gm_combat_action', function()
  local a = require('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  __add('combat_train_action', type(a), a)
  for _, k in ipairs({ 'set_niubility', 'set_game_speed', 'add_buff', 'rm_buff', 'set_lock_res_consume' }) do __add('combat_method.' .. k, type(a[k])) end
end)
");
        }

        string BuildLogPositionLua()
        {
            return BuildLuaEnvelope("log_pos", @"
__try('log_position', function()
  local mp = type(G) == 'table' and G.main_player or nil
  if not mp then error('main_player missing') end
  local pos = mp.get_position and mp:get_position() or mp.position or mp.pos
  if pos then
    __add('坐标', string.format('%.1f, %.1f, %.1f', pos.x or 0, pos.y or 0, pos.z or 0))
    __add('raw', tostring(pos.x), tostring(pos.y), tostring(pos.z))
  else
    __add('坐标', '无法获取位置')
  end
end)
");
        }

        string BuildTeleportToLua(string coordText)
        {
            return BuildLuaEnvelope("teleport_to", @"
local coords = '" + EscapeLuaString(coordText) + @"'
local parts = {}
for num in coords:gmatch('[%-]?%d+%.?%d*') do parts[#parts + 1] = tonumber(num) end
if #parts < 3 then __add('error', '需要x,y,z三个坐标'); return end
local tx, ty, tz = parts[1], parts[2], parts[3]
local mp = type(G) == 'table' and G.main_player or nil
if not mp then __add('error', 'main_player missing'); return end
local apis = {
  {'set_position', function() mp:set_position(tx, ty, tz) end},
  {'tp_to', function() mp:tp_to(tx, ty, tz) end},
  {'warp_to', function() mp:warp_to(tx, ty, tz) end},
  {'teleport_to', function() mp:teleport_to(tx, ty, tz) end},
  {'position_assign', function() mp.position.x = tx; mp.position.y = ty; mp.position.z = tz end},
}
for _, api in ipairs(apis) do
  local name, fn = api[1], api[2]
  local ok, err = pcall(fn)
  if ok then __add('传送', name .. ' -> ' .. string.format('%.1f,%.1f,%.1f', tx, ty, tz)); return end
  __add('skip', name .. ': ' .. tostring(err))
end
__add('传送', '失败: 无可用传送API')
");
        }

        // Lua builder entry points.

        string BuildLoopFeatureLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
local portable = _G.portable or portable or nil
local function player()
  return type(G) == 'table' and G.main_player or nil
end
local BUFF_CONFIG_PATH = '" + buffConfigPath + @"'
local function add_buff(id)
  local mp = player()
  if not mp or type(mp.add_buff) ~= 'function' then error('main_player.add_buff missing') end
  return mp.add_buff(mp, id)
end
local function combat_action()
  local ok, mod
  if _G.portable and _G.portable.import then
    ok, mod = pcall(_G.portable.import, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
    if ok and mod then return mod end
  end
  ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if ok and mod then return mod end
  return nil
end
local function local_remove_by_no(id)
  local mp = player()
  if not mp then error('main_player missing') end
  local ok, mod = pcall(require, 'hexm.client.entities.local.player_avatar_members.imp_buff')
  if not ok or not mod or not mod.PlayerAvatarMember or type(mod.PlayerAvatarMember.buff_remove_by_No) ~= 'function' then
    error('local buff_remove_by_No missing')
  end
  local candidates = { mp }
  if type(mp.__components__) == 'table' then
    for _, c in pairs(mp.__components__) do
      if type(c) == 'table' then candidates[#candidates + 1] = c end
    end
  end
  for _, name in ipairs({ 'imp_buff', 'buff', 'buff_comp', 'm_buff', '_buff' }) do
    if type(mp[name]) == 'table' then candidates[#candidates + 1] = mp[name] end
  end
  local lastErr = nil
  for _, target in ipairs(candidates) do
    ok, lastErr = pcall(function() return mod.PlayerAvatarMember.buff_remove_by_No(target, id) end)
    if ok then return true end
    if target and type(target.buff_remove_by_No) == 'function' then
      ok, lastErr = pcall(function() return target.buff_remove_by_No(target, id) end)
      if ok then return true end
      ok, lastErr = pcall(function() return target:buff_remove_by_No(id) end)
      if ok then return true end
    end
    if target and type(target.remove_buffs_by_No) == 'function' then
      ok, lastErr = pcall(function() return target.remove_buffs_by_No(target, id) end)
      if ok then return true end
      ok, lastErr = pcall(function() return target:remove_buffs_by_No(id) end)
      if ok then return true end
    end
    if target and type(target.remove_buff) == 'function' then
      ok, lastErr = pcall(function() return target.remove_buff(target, id) end)
      if ok then return true end
      ok, lastErr = pcall(function() return target:remove_buff(id) end)
      if ok then return true end
    end
  end
  error(tostring(lastErr or 'local buff_remove_by_No failed'))
end
local function server_remove_buff(id)
  local mp = player()
  if not mp then error('main_player missing') end
  local ok, mod = pcall(require, 'hexm.client.entities.server.player_avatar_members.imp_buff')
  if not ok or not mod or not mod.PlayerAvatarMember or type(mod.PlayerAvatarMember.rpc_fake_remove_buffs_by_No) ~= 'function' then
    error('server remove buff missing')
  end
  return mod.PlayerAvatarMember.rpc_fake_remove_buffs_by_No(mp, id)
end
local function remove_buff(id)
  local mp = player()
  if not mp then error('main_player missing') end
  if type(mp.remove_buff) == 'function' then return mp.remove_buff(mp, id) end
  if type(mp.rm_buff) == 'function' then return mp.rm_buff(mp, id) end
  error('main_player.remove_buff missing')
end
local function __try_bool(tag, fn)
  local ok, err = pcall(fn)
  if ok then __add(tag, 'ok'); return true end
  __add(tag, 'fail', tostring(err))
  return false
end
local function remove_buff_full(tag, id)
  local removed = false
  local mp = player()
  local eid = mp and mp.entity_id or nil
  removed = __try_bool(tag .. '.gm_remove_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.rm_buff) == 'function' then return action.rm_buff(id) end
    error('combat_train_action.rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.gm_remove_colon_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.rm_buff) == 'function' then return action:rm_buff(id) end
    error('combat_train_action:rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.gm_remove_buff_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.remove_buff) == 'function' then return action.remove_buff(id, eid) end
    error('combat_train_action.remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.gm_remove_buff_colon_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.remove_buff) == 'function' then return action:remove_buff(id, eid) end
    error('combat_train_action:remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.gm_del_buff_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.del_buff) == 'function' then return action.del_buff(id, eid) end
    error('combat_train_action.del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.gm_clear_buff_' .. tostring(id), function()
    local action = combat_action()
    if action and type(action.clear_buff) == 'function' then return action.clear_buff(id, eid) end
    error('combat_train_action.clear_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.main_remove_' .. tostring(id), function() return remove_buff(id) end) or removed
  removed = __try_bool(tag .. '.main_remove_colon_' .. tostring(id), function()
    if mp and type(mp.remove_buff) == 'function' then return mp:remove_buff(id) end
    error('main_player:remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.main_del_buff_' .. tostring(id), function()
    if mp and type(mp.del_buff) == 'function' then return mp.del_buff(mp, id) end
    error('main_player.del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.main_del_buff_colon_' .. tostring(id), function()
    if mp and type(mp.del_buff) == 'function' then return mp:del_buff(id) end
    error('main_player:del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.local_remove_by_no_' .. tostring(id), function() return local_remove_by_no(id) end) or removed
  removed = __try_bool(tag .. '.server_remove_' .. tostring(id), function() return server_remove_buff(id) end) or removed
  removed = __try_bool(tag .. '.main_remove_retry_' .. tostring(id), function() return remove_buff(id) end) or removed
  __add(tag .. '.summary', tostring(id), removed and 'ok' or 'fail')
  return removed
end
local function remove_buff_repeat(tag, id, times)
  local removed = 0
  for i = 1, times do
    if remove_buff_full(tag .. '.try' .. tostring(i), id) then removed = removed + 1 end
  end
  __add(tag .. '.repeat', tostring(id), removed, times)
  return removed
end
local function read_named_buff_list(label, fallback)
  local f = io.open(BUFF_CONFIG_PATH, 'r')
  if not f then return fallback end
  local content = f:read('*a') or ''
  f:close()
  for line in content:gmatch('[^\r\n]+') do
    local code_line = line:gsub('%-%-.*$', '')
    code_line = code_line:gsub('#.*$', '')
    code_line = code_line:match('^%s*(.-)%s*$') or ''
    local foundLabel, rest = code_line:match('^%s*([A-Za-z_]+)%s*[:=]%s*(%b{})')
    if foundLabel and string.upper(foundLabel) == string.upper(label) and rest then
      local inner = rest:sub(2, #rest - 1)
      local ids = {}
      for token in inner:gmatch('[^,]+') do
        local num = tonumber((token:gsub('%s+', '')))
        if num then table.insert(ids, num) end
      end
      if #ids > 0 then
        __add('buff_config.category', label, 'count=' .. tostring(#ids))
        return ids
      end
    end
  end
  return fallback
end
local function delayed_add_buffs(tag, ids, minDelay, maxDelay)
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  if not scene then
    for _, id in ipairs(ids) do __try(tag .. '.add_buff_' .. tostring(id), function() return add_buff(id) end) end
    return
  end
  local delay = 0
  for i, id in ipairs(ids) do
    delay = delay + minDelay + (maxDelay - minDelay) * math.random()
    local function do_add() __try(tag .. '.add_buff_' .. tostring(id), function() return add_buff(id) end) end
    scene:runAction(cc.Sequence:create({ cc.DelayTime:create(delay), cc.CallFunc:create(do_add) }))
  end
end
local function apply_strong_buffs()
  delayed_add_buffs('loop', read_named_buff_list('LOOP_STRONG', {1053027,1053026,109927,200005,70141,70063,109506,30302,30314,107031,1053070,102404,102454,109914,109917,109923,109515,10532,200035,200036,102400,102401,102402,102450,102451,102452,109014,109015,109016,109920,109921,109922,102407,102457,102408,102458}), 0.5, 2.0)
end
local function apply_defense_buffs()
  delayed_add_buffs('def', read_named_buff_list('DEFENSE', {30372,30310,70184,200071,200059,200083,200099,200086,30366,30303,30333,30334,30376,30379,30406,102707,70005,102408,102458,102703,102704,200031}), 0.5, 2.0)
end
local function apply_loop_defense_buffs()
  delayed_add_buffs('loop_def', read_named_buff_list('LOOP_DEFENSE', {30372,30310,30333,30334,30376,30379,30406,70005,102703,102704,200031}), 0.5, 2.0)
end
local function apply_minimal_buffs()
  delayed_add_buffs('minimal', read_named_buff_list('MINIMAL', {30372,70063,1053070}), 0.5, 2.0)
end
local function apply_stealth_flags()
  -- 递归遍历_G(限深度)强关客户端安全标志(ACSDK等)
  local FLAGS = {
    DISABLE_ACSDK = true, acsdk_info_has_inited = false,
    DEBUG = false, ENABLE_DEBUG_PRINT = false,
    ENABLE_FORCE_SHOW_GM = false, FORCE_OPEN_DEBUG_SHORTCUT = false,
    GM_IS_OPEN_GUIDE = false, GM_USE_PUBLISH = false
  }
  local MAX_DEPTH = 8
  local visited = setmetatable({}, { __mode = 'k' })
  local changed = 0
  local function walk(tbl, depth)
    if depth >= MAX_DEPTH or visited[tbl] then return end
    visited[tbl] = true
    for k, v in next, tbl do
      if type(k) == 'string' and FLAGS[k] ~= nil then
        if pcall(function() rawset(tbl, k, FLAGS[k]) end) then changed = changed + 1 end
      end
      if type(v) == 'table' then walk(v, depth + 1) end
    end
  end
  __try('stealth_flags.walk', function() walk(_G, 0); return changed end)
  __add('stealth_flags', 'changed', changed)
end
local function apply_gather_buffs()
  delayed_add_buffs('gather', read_named_buff_list('GATHER', {104002,104025,104027,104031,104033,104036,104037,104038,104039}), 0.5, 2.0)
end
local function apply_aux_buffs()
  delayed_add_buffs('aux', read_named_buff_list('AUX', {30005,70110,70025,102701,102702,102703,102704,109602,109603}), 0.5, 2.0)
end
local function apply_unknown_buffs()
  delayed_add_buffs('unknown', read_named_buff_list('UNKNOWN', {200102,107201,107202,107203,107204,70182,70183,70186,70187,200095,1053027,109927,200005,109506,70141,70063}), 0.5, 2.0)
end
local function remove_named_buffs(tag, label, fallback)
  local removed = 0
  for _, id in ipairs(read_named_buff_list(label, fallback)) do
    if remove_buff_repeat(tag, id, 6) > 0 then removed = removed + 1 end
  end
  __add(tag, 'removed', removed)
end
local function run_loot_once()
  local mp = player()
  if not mp then error('main_player missing') end
  __try('loot.collect_nearby_collections', function() if mp.ride_skill_collect_nearby_collections then return mp:ride_skill_collect_nearby_collections(5000) else error('missing') end end)
  __try('loot.kill_reward', function() if mp.ride_skill_find_nearest_kill_reward then local r = mp:ride_skill_find_nearest_kill_reward(5000); if r and mp.ride_skill_get_kill_reward then return mp:ride_skill_get_kill_reward(r) end else error('missing') end end)
end
local function run_recover_once()
  delayed_add_buffs('recover', read_named_buff_list('RECOVER', {70141, 70063, 102410, 102460}), 0.5, 2.0)
end
local function stop_loop(name)
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  local key = '__SVC_' .. name .. '_ACTION'
  if scene and _G[key] then scene:stopAction(_G[key]) end
  _G[key] = nil
end
local function disable_loop(name)
  stop_loop(name)
  _G['__SVC_' .. name .. '_ACTIVE'] = false
  _G['__SVC_' .. name .. '_GEN'] = (_G['__SVC_' .. name .. '_GEN'] or 0) + 1
  __add(name .. '_loop', 'disabled')
end
local function toggle_loop(name, delaySec, func)
  local activeKey = '__SVC_' .. name .. '_ACTIVE'
  local actionKey = '__SVC_' .. name .. '_ACTION'
  local genKey = '__SVC_' .. name .. '_GEN'
  if _G[activeKey] then
    stop_loop(name)
    _G[activeKey] = false
    _G[genKey] = (_G[genKey] or 0) + 1
    __add(name .. '_loop', 'stopped')
    return
  end
  stop_loop(name)
  _G[activeKey] = false
  _G[genKey] = (_G[genKey] or 0) + 1
  local gen = _G[genKey]
  func()
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  if not scene then error('running scene missing') end
  local action = cc.RepeatForever:create(cc.Sequence:create({ cc.DelayTime:create(delaySec), cc.CallFunc:create(function()
    if _G[activeKey] and _G[genKey] == gen then pcall(func) end
  end) }))
  scene:runAction(action)
  _G[actionKey] = action
  _G[activeKey] = true
  __add(name .. '_loop', 'started', delaySec)
end
local function set_dialog_speed(speed)
  if G then
    if G.dialog_global_time_scale ~= nil then G.dialog_global_time_scale = speed end
    if G.space then
      local space = G.space
      if space.dialog_global_time_scale ~= nil then space.dialog_global_time_scale = speed end
      if space.dialog_set_global_time_scale then pcall(space.dialog_set_global_time_scale, space, speed) end
      if space.imp_dialogs_manager and space.imp_dialogs_manager.dialog_set_global_time_scale then pcall(space.imp_dialogs_manager.dialog_set_global_time_scale, space.imp_dialogs_manager, speed) end
    end
    local mp = G.main_player
    if mp and mp.dialog_set_time_speed_scale then pcall(mp.dialog_set_time_speed_scale, mp, speed ~= 1.0, speed) end
  end
  __add('dialog_speed', speed)
end
local function clear_all_local_buffs(tag)
  local mp = player()
  if not mp then return false end
  local cleared = false
  __try(tag .. '.player.clear_all_buffs', function()
    if type(mp.clear_all_buffs) == 'function' then cleared = true; return mp.clear_all_buffs(mp) end
    error('main_player.clear_all_buffs missing')
  end)
  __try(tag .. '.player:clear_all_buffs', function()
    if type(mp.clear_all_buffs) == 'function' then cleared = true; return mp:clear_all_buffs() end
    error('main_player:clear_all_buffs missing')
  end)
  return cleared
end
local function has_buff(id)
  local mp = player()
  if not mp then return false end
  local ok, res
  ok, res = pcall(function() if type(mp.has_buff) == 'function' then return mp:has_buff(id) end end)
  if ok and res ~= nil then return res end
  ok, res = pcall(function() if type(mp.has_buff) == 'function' then return mp.has_buff(mp, id) end end)
  if ok and res ~= nil then return res end
  ok, res = pcall(function() if type(mp.get_buff) == 'function' then return mp:get_buff(id) ~= nil end end)
  if ok and res ~= nil then return res end
  ok, res = pcall(function() if type(mp.get_buff) == 'function' then return mp.get_buff(mp, id) ~= nil end end)
  if ok and res ~= nil then return res end
  return true
end
local function remove_all_buff_sets()
  disable_loop('LOOP_BUFF')
  disable_loop('LOOP_DEFENSE')
  disable_loop('LOOP_RECOVER')
  disable_loop('LOOP_LOOT')
  clear_all_local_buffs('remove_all.preclear')
  local entries = {
    { 'REMOVE', {108010,380013,70063,70141} },
    { 'ATTACK', {1053026,30302,30314,107031,1053070,102400,102401,102402,102404,102405,102406,102450,102451,102452,102455,102456,109003,109009,109021,109901,109903,109905,109908,109909,109910,109911,109912,109923,109926,102423,102425,109501,109503,109505,109507,109509,109511,70003,70004,109512,102407,102457,102408,102458} },
    { 'DEFENSE', {70184,200071,200059,200083,200099,200086,30366,102707,70005,102408,102458,102703,102704,200031} },
    { 'LOOP_DEFENSE', {30372,30310,30303,30333,30334,30376,30379,30406} },
    { 'LOOP_STRONG', {1053027,1053026,109927,200005,70141,70063,109506,30302,30314,107031,1053070,102404,102454,109914,109917,109923,109515,10532,200035,200036,102400,102401,102402,102450,102451,102452,109014,109015,109016,109920,109921,109922} },
    { 'GATHER', {104002,104025,104027,104031,104033,104036,104037,104038,104039} },
    { 'AUX', {30005,70110,70025,102701,102702,109602,109603} },
    { 'UNKNOWN', {200102,107201,107202,107203,107204,70182,70183,70186,70187,200095,1053027,109927,200005,109506,70141,70063} },
    { 'GOD', {70063,30372,70005} },
    { 'INVIS', {108010} },
    { 'ATK_MUL', {1053017,1053018,1053019} },
    { 'RECOVER', {70141,70063,102410,102460} },
    { 'NPC_DUMB', {380013} },
    { 'SUPER_DODGE', {102703,102704} },
    { 'MINIMAL', {30372,70063,1053070} }
  }
  local seen = {}
  local ids = {}
  local removed = 0
  local skipped = 0
  for _, entry in ipairs(entries) do
    for _, id in ipairs(read_named_buff_list(entry[1], entry[2])) do
      if not seen[id] then seen[id] = true; table.insert(ids, id) end
    end
  end
  for _, id in ipairs(ids) do
    if not has_buff(id) then
      skipped = skipped + 1
    else
      remove_buff_repeat('remove_all.pass1', id, 3)
      removed = removed + 1
    end
  end
  for _, id in ipairs(ids) do
    if has_buff(id) then
      remove_buff_repeat('remove_all.pass2', id, 6)
    end
  end
  for _, id in ipairs({102404,102454,102401,102451,102402,102452,109014,109015,109016,109920,109921,109922,109923,102407,102457,102408,102458}) do
    if has_buff(id) then
      remove_buff_repeat('remove_all.stubborn', id, 20)
    end
  end
  clear_all_local_buffs('remove_all.postclear')
  __add('remove_all_buffs', 'removed', removed, 'skipped', skipped, 'total_ids', #ids)
end
_G.LOOP_BUFF_ACTIVE = _G.__SVC_LOOP_BUFF_ACTIVE
_G.LOOP_LOOT_ACTIVE = _G.__SVC_LOOP_LOOT_ACTIVE
_G.LOOP_RECOVER_ACTIVE = _G.__SVC_LOOP_RECOVER_ACTIVE
";
            if (mode == "loop_buff") body += "\ntoggle_loop('LOOP_BUFF', 3.0, apply_strong_buffs)\n";
            else if (mode == "loop_loot") body += "\ntoggle_loop('LOOP_LOOT', 10.0, run_loot_once)\n";
            else if (mode == "loot_once") body += "\nrun_loot_once()\n";
            else if (mode == "loop_recover") body += "\ntoggle_loop('LOOP_RECOVER', 5.0, run_recover_once)\n";
            else if (mode == "loop_defense") body += "\ntoggle_loop('LOOP_DEFENSE', 5.0, apply_loop_defense_buffs)\n";
            else if (mode == "minimal_buff") body += "\napply_minimal_buffs()\n__add('minimal_buff', 'applied')\n";
            else if (mode == "stealth_flags") body += "\napply_stealth_flags()\n";
            else if (mode == "gather_buff") body += "\napply_gather_buffs()\n__add('gather_buff', 'applied_45_ids')\n";
            else if (mode == "aux_buff") body += "\napply_aux_buffs()\n__add('aux_buff', 'applied_9_ids')\n";
            else if (mode == "unknown_buff") body += "\napply_unknown_buffs()\n__add('unknown_buff', 'applied_16_ids')\n";
            else if (mode == "remove_all_buffs") body += "\nremove_all_buff_sets()\n";
            else if (mode == "onehit_off") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
remove_buff_full('onehit_off', 400148)
__add('onehit_off', 'done')
";
            else if (mode == "atk_mul_2") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
-- 保存原始函数（只保存一次）
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
if not _G.__orig_force_set_HP and type(mp.force_set_HP) == 'function' then
  _G.__orig_force_set_HP = mp.force_set_HP
end
local mul = 2
-- monkey-patch do_direct_damage
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_4") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
local mul = 4
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_8") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
local mul = 8
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_reset") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
-- 还原原始函数
if _G.__orig_do_direct_damage and type(_G.__orig_do_direct_damage) == 'function' then
  mp.do_direct_damage = _G.__orig_do_direct_damage
  _G.__orig_do_direct_damage = nil
  __add('atk_mul_reset.do_direct_damage', 'restored')
else
  __add('atk_mul_reset.do_direct_damage', 'no_orig')
end
_G.__atk_mul = nil
__add('atk_mul_reset', 'done')
";
            else if (mode.StartsWith("dialog_speed_") && mode != "dialog_speed_reset")
            {
                double spd;
                if (double.TryParse(mode.Substring("dialog_speed_".Length), out spd))
                    body += "\nset_dialog_speed(" + spd.ToString("0.0") + ")\n";
            }
            else if (mode == "dialog_speed_reset") body += "\nset_dialog_speed(1.0)\n";
            return BuildLuaEnvelope("loop_feature_" + mode, body);
        }

        string BuildGameFeatureLua(string mode)
        {
            string body = @"
local function player()
  return type(G) == 'table' and G.main_player or nil
end
local function add_buff(id)
  local mp = player()
  if not mp or type(mp.add_buff) ~= 'function' then error('main_player.add_buff missing') end
  return mp.add_buff(mp, id)
end
local function __try(tag, fn)
  local ok, err = pcall(fn)
  if ok then __add(tag, 'ok') else __add(tag, 'fail', tostring(err)) end
end
";
            if (mode == "auto_chest")
            {
                body += @"
local function is_chest_name(name)
  if type(name) ~= 'string' then return false end
  local keywords = {'宝箱','箱子','chest','treasure','采集','矿石','草药','木材','铁矿','铜矿','银矿','金矿'}
  for _, kw in ipairs(keywords) do if string.find(name, kw) then return true end end
  return false
end
local function run_auto_chest_collect()
  local mp = player()
  if not mp then error('main_player missing') end
  local collected = 0
  local space = G and G.space
  if not space then error('G.space missing') end
  __try('chest.find_entities', function()
    if space.find_entities_in_range then
      local entities = space:find_entities_in_range(mp, 5000)
      if type(entities) == 'table' then
        for _, ent in ipairs(entities) do
          if type(ent) == 'table' and is_chest_name(ent.name or ent.cname or '') then
            __try('chest.interact_' .. tostring(collected), function()
              if ent.interact then ent:interact(mp) end
              if ent.open then ent:open() end
              if ent.collect then ent:collect() end
            end)
            collected = collected + 1
          end
        end
      end
    end
  end)
  __try('chest.nearby_collect', function()
    if mp.ride_skill_collect_nearby_collections then mp:ride_skill_collect_nearby_collections(5000) end
    if mp.ride_skill_find_nearest_kill_reward then
      local r = mp:ride_skill_find_nearest_kill_reward(5000)
      if r and mp.ride_skill_get_kill_reward then mp:ride_skill_get_kill_reward(r) end
    end
  end)
  __add('auto_chest', 'collected', collected)
end
run_auto_chest_collect()
";
            }
            else if (mode == "auto_loot_plus")
            {
                body += @"
local function is_collect_name(name)
  if type(name) ~= 'string' then return false end
  local keywords = {'宝箱','箱子','chest','treasure','采集','矿石','草药','木材','奖励','drop'}
  for _, kw in ipairs(keywords) do if string.find(name, kw) then return true end end
  return false
end
local function run_auto_loot_plus()
  local mp = player()
  if not mp then error('main_player missing') end
  local collected = 0
  __try('loot_plus.nearby_collect', function()
    if mp.ride_skill_collect_nearby_collections then mp:ride_skill_collect_nearby_collections(1500) end
    if mp.ride_skill_find_nearest_kill_reward then
      local rewards = mp:ride_skill_find_nearest_kill_reward(1500)
      if rewards and mp.ride_skill_get_kill_reward then mp:ride_skill_get_kill_reward(rewards) end
    end
  end)
  __try('loot_plus.drop_manager', function()
    local dm = rawget(_G, 'DropManager')
    if dm and dm.get_nearby_drop_entities then
      local drops = dm.get_nearby_drop_entities(5000) or {}
      for _, eid in ipairs(drops) do
        pcall(function() mp:pick_drop_item(eid) end)
        pcall(function() mp:pick_reward_item(eid) end)
        collected = collected + 1
      end
    end
  end)
  __try('loot_plus.entity_scan', function()
    local space = G and G.space
    if space and space.find_entities_in_range then
      local entities = space:find_entities_in_range(mp, 2500)
      if type(entities) == 'table' then
        for _, ent in ipairs(entities) do
          local name = type(ent) == 'table' and (ent.name or ent.cname or '') or ''
          if type(ent) == 'table' and is_collect_name(name) then
            if ent.interact then pcall(function() ent:interact(mp) end) end
            if ent.open then pcall(function() ent:open() end) end
            if ent.collect then pcall(function() ent:collect() end) end
          end
        end
      end
    end
  end)
  __add('auto_loot_plus', 'collected', collected)
end
run_auto_loot_plus()
";
            }
            else if (mode == "rhythm_game")
            {
                body += @"
local function toggle_rhythm_game()
  if _G.__SVC_RHYTHM_ACTIVE then
    _G.__SVC_RHYTHM_ACTIVE = false
    local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene()
    if scene and _G.__SVC_RHYTHM_ACTION then scene:stopAction(_G.__SVC_RHYTHM_ACTION) end
    _G.__SVC_RHYTHM_ACTION = nil
    __add('rhythm_game', 'stopped')
    return
  end
  _G.__SVC_RHYTHM_ACTIVE = true
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene()
  if not scene then error('running scene missing') end
  local function auto_rhythm()
    local mp = player()
    if not mp then return end
    __try('rhythm.auto_play', function()
      if type(_G.gm_combat) == 'table' and _G.gm_combat.auto_rhythm then _G.gm_combat.auto_rhythm()
      elseif mp.rhythm_game_auto_play then mp:rhythm_game_auto_play() end
    end)
  end
  local action = cc.RepeatForever:create(cc.Sequence:create({cc.DelayTime:create(0.5), cc.CallFunc:create(function() pcall(auto_rhythm) end)}))
  scene:runAction(action)
  _G.__SVC_RHYTHM_ACTION = action
  __add('rhythm_game', 'started')
end
toggle_rhythm_game()
";
            }
            else if (mode == "chess_win")
            {
                body += @"
local function activate_chess_win()
  __try('chess.gm_win', function()
    if type(_G.gm_combat) == 'table' and _G.gm_combat.chess_instant_win then _G.gm_combat.chess_instant_win()
    elseif type(_G.gm_decorator) == 'table' and _G.gm_decorator.chess_win then _G.gm_decorator:chess_win() end
  end)
  __try('chess.force_win', function()
    local space = G and G.space
    if space and space.find_entities_in_range then
      local mp = player()
      local entities = space:find_entities_in_range(mp, 5000)
      if type(entities) == 'table' then
        for _, ent in ipairs(entities) do
          if type(ent) == 'table' then
            local name = ent.name or ent.cname or ''
            if string.find(name, '棋') or string.find(name, 'chess') then
              if ent.force_win then ent:force_win() end
              if ent.set_result then ent:set_result(1) end
              if ent.finish then ent:finish() end
            end
          end
        end
      end
    end
  end)
  __add('chess_win', 'executed')
end
activate_chess_win()
";
            }
            else if (mode == "pitch_pot_easy")
            {
                body += @"
local function enlarge_pitch_pot()
  __try('pitch_pot.scale_circle', function()
    if type(_G.gm_wanfa) == 'table' and _G.gm_wanfa.gm_scale_pitch_pot_circle then _G.gm_wanfa.gm_scale_pitch_pot_circle(7)
    elseif type(gm_wanfa) == 'table' and gm_wanfa.gm_scale_pitch_pot_circle then gm_wanfa.gm_scale_pitch_pot_circle(7)
    else error('gm_scale_pitch_pot_circle missing') end
  end)
  __add('pitch_pot_easy', 'executed')
end
enlarge_pitch_pot()
";
            }
            return BuildLuaEnvelope("game_feature_" + mode, body);
        }

        string BuildOutfitListLua()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("local OUTFIT_LIST = {");
            for (int i = 0; i < OutfitIds.Length && i < OutfitNames.Length; i++)
            {
                sb.Append("  { id = ");
                sb.Append(OutfitIds[i].ToString());
                sb.Append(", name = ");
                sb.Append(LuaString(OutfitNames[i]));
                sb.AppendLine(" },");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        string BuildOutfitApplyLua(int outfitId, string outfitName)
        {
            string body = @"
local outfit_id = " + outfitId.ToString() + @"
local outfit_name = " + LuaString(outfitName) + @"
local mp = type(G) == 'table' and G.main_player or nil
__add('outfit.name', outfit_name)
__add('outfit.id', outfit_id)
if not mp then error('main_player missing') end
if type(mp.add_buff) ~= 'function' then error('main_player.add_buff missing') end
__try('outfit.apply.add_buff', function() return mp:add_buff(outfit_id) end)
__try('outfit.apply.add_buff_raw', function() return mp.add_buff(mp, outfit_id) end)
";
            return BuildLuaEnvelope("outfit_apply_" + outfitId.ToString(), body);
        }

        string BuildOutfitPickerLua()
        {
            string body = BuildOutfitListLua() + @"
local function apply_outfit(outfit, title)
  local mp = type(G) == 'table' and G.main_player or nil
  if not mp then error('main_player missing') end
  if type(mp.add_buff) ~= 'function' then error('main_player.add_buff missing') end
  local ok, err = pcall(function() return mp:add_buff(outfit.id) end)
  if not ok then ok, err = pcall(function() return mp.add_buff(mp, outfit.id) end) end
  __add('outfit.apply', tostring(outfit.id), outfit.name, ok and 'ok' or tostring(err))
  if title and title.setString then
    title:setString((ok and '已应用: ' or '失败: ') .. outfit.name)
    if title.setColor and cc and cc.c3b then
      title:setColor(ok and cc.c3b(100,255,100) or cc.c3b(255,100,100))
    end
  end
end
local function close_old()
  if _G.__SVC_OUTFIT_PICKER then
    pcall(function() _G.__SVC_OUTFIT_PICKER:removeFromParent() end)
    _G.__SVC_OUTFIT_PICKER = nil
  end
end
local function show_picker()
  if not cc or not ccui then error('cc/ccui missing') end
  local director = cc.Director:getInstance()
  local scene = director and director:getRunningScene()
  if not scene then error('running scene missing') end
  local size = director:getVisibleSize()
  close_old()
  local width, height = 470, 790
  local page = tonumber(_G.__SVC_OUTFIT_PAGE) or 1
  local perPage = 11
  local totalPages = math.max(1, math.ceil(#OUTFIT_LIST / perPage))
  if page < 1 then page = 1 end
  if page > totalPages then page = totalPages end
  local layer = ccui.Layout:create()
  layer:setContentSize(cc.size(width, height))
  layer:setBackGroundColorType(1)
  layer:setBackGroundColor(cc.c3b(20, 24, 30))
  layer:setBackGroundColorOpacity(235)
  layer:setTouchEnabled(true)
  layer:setPosition(cc.p(math.max(20, size.width - width - 40), math.max(20, (size.height - height) * 0.5)))
  local title = ccui.Text:create('换装列表', 'Arial', 34)
  title:setColor(cc.c3b(100, 230, 255))
  title:setPosition(cc.p(width * 0.5, height - 38))
  layer:addChild(title)
  local closeBtn = ccui.Button:create()
  closeBtn:setTitleText('[ 关闭 X ]')
  closeBtn:setTitleFontSize(26)
  closeBtn:setPosition(cc.p(width - 90, height - 38))
  closeBtn:addClickEventListener(function() close_old() end)
  layer:addChild(closeBtn)
  local container = ccui.Layout:create()
  container:setContentSize(cc.size(width, height - 140))
  container:setPosition(cc.p(0, 70))
  layer:addChild(container)
  local function render()
    container:removeAllChildren()
    local startIndex = (page - 1) * perPage + 1
    local endIndex = math.min(startIndex + perPage - 1, #OUTFIT_LIST)
    for i = startIndex, endIndex do
      local outfit = OUTFIT_LIST[i]
      local item = ccui.Layout:create()
      item:setContentSize(cc.size(width - 40, 48))
      item:setTouchEnabled(true)
      item:setPosition(cc.p(20, height - 160 - (i - startIndex) * 55))
      local label = ccui.Text:create(tostring(outfit.id) .. '  ' .. outfit.name, 'Arial', 27)
      label:setAnchorPoint(cc.p(0, 0.5))
      label:setPosition(cc.p(10, 24))
      label:setColor(cc.c3b(220, 220, 220))
      item:addChild(label)
      item:addClickEventListener(function()
        _G.__SVC_OUTFIT_SELECTED_INDEX = i
        apply_outfit(outfit, title)
      end)
      container:addChild(item)
    end
  end
  local pageText = ccui.Text:create('', 'Arial', 24)
  pageText:setPosition(cc.p(width * 0.5, 35))
  layer:addChild(pageText)
  local prevBtn = ccui.Button:create()
  prevBtn:setTitleText('<< 上一页')
  prevBtn:setTitleFontSize(24)
  prevBtn:setPosition(cc.p(width * 0.24, 35))
  prevBtn:addClickEventListener(function()
    if page > 1 then page = page - 1; _G.__SVC_OUTFIT_PAGE = page; pageText:setString(tostring(page) .. '/' .. tostring(totalPages)); render() end
  end)
  layer:addChild(prevBtn)
  local nextBtn = ccui.Button:create()
  nextBtn:setTitleText('下一页 >>')
  nextBtn:setTitleFontSize(24)
  nextBtn:setPosition(cc.p(width * 0.76, 35))
  nextBtn:addClickEventListener(function()
    if page < totalPages then page = page + 1; _G.__SVC_OUTFIT_PAGE = page; pageText:setString(tostring(page) .. '/' .. tostring(totalPages)); render() end
  end)
  layer:addChild(nextBtn)
  pageText:setString(tostring(page) .. '/' .. tostring(totalPages))
  render()
  scene:addChild(layer, 10001)
  _G.__SVC_OUTFIT_PICKER = layer
  __add('outfit.picker', 'shown', 'items=' .. tostring(#OUTFIT_LIST))
end
show_picker()
";
            return BuildLuaEnvelope("outfit_picker", body);
        }

        string BuildYyLaoLiuBuffToolLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
local BUFF_CONFIG_PATH = '" + buffConfigPath + @"'
local function player()
  return type(G) == 'table' and G.main_player or nil
end
local function read_named_buff_list(label, fallback)
  local f = io.open(BUFF_CONFIG_PATH, 'r')
  if not f then return fallback end
  local content = f:read('*a') or ''
  f:close()
  for line in content:gmatch('[^\r\n]+') do
    local code_line = line:gsub('%-%-.*$', '')
    code_line = code_line:gsub('#.*$', '')
    code_line = code_line:match('^%s*(.-)%s*$') or ''
    local foundLabel, rest = code_line:match('^%s*([A-Za-z_]+)%s*[:=]%s*(%b{})')
    if foundLabel and string.upper(foundLabel) == string.upper(label) and rest then
      local inner = rest:sub(2, #rest - 1)
      local ids = {}
      for token in inner:gmatch('[^,]+') do
        local num = tonumber((token:gsub('%s+', '')))
        if num then table.insert(ids, num) end
      end
      if #ids > 0 then return ids end
    end
  end
  return fallback
end
local function remove_one_buff(mp, id)
  local ok, err = false, nil
  if mp and type(mp.remove_buff) == 'function' then
    ok, err = pcall(function() return mp:remove_buff(id) end)
    if ok then return true end
    ok, err = pcall(function() return mp.remove_buff(mp, id) end)
    if ok then return true end
  end
  if mp and type(mp.remove_buffs_by_No) == 'function' then
    ok, err = pcall(function() return mp:remove_buffs_by_No(id) end)
    if ok then return true end
  end
  __add('yy_remove_buff.fail', tostring(id), tostring(err))
  return false
end
_G.RunRemoveBuffs = function()
  local mp = player()
  if not mp then error('main_player missing') end
  local ids = {}
  if type(_G.BUFF_LIST) == 'table' then
    for _, entry in ipairs(_G.BUFF_LIST) do
      if entry and entry.id then table.insert(ids, entry.id) end
    end
  end
  if #ids == 0 then ids = read_named_buff_list('REMOVE', {108010, 380013, 70063, 70141}) end
  local removed = 0
  for _, id in ipairs(ids) do
    if remove_one_buff(mp, id) then removed = removed + 1 end
  end
  __add('yy_remove_buffs', 'ids=' .. tostring(#ids), 'removed=' .. tostring(removed))
end
";
            if (mode == "yy_remove_buffs") body += "\nRunRemoveBuffs()\n";
            return BuildLuaEnvelope("yylaoliu_buff_tool_" + mode, body);
        }

        string BuildYyLaoLiuLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
local player = type(G) == 'table' and G.main_player or nil
__add('player', type(player), player)
local BUFF_CONFIG_PATH = '" + buffConfigPath + @"'
local function try_import(modname)
  local ok, mod = pcall(require, modname)
  if ok and mod then return mod end
  if portable and portable.import then
    ok, mod = pcall(portable.import, modname)
    if ok and mod then return mod end
  end
  if package and package.loaded then return package.loaded[modname] end
  return nil
end
local function mp_add_buff(id)
  local ok = false
  __try('mp.add_buff_' .. tostring(id), function()
    if player and type(player.add_buff) == 'function' then
      local ret = player.add_buff(player, id)
      ok = true
      return ret
    end
    error('main_player.add_buff missing')
  end)
  return ok
end
local function __try_bool(tag, fn)
  local ok, err = pcall(fn)
  if ok then __add(tag, 'ok'); return true end
  __add(tag, 'fail', tostring(err))
  return false
end
local function get_player()
  return player or (type(G) == 'table' and G.main_player) or nil
end
local function get_combat_action()
  return try_import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
end
local function local_remove_by_no(id)
  local mp = get_player()
  if not mp then error('main_player missing') end
  local mod = try_import('hexm.client.entities.local.player_avatar_members.imp_buff')
  if not mod or not mod.PlayerAvatarMember or type(mod.PlayerAvatarMember.buff_remove_by_No) ~= 'function' then
    error('local buff_remove_by_No missing')
  end
  return mod.PlayerAvatarMember.buff_remove_by_No(mp, id)
end
local function server_remove_buff(id)
  local mp = get_player()
  if not mp then error('main_player missing') end
  local mod = try_import('hexm.client.entities.server.player_avatar_members.imp_buff')
  if not mod or not mod.PlayerAvatarMember or type(mod.PlayerAvatarMember.rpc_fake_remove_buffs_by_No) ~= 'function' then
    error('server rpc_fake_remove_buffs_by_No missing')
  end
  return mod.PlayerAvatarMember.rpc_fake_remove_buffs_by_No(mp, id)
end
local function remove_buff_full(tag, id)
  local removed = false
  local mp = get_player()
  local eid = mp and mp.entity_id or nil
  removed = __try_bool(tag .. '.action.rm_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.rm_buff) == 'function' then return action.rm_buff(id) end
    error('combat_train_action.rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action:rm_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.rm_buff) == 'function' then return action:rm_buff(id) end
    error('combat_train_action:rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action.remove_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.remove_buff) == 'function' then return action.remove_buff(id, eid) end
    error('combat_train_action.remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action:remove_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.remove_buff) == 'function' then return action:remove_buff(id, eid) end
    error('combat_train_action:remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action.del_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.del_buff) == 'function' then return action.del_buff(id, eid) end
    error('combat_train_action.del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action:del_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.del_buff) == 'function' then return action:del_buff(id, eid) end
    error('combat_train_action:del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action.clear_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.clear_buff) == 'function' then return action.clear_buff(id, eid) end
    error('combat_train_action.clear_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.action:clear_buff.' .. tostring(id), function()
    local action = get_combat_action()
    if action and type(action.clear_buff) == 'function' then return action:clear_buff(id, eid) end
    error('combat_train_action:clear_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player.remove_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.remove_buff) == 'function' then return p.remove_buff(p, id) end
    error('main_player.remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player:remove_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.remove_buff) == 'function' then return p:remove_buff(id) end
    error('main_player:remove_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player.rm_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.rm_buff) == 'function' then return p.rm_buff(p, id) end
    error('main_player.rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player:rm_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.rm_buff) == 'function' then return p:rm_buff(id) end
    error('main_player:rm_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player.del_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.del_buff) == 'function' then return p.del_buff(p, id) end
    error('main_player.del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.player:del_buff.' .. tostring(id), function()
    local p = get_player()
    if p and type(p.del_buff) == 'function' then return p:del_buff(id) end
    error('main_player:del_buff missing')
  end) or removed
  removed = __try_bool(tag .. '.local_remove_by_no.' .. tostring(id), function() return local_remove_by_no(id) end) or removed
  removed = __try_bool(tag .. '.server_remove.' .. tostring(id), function() return server_remove_buff(id) end) or removed
  __add(tag .. '.summary', tostring(id), removed and 'ok' or 'fail')
  return removed
end
local function mp_remove_buff(id) return remove_buff_full('mp_remove', id) end
local function parse_braced_ids(s)
  if not s then return {} end
  local start_brace = s:find('{', 1, true)
  local end_brace = s:find('}', 1, true)
  if not start_brace or not end_brace then return {} end
  local inner = s:sub(start_brace + 1, end_brace - 1)
  local ids = {}
  for token in inner:gmatch('[^,]+') do
    local num = tonumber((token:gsub('%s+', '')))
    if num then table.insert(ids, num) end
  end
  return ids
end
local function parse_table_entry(line)
  if not line then return nil end
  local id = line:match('id%s*=%s*([%d]+)')
  if id then id = tonumber(id) end
  if not id then return nil end
  local function flag_for(name)
    local v = line:match(name .. '%s*=%s*([%w]+)')
    if not v then return false end
    v = v:lower()
    return v == 'true' or v == '1'
  end
  return {
    id = id,
    note = line:match([[note%s*=%s*""([^""]*)""]]) or line:match([[note%s*=%s*'([^']*)']]) or '',
    auto = flag_for('auto'),
    perm = flag_for('perm') or flag_for('permanent'),
    remove = flag_for('remove')
  }
end
local function get_buff_config()
  local data = { auto = {}, permanent = {}, remove = {}, note_map = {} }
  local f = io.open(BUFF_CONFIG_PATH, 'r')
  if not f then
    __add('buff_config', 'missing', BUFF_CONFIG_PATH)
    return data
  end
  local content = f:read('*a') or ''
  f:close()
  local auto_map, perm_map, remove_map, note_map = {}, {}, {}, {}
  for line in content:gmatch('[^\r\n]+') do
    local code_line = line:gsub('%-%-.*$', '')
    code_line = code_line:gsub('#.*$', '')
    code_line = code_line:match('^%s*(.-)%s*$') or ''
    local label, rest = code_line:match('^%s*([A-Za-z_]+)%s*[:=]%s*(%b{})')
    if label and rest then
      local ids = parse_braced_ids(rest)
      local up = string.upper(label)
      for _, id in ipairs(ids) do
        if up == 'AUTO' then auto_map[id] = true
        elseif up == 'PERMANENT' then perm_map[id] = true
        elseif up == 'REMOVE' then remove_map[id] = true
        end
      end
    end
  end
  for line in content:gmatch('[^\r\n]+') do
    local code_line = line:gsub('%-%-.*$', '')
    code_line = code_line:gsub('#.*$', '')
    code_line = code_line:match('^%s*(.-)%s*$') or ''
    if code_line:match('^%s*%b{}%s*,?%s*$') then
      local entry = parse_table_entry(code_line)
      if entry and entry.id then
        if entry.auto then auto_map[entry.id] = true end
        if entry.perm then perm_map[entry.id] = true end
        if entry.remove then remove_map[entry.id] = true end
        if entry.note ~= '' then note_map[entry.id] = entry.note end
      end
    end
  end
  local function map_to_sorted_array(m)
    local arr = {}
    for id, _ in pairs(m) do table.insert(arr, id) end
    table.sort(arr)
    return arr
  end
  data.auto = map_to_sorted_array(auto_map)
  data.permanent = map_to_sorted_array(perm_map)
  data.remove = map_to_sorted_array(remove_map)
  data.note_map = note_map
  __add('buff_config', 'loaded', BUFF_CONFIG_PATH, 'AUTO=' .. tostring(#data.auto), 'PERMANENT=' .. tostring(#data.permanent), 'REMOVE=' .. tostring(#data.remove))
  return data
end
local function apply_config_auto_buffs()
  local cfg = get_buff_config()
  if #cfg.auto == 0 then return false end
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  if not scene then
    local applied = 0
    for _, id in ipairs(cfg.auto) do
      local note = cfg.note_map[id] or ''
      __try('buff_config.auto.' .. tostring(id), function()
        if player and type(player.add_buff) == 'function' then player.add_buff(player, id) else error('main_player.add_buff missing') end
      end)
      applied = applied + 1
      if note ~= '' then __add('buff_config.auto.note', tostring(id), note) end
    end
    __add('buff_config.auto_applied', applied)
    return applied > 0
  end
  local applied = 0
  local delay = 0
  for _, id in ipairs(cfg.auto) do
    local note = cfg.note_map[id] or ''
    local bid = id
    local bnote = note
    delay = delay + 0.5 + 1.5 * math.random()
    local function do_add()
      __try('buff_config.auto.' .. tostring(bid), function()
        if player and type(player.add_buff) == 'function' then player.add_buff(player, bid) else error('main_player.add_buff missing') end
      end)
      if bnote ~= '' then __add('buff_config.auto.note', tostring(bid), bnote) end
    end
    scene:runAction(cc.Sequence:create({ cc.DelayTime:create(delay), cc.CallFunc:create(do_add) }))
    applied = applied + 1
  end
  __add('buff_config.auto_applied', applied)
  return applied > 0
end
local function apply_config_remove_buffs()
  local cfg = get_buff_config()
  if #cfg.remove == 0 then return false end
  local removed = 0
  for _, id in ipairs(cfg.remove) do
    local note = cfg.note_map[id] or ''
    if remove_buff_full('buff_config.remove', id) then removed = removed + 1 end
    if note ~= '' then __add('buff_config.remove.note', tostring(id), note) end
  end
  __add('buff_config.remove_applied', removed)
  return removed > 0
end
local function apply_config_permanent_buffs()
  local cfg = get_buff_config()
  if #cfg.permanent == 0 then return false end
  local action = try_import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if not action then
    __add('buff_config.permanent', 'combat_train_action_missing')
    return false
  end
  local eid = player and player.entity_id or nil
  if not eid then
    __add('buff_config.permanent', 'player_entity_missing')
    return false
  end
  local duration = 10675199116730015
  local level = 5
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  if not scene then
    local applied = 0
    for _, id in ipairs(cfg.permanent) do
      local note = cfg.note_map[id] or ''
      local ok = pcall(function() action.add_buff(id, eid, duration, level, eid, 'gm_tool_cfg') end)
      if not ok then
        ok = pcall(function() action:add_buff(id, eid, duration, level, eid, 'gm_tool_cfg') end)
      end
      __add('buff_config.permanent.' .. tostring(id), ok and 'ok' or 'fail', note)
      if ok then applied = applied + 1 end
    end
    __add('buff_config.permanent_applied', applied)
    return applied > 0
  end
  local applied = 0
  local delay = 0
  for _, id in ipairs(cfg.permanent) do
    local note = cfg.note_map[id] or ''
    local bid = id
    local bnote = note
    delay = delay + 0.5 + 1.5 * math.random()
    local function do_add()
      local ok = pcall(function() action.add_buff(bid, eid, duration, level, eid, 'gm_tool_cfg') end)
      if not ok then
        ok = pcall(function() action:add_buff(bid, eid, duration, level, eid, 'gm_tool_cfg') end)
      end
      __add('buff_config.permanent.' .. tostring(bid), ok and 'ok' or 'fail', bnote)
    end
    scene:runAction(cc.Sequence:create({ cc.DelayTime:create(delay), cc.CallFunc:create(do_add) }))
    applied = applied + 1
  end
  __add('buff_config.permanent_applied', applied)
  return applied > 0
end
function RunKillNPC()
  __add('RunKillNPC', 'begin')
  local count = 0
  local combat_action = try_import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  __add('combat_action', type(combat_action), combat_action)
  if combat_action then
    __try('combat_action.set_npc_mortal', function() if combat_action.set_npc_mortal then combat_action.set_npc_mortal(true) else error('missing') end end)
    __try('combat_action.kill_all_npc', function() if combat_action.kill_all_npc then combat_action.kill_all_npc() else error('missing') end end)
    __try('combat_action.set_niubility', function() if combat_action.set_niubility then combat_action.set_niubility(1) else error('missing') end end)
  end
  __try('locked_target_damage', function()
    local target_id = player and type(player.get_lock_target_id) == 'function' and player.get_lock_target_id(player) or nil
    __add('target_id', type(target_id), target_id)
    if target_id and G and G.space and type(G.space.get_entity) == 'function' then
      local target = G.space.get_entity(G.space, target_id)
      __add('target', type(target), target)
      if target then
        pcall(function() target.force_set_HP(target, 0, player.entity_id, 'gm') end)
        pcall(function() target.attr_set_HP(target, 0, player.entity_id, true, false) end)
        pcall(function() target.do_direct_damage(target, 999999999, player.entity_id, 0, 0, 0, 0) end)
        count = count + 1
      end
    end
  end)
  __try('aoi_entity_damage', function()
    local manager = rawget(_G, 'MEntityManager') or rawget(_G, 'EntityManager')
    if manager and type(manager.GetAOIEntities) == 'function' then
      local all = manager.GetAOIEntities(manager) or {}
      for i = 1, math.min(#all, 80) do
        local ent = all[i]
        local okName, name = pcall(function() return ent.GetName and ent:GetName() or tostring(ent) end)
        if okName and name and (name:find('AiAvatar') or name:find('Npc') or name:find('Boss')) then
          local eid = ent.entity_id
          local target = eid and G and G.space and G.space.get_entity and G.space:get_entity(eid) or ent
          if target and target ~= player then
            pcall(function() target.force_set_HP(target, 0, player.entity_id, 'gm') end)
            pcall(function() target.do_direct_damage(target, 999999999, player.entity_id, 0, 0, 0, 0) end)
            count = count + 1
          end
        end
      end
    else
      __add('aoi_manager_missing', type(manager), manager)
    end
  end)
  __add('RunKillNPC.count', count)
end
function RunRecover()
  local action = try_import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if action then
    __try('recover_hp', function() if action.recover_hp then action.recover_hp(1) else error('missing') end end)
    __try('fullfill_all_combat_res', function() if action.fullfill_all_combat_res then action.fullfill_all_combat_res(1) else error('missing') end end)
  end
  for _, id in ipairs(read_named_buff_list('RECOVER', {70141, 70063})) do
    mp_add_buff(id)
  end
end
function RunAutoLoot()
  __try('ride_collect', function() if player and player.ride_skill_collect_nearby_collections then player:ride_skill_collect_nearby_collections(5000) else error('missing') end end)
  __try('kill_reward', function() if player and player.ride_skill_find_nearest_kill_reward then local r = player:ride_skill_find_nearest_kill_reward(5000); if r and player.ride_skill_get_kill_reward then player:ride_skill_get_kill_reward(r) end else error('missing') end end)
  __try('drop_manager', function() local dm = rawget(_G, 'DropManager'); if dm and dm.get_nearby_drop_entities then local drops = dm.get_nearby_drop_entities(5000) or {}; for _, eid in ipairs(drops) do pcall(function() player:pick_drop_item(eid) end); pcall(function() player:pick_reward_item(eid) end) end else error('missing') end end)
end
function RunNpcDumb()
  for _, id in ipairs(read_named_buff_list('NPC_DUMB', {380013})) do
    mp_add_buff(id)
  end
  __try('toggle_npc_ai', function() if type(toggle_npc_ai) == 'function' then toggle_npc_ai() else error('missing') end end)
end
function RunSuperDodge()
  for _, id in ipairs(read_named_buff_list('SUPER_DODGE', {102703, 102704})) do
    mp_add_buff(id)
  end
end
function RunAutoBuff()
  delayed_main_add_buffs('auto', {30302,30314,107031,1053070,102400,102401,102402,102404,102405,102406,102423,102425,102450,102451,102452,102454,102455,102456,109003,109009,109014,109015,109016,109021,109501,109503,109505,109507,109509,109511,109515,109901,109903,109905,109908,109909,109910,109911,109912,109914,109917,109920,109921,109922,109923,109926}, 0.5, 2.0)
end
function RunRemoveBuffs()
  local removed = 0
  for _, id in ipairs({108010,380013,70063,70141}) do
    if mp_remove_buff(id) then removed = removed + 1 end
  end
  __add('RunRemoveBuffs.removed', removed)
end
function RunPermanentBuffs()
  if not try_import('hexm.client.ui.windows.gm.gm_combat.combat_train_action') then
    __add('permbuffs', 'combat_train_action_missing', 'fallback_main_player_add_buff')
  end
  local buffs = read_named_buff_list('PERMANENT', {30372,70063,1053070,30310,30333,30334})
  local added = 0
  for _, id in ipairs(buffs) do
    if mp_add_buff(id) then added = added + 1 end
  end
  __add('permbuffs.added', added)
end
";
            if (mode == "yy_autoloot") body += "\nRunAutoLoot()\n";
            else if (mode == "yy_npcdumb") body += "\nRunNpcDumb()\n";
            else if (mode == "yy_npcdumb_off") body += "\n__try('npcdumb_off', function() if _G.GM_DisableNPCDUMB then _G.GM_DisableNPCDUMB() else __add('npcdumb_off', 'no_disable_func') end end)\n";
            else if (mode == "yy_recover") body += "\nRunRecover()\n";
            else if (mode == "yy_superdodge") body += "\nRunSuperDodge()\n";
            else if (mode == "yy_removebuffs") body += "\n__try('removebuffs', function() if not apply_config_remove_buffs() then return RunRemoveBuffs() end end)\n";
            else if (mode == "yy_autobuff") body += "\n__try('autobuff', function() if not apply_config_auto_buffs() then return RunAutoBuff() end end)\n";
            else if (mode == "yy_permbuffs") body += "\n__try('permbuffs', function() if not apply_config_permanent_buffs() then return RunPermanentBuffs() end end)\n";
            return BuildLuaEnvelope("yylaoliu_" + mode, body);
        }

        string BuildCombatExperimentLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
local portable = _G.portable or portable or nil
local player = type(G) == 'table' and G.main_player or nil
__add('player', type(player), player)
local BUFF_CONFIG_PATH = '" + buffConfigPath + @"'
local function call_class(label, path, className, method, ...)
  local args = { ... }
  __try(label .. '.require', function()
    local mod = require(path)
    __add(label .. '.module', type(mod), mod)
    local cls = mod and mod[className]
    __add(label .. '.class', type(cls), cls)
    if cls and type(cls[method]) == 'function' then
      local ret = { cls[method](player, table.unpack(args)) }
      local parts = { label .. '.' .. method, 'ret_count=' .. tostring(#ret) }
      for i = 1, math.min(#ret, 6) do parts[#parts + 1] = tostring(ret[i]) end
      __add(table.unpack(parts))
    else
      __add(label .. '.missing_class_method', tostring(method))
    end
  end)
end
local function add_buff(buffNo)
  call_class('server_buff_' .. tostring(buffNo), 'hexm.client.entities.server.player_avatar_members.imp_buff', 'PlayerAvatarMember', 'rpc_fake_add_buff', buffNo)
end
local function remove_buff(buffNo)
  call_class('server_remove_buff_' .. tostring(buffNo), 'hexm.client.entities.server.player_avatar_members.imp_buff', 'PlayerAvatarMember', 'rpc_fake_remove_buffs_by_No', buffNo)
end
local function add_buff_long(buffNo)
  add_buff(buffNo)
  __try('combat_action.add_buff_long_' .. tostring(buffNo), function()
    local ok, action = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
    if not ok then action = nil end
    if action and type(action.add_buff) == 'function' then
      local eid = player and player.entity_id or nil
      local ret = { action.add_buff(buffNo, eid, 1800, 1, eid, 'gm_tool_long') }
      __add('combat_action.add_buff_long.ret', buffNo, 'ret_count=' .. tostring(#ret))
    else
      __add('combat_action.add_buff_long.missing', tostring(buffNo))
    end
  end)
end
local function refresh_long_buff(buffNo, times)
  times = times or 3
  for i = 1, times do add_buff_long(buffNo) end
end
local function get_gm_action()
  local ok, mod
  -- Step 1: portable.import
  if portable and portable.import then
    ok, mod = pcall(portable.import, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
    __add('gm_action.step1_portable', ok and type(mod) or 'fail:' .. tostring(mod))
    if ok and mod then return mod end
  else
    __add('gm_action.step1_portable', 'skipped_portable_nil')
  end
  -- Step 2: package.loaded
  ok, mod = pcall(function() return package.loaded['hexm.client.ui.windows.gm.gm_combat.combat_train_action'] end)
  __add('gm_action.step2_pkgloaded', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and mod then return mod end
  -- Step 3: require
  ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  __add('gm_action.step3_require', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and mod then return mod end
  return nil
end
local function call_gm_action(method, ...)
  local args = { ... }
  __try('gm_action.' .. method, function()
    local action = get_gm_action()
    __add('gm_action.module', type(action), action)
    if action and type(action[method]) == 'function' then
      local ret = { action[method](table.unpack(args)) }
      local parts = { 'gm_action.' .. method .. '.ret', 'ret_count=' .. tostring(#ret) }
      for i = 1, math.min(#ret, 6) do parts[#parts + 1] = tostring(ret[i]) end
      __add(table.unpack(parts))
    else
      __add('gm_action.missing_method', method)
    end
  end)
end
local function get_gm_combat()
  if type(_G.gm_combat) == 'table' then __add('gm_combat.step0_global', 'found'); return _G.gm_combat end
  if type(gm_combat) == 'table' then __add('gm_combat.step0_local', 'found'); return gm_combat end
  local ok, mod
  if portable and portable.import then
    ok, mod = pcall(portable.import, 'hexm.client.debug.gm.gm_commands.gm_combat')
    __add('gm_combat.step1_portable', ok and type(mod) or 'fail:' .. tostring(mod))
    if ok and mod then return mod end
  else
    __add('gm_combat.step1_portable', 'skipped_portable_nil')
  end
  ok, mod = pcall(function() return package.loaded['hexm.client.debug.gm.gm_commands.gm_combat'] end)
  __add('gm_combat.step2_pkgloaded', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and mod then return mod end
  ok, mod = pcall(require, 'hexm.client.debug.gm.gm_commands.gm_combat')
  __add('gm_combat.step3_require', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and type(mod) == 'table' then return mod end
  return nil
end
local function call_gm_combat(method, ...)
  local args = { ... }
  __try('gm_combat.' .. method, function()
    local combat = get_gm_combat()
    __add('gm_combat.module', type(combat), combat)
    if combat and type(combat[method]) == 'function' then
      local ret = { combat[method](table.unpack(args)) }
      local parts = { 'gm_combat.' .. method .. '.ret', 'ret_count=' .. tostring(#ret) }
      for i = 1, math.min(#ret, 6) do parts[#parts + 1] = tostring(ret[i]) end
      __add(table.unpack(parts))
    else
      __add('gm_combat.missing_method', method)
    end
  end)
end
local function main_add_buff(buffNo)
  local ok = false
  __try('main_player.add_buff_' .. tostring(buffNo), function()
    if player and type(player.add_buff) == 'function' then
      local ret = player.add_buff(player, buffNo)
      ok = true
      return ret
    end
    error('main_player.add_buff missing')
  end)
  return ok
end
local function delayed_main_add_buffs(tag, ids, minDelay, maxDelay)
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  if not scene then
    for _, id in ipairs(ids) do main_add_buff(id) end
    return
  end
  local delay = 0
  for i, id in ipairs(ids) do
    delay = delay + minDelay + (maxDelay - minDelay) * math.random()
    local function do_add() main_add_buff(id) end
    scene:runAction(cc.Sequence:create({ cc.DelayTime:create(delay), cc.CallFunc:create(do_add) }))
  end
end
local function main_remove_buff(buffNo)
  local ok = false
  __try('main_player.remove_buff_' .. tostring(buffNo), function()
    if player and type(player.remove_buff) == 'function' then
      local ret = player.remove_buff(player, buffNo)
      ok = true
      return ret
    end
    if player and type(player.rm_buff) == 'function' then
      local ret = player.rm_buff(player, buffNo)
      ok = true
      return ret
    end
    error('main_player remove method missing')
  end)
  return ok
end
local function read_named_buff_list(label, fallback)
  local f = io.open(BUFF_CONFIG_PATH, 'r')
  if not f then return fallback end
  local content = f:read('*a') or ''
  f:close()
  for line in content:gmatch('[^\r\n]+') do
    local code_line = line:gsub('%-%-.*$', '')
    code_line = code_line:gsub('#.*$', '')
    code_line = code_line:match('^%s*(.-)%s*$') or ''
    local foundLabel, rest = code_line:match('^%s*([A-Za-z_]+)%s*[:=]%s*(%b{})')
    if foundLabel and string.upper(foundLabel) == string.upper(label) and rest then
      local inner = rest:sub(2, #rest - 1)
      local ids = {}
      for token in inner:gmatch('[^,]+') do
        local num = tonumber((token:gsub('%s+', '')))
        if num then table.insert(ids, num) end
      end
      if #ids > 0 then
        __add('buff_config.category', label, 'count=' .. tostring(#ids))
        return ids
      end
    end
  end
  return fallback
end
__try('require.buff_invincible', function() local m = require('hexm.common.combat.buff.members.buff_invincible'); __add('buff_invincible', type(m), m) end)
__try('require.buff_misc', function() local m = require('hexm.common.misc.buff_misc'); __add('buff_misc', type(m), m) end)
";
            if (mode == "god") body += @"
for _, id in ipairs(read_named_buff_list('GOD', {70063, 30372, 70005})) do
  add_buff(id)
end
";
            else if (mode == "super_dodge") body += @"
for _, id in ipairs(read_named_buff_list('SUPER_DODGE', {102703, 102704})) do
  add_buff(id)
end
";
            else if (mode == "onehit") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
__try('onehit.add_buff_400148', function() return mp:add_buff(400148) end)
__try('onehit.add_buff_raw_400148', function() return mp.add_buff(mp, 400148) end)
__add('onehit', 'applied_400148')
";
            else if (mode == "atk_mul_2") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
local mul = 2
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_4") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
local mul = 4
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_8") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if not _G.__orig_do_direct_damage and type(mp.do_direct_damage) == 'function' then
  _G.__orig_do_direct_damage = mp.do_direct_damage
end
local mul = 8
if _G.__orig_do_direct_damage then
  mp.do_direct_damage = function(self, amount, ...)
    return _G.__orig_do_direct_damage(self, amount * mul, ...)
  end
  __add('atk_mul.do_direct_damage', 'patched_x' .. tostring(mul))
else
  __add('atk_mul.do_direct_damage', 'orig_missing')
end
_G.__atk_mul = mul
__add('atk_mul', 'x' .. tostring(mul) .. '_applied')
";
            else if (mode == "atk_mul_reset") body += @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then error('main_player missing') end
if _G.__orig_do_direct_damage and type(_G.__orig_do_direct_damage) == 'function' then
  mp.do_direct_damage = _G.__orig_do_direct_damage
  _G.__orig_do_direct_damage = nil
  __add('atk_mul_reset.do_direct_damage', 'restored')
else
  __add('atk_mul_reset.do_direct_damage', 'no_orig')
end
_G.__atk_mul = nil
__add('atk_mul_reset', 'done')
";
            else if (mode == "stamina_lock") body += @"
call_gm_action('set_lock_res_consume', true)
call_gm_combat('gm_set_sp_calc', 1)
call_gm_combat('gm_lock_res_consume', true)
";
            else if (mode == "stamina_dive") body += @"
call_gm_combat('gm_unlimited_dive_resource', true)
";
            else if (mode == "stamina_empty") body += @"
call_gm_combat('gm_empty_combat_resource')
";
            else if (mode == "stamina_reset_all") body += @"
call_gm_action('set_lock_res_consume', false)
call_gm_combat('gm_set_sp_calc', 0)
call_gm_combat('gm_lock_res_consume', false)
call_gm_combat('gm_unlimited_dive_resource', false)
call_gm_combat('gm_reset_combat_resource')
";
            else if (mode == "invis") body += @"
for _, id in ipairs(read_named_buff_list('INVIS', {108010})) do
  add_buff(id)
  main_add_buff(id)
end
";
            else if (mode == "invis_off") body += @"
for _, id in ipairs(read_named_buff_list('INVIS', {108010})) do
  call_gm_action('rm_buff', id)
  remove_buff(id)
  main_remove_buff(id)
end
";
            else if (mode == "god_off") body += @"
for _, id in ipairs(read_named_buff_list('GOD', {70063, 30372, 70005})) do
  call_gm_action('rm_buff', id)
  remove_buff(id)
  main_remove_buff(id)
end
";
            else if (mode == "super_dodge_off") body += @"
for _, id in ipairs(read_named_buff_list('SUPER_DODGE', {102703, 102704})) do
  call_gm_action('rm_buff', id)
  remove_buff(id)
  main_remove_buff(id)
end
";
            else if (mode == "atkbuff_combo") body += @"
__add('国服强力Buff来源', 'Where Winds Meet/Scripts/script_debug.txt 已记录成功施加')
for _, id in ipairs(read_named_buff_list('ATTACK', {1053027,1053026,109927,200005,109506,30302,30314,107031,1053070,10532,200035,200036,102400,102401,102402,102404,102405,102406,102407,102450,102451,102452,102454,102455,102456,102457,109014,109015,109016,109003,109009,109021,109901,109903,109905,109908,109909,109910,109911,109912,109914,109917,109926,109920,109921,109922,109923,102423,102425,109501,109503,109505,109507,109509,109511,109515,70003,70004,70063,109512})) do
  if id == 109501 or id == 109503 or id == 109505 or id == 109507 or id == 109509 or id == 109511 or id == 109515 then
    add_buff_long(id)
    if id == 109515 then refresh_long_buff(109515, 3) end
  else
    add_buff(id)
  end
end
";
            else if (mode == "defbuff") body += @"
for _, id in ipairs(read_named_buff_list('DEFENSE', {30372,30310,70184,200071,200059,200083,200099,200086,30366,30303,30333,30334,30376,30379,30406,102707,70005,102408,102458,102703,102704,200031})) do
  add_buff(id)
end
";
            else if (mode == "cutscene_kill") body += @"
__try('cutscene.clear_log', function()
  local ok, gm_cutscene = pcall(require, 'hexm.client.debug.gm.gm_commands.gm_cutscene')
  if ok and gm_cutscene then
    if gm_cutscene.gm_cutscene_clear_log then pcall(gm_cutscene.gm_cutscene_clear_log) end
    if gm_cutscene.gm_cutscene_debug_terminate then pcall(gm_cutscene.gm_cutscene_debug_terminate) end
  end
end)
__add('cutscene_kill', 'done')
";
            else if (mode.StartsWith("atk_speed_")) body += @"
local targetSpeed = " + mode.Replace("atk_speed_", "") + @"
__add('portable', type(_G.portable), type(portable))
local action = nil
-- 与 Test.lua apply_new_gm_speed 一致：直接用 portable.import
if _G.portable and _G.portable.import then
  local ok, mod = pcall(_G.portable.import, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  __add('atk_speed.portable_import', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and mod then action = mod end
end
if not action then
  local ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  __add('atk_speed.require', ok and type(mod) or 'fail:' .. tostring(mod))
  if ok and mod then action = mod end
end
if action and type(action.set_game_speed) == 'function' then
  pcall(action.set_game_speed, targetSpeed)
  __add('atk_speed', 'applied', targetSpeed)
else
  __add('atk_speed', 'set_game_speed_missing')
end
";
            else if (mode == "atk_speed_reset") body += @"
local action = nil
if _G.portable and _G.portable.import then
  local ok, mod = pcall(_G.portable.import, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if ok and mod then action = mod end
end
if not action then
  local ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if ok and mod then action = mod end
end
if action and type(action.set_game_speed) == 'function' then
  pcall(action.set_game_speed, 1.0)
  __add('atk_speed', 'reset')
else
  __add('atk_speed', 'set_game_speed_missing')
end
";
            return BuildLuaEnvelope("combat_" + mode, body);
        }

        bool ReadMemory(IntPtr hProcess, long address, byte[] buffer)
        {
            IntPtr bytesRead;
            return ReadProcessMemory(hProcess, new IntPtr(address), buffer, new IntPtr(buffer.Length), out bytesRead) && bytesRead.ToInt64() == buffer.Length;
        }

        bool WriteMemory(IntPtr hProcess, long address, byte[] buffer)
        {
            IntPtr bytesWritten;
            return WriteProcessMemory(hProcess, new IntPtr(address), buffer, new IntPtr(buffer.Length), out bytesWritten) && bytesWritten.ToInt64() == buffer.Length;
        }

        long ReadInt64(IntPtr hProcess, long address)
        {
            byte[] buffer = new byte[8];
            if (!ReadMemory(hProcess, address, buffer)) return 0;
            return BitConverter.ToInt64(buffer, 0);
        }

        float ReadFloat(IntPtr hProcess, long address)
        {
            byte[] buffer = new byte[4];
            if (!ReadMemory(hProcess, address, buffer)) return float.NaN;
            return BitConverter.ToSingle(buffer, 0);
        }

        bool WriteFloat(IntPtr hProcess, long address, float value)
        {
            return WriteMemory(hProcess, address, BitConverter.GetBytes(value));
        }

        long FollowPointerChain(IntPtr hProcess, long baseAddress, long[] offsets)
        {
            long address = baseAddress;
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                address = ReadInt64(hProcess, address + offsets[i]);
                if (address == 0) return 0;
            }
            return address + offsets[offsets.Length - 1];
        }

        void SetMemorySpeed(bool reset)
        {
            Process[] procs = Process.GetProcessesByName("yysls");
            if (procs.Length == 0) { AppendLog("内存速度失败: 未找到游戏进程"); return; }
            Process proc = procs[0];
            // 释放未使用的 Process 句柄
            for (int i = 1; i < procs.Length; i++) SafeDisposeProcess(procs[i]);
            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, proc.Id);
            if (hProcess == IntPtr.Zero) { AppendLog("内存速度失败: 无法打开进程"); SafeDisposeProcess(proc); return; }
            try
            {
                long moduleBase = proc.MainModule.BaseAddress.ToInt64();
                long globalBase = moduleBase + GLOBAL_BASE_OFFSET;
                int okCount = 0;
                foreach (MemorySpeedEntry entry in memorySpeedEntries)
                {
                    long address = FollowPointerChain(hProcess, globalBase, entry.Offsets);
                    if (address == 0) { AppendLog(entry.Name + ": 偏移链无效"); continue; }
                    float before = ReadFloat(hProcess, address);
                    float value = reset ? 1.0f : entry.Value;
                    bool ok = WriteFloat(hProcess, address, value);
                    float after = ReadFloat(hProcess, address);
                    AppendLog(string.Format("{0}: 0x{1:X} {2:F3}->{3:F3} {4}", entry.Name, address, before, after, ok ? "OK" : "FAIL"));
                    if (ok) okCount++;
                }
                AppendLog(reset ? "恢复速度完成，成功项: " + okCount : "内存加速完成，成功项: " + okCount);
            }
            catch (Exception ex) { AppendLog("内存速度异常: " + ex.Message); }
            finally { CloseHandle(hProcess); SafeDisposeProcess(proc); }
        }

        void UpdateStatus()
        {
            SyncReadyState(false);
            string status = "";
            Color color = Color.Blue;
            Process[] procs = Process.GetProcessesByName("yysls");
            bool gameRunning = procs.Length > 0;
            try
            {
                if (string.IsNullOrEmpty(gameRootPath)) { status = "游戏未运行 - 请先设置目录"; color = Color.Gray; }
                else if (!gameRunning && !gameLaunched) { status = "游戏未运行 - " + gameRootPath; color = Color.Gray; }
                else if (!gameRunning && gameLaunched) { status = "游戏可能已退出"; color = Color.Red; gameLaunched = false; }
                else if (isReady) { status = "已就绪 - 可以使用 GM 命令"; color = Color.Green; }
                else if (File.Exists(Path.Combine(ToolDir, "core.config"))) { status = "工具文件已就位 - 注入后检查 gm_tool.log"; color = Color.Orange; }
                else { status = "游戏运行中 - 点[注入]"; color = Color.Blue; }

                lblStatus.Text = "状态: " + status;
                lblStatus.ForeColor = color;
                SetGMEnabled(isReady);
            }
            finally { SafeDisposeProcesses(procs); }
        }

        void SetGMEnabled(bool enabled)
        {
            SetButtonsEnabled(grpGM, enabled);
        }

        void ApplyCleanStyle()
        {
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(241, 245, 249);
            ApplyCleanStyleRecursive(this);
        }

        void ApplyCleanStyleRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                control.Font = Font;

                GroupBox group = control as GroupBox;
                if (group != null)
                {
                    group.ForeColor = Color.FromArgb(45, 55, 72);
                    group.BackColor = BackColor;
                }

                Panel panel = control as Panel;
                if (panel != null)
                {
                    if (panel == grpGM || panel == tabGM)
                        panel.BackColor = Color.White;
                    else if (panel.Size.Height >= 100 && panel.Size.Width >= 300)
                        panel.BackColor = Color.FromArgb(248, 250, 252);
                    else
                        panel.BackColor = Color.White;
                }

                Label label = control as Label;
                if (label != null && label.ForeColor == SystemColors.ControlText)
                {
                    label.ForeColor = Color.FromArgb(74, 85, 104);
                }

                Button button = control as Button;
                if (button != null)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(219, 234, 254);
                    button.ForeColor = Color.FromArgb(31, 41, 55);
                    if (button.BackColor == SystemColors.Control || button.BackColor == Color.Empty)
                    {
                        button.BackColor = Color.White;
                    }
                    button.Height = Math.Max(button.Height, 28);
                    button.UseVisualStyleBackColor = false;
                }

                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = Color.FromArgb(31, 41, 55);
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    if (textBox.Multiline)
                    {
                        textBox.Font = new Font("Consolas", 9F);
                    }
                }

                ComboBox combo = control as ComboBox;
                if (combo != null)
                {
                    combo.BackColor = Color.White;
                    combo.ForeColor = Color.FromArgb(31, 41, 55);
                    combo.FlatStyle = FlatStyle.Flat;
                }

                if (control.HasChildren) ApplyCleanStyleRecursive(control);
            }
        }

        void SetButtonsEnabled(Control parent, bool enabled)
        {
            foreach (Control control in parent.Controls)
            {
                // 初始标签页的按钮始终可点击
                if (parent == tabInit || parent == grpGM && control == tabInit) continue;
                Button button = control as Button;
                if (button != null && (button.Tag as string) != "nav") button.Enabled = enabled;
                CheckBox chk = control as CheckBox;
                if (chk != null) chk.Enabled = enabled;
                ComboBox combo = control as ComboBox;
                if (combo != null) combo.Enabled = enabled;
                if (control.HasChildren) SetButtonsEnabled(control, enabled);
            }
        }

        void CheckState()
        {
            SyncReadyState(true);
            UpdateStatus();
            UpdateLogView();
        }

        bool IsConnectorReadyFromLog()
        {
            try
            {
                // 优先检查随机化日志文件，其次检查默认名
                string gameLogPath = GetGameFilePath(Path.GetFileName(UnifiedLogFile));
                string activeLogPath = GetPreferredExistingPath(gameLogPath, UnifiedLogFile);
                // 也检查旧默认名
                if (string.IsNullOrEmpty(activeLogPath) || !File.Exists(activeLogPath))
                {
                    string oldLogPath = GetGameFilePath("gm_tool.log");
                    activeLogPath = GetPreferredExistingPath(oldLogPath, Path.Combine(ToolDir, "gm_tool.log"));
                }
                if (string.IsNullOrEmpty(activeLogPath) || !File.Exists(activeLogPath)) return false;
                string text = File.ReadAllText(activeLogPath);
                if (string.IsNullOrEmpty(text)) return false;
                bool readyLogged =
                    text.Contains("Ready. Command direct executor is polling:") ||
                    text.Contains("Ready file created:") ||
                    text.Contains("Ready. Mode:");
                bool capturedState =
                    text.Contains("CAPTURED L=") ||
                    text.Contains("CMD source:");
                return readyLogged && capturedState;
            }
            catch
            {
                return false;
            }
        }

        void SyncReadyState(bool appendReadyLog)
        {
            bool autoLoadMode = !string.IsNullOrEmpty(gameBinPath);
            bool detected = IsConnectorReadyFromLog();
            if (!detected)
            {
                if (!autoLoadMode)
                {
                    if (isReady) AppendLog("GM 就绪状态丢失; GM 按钮已禁用。");
                    isReady = false;
                }
                return;
            }
            if (!isReady && appendReadyLog) AppendLog("Lua ready.");
            isReady = true;
        }

        void UpdateLogView()
        {
            
        }

        string BuildUnifiedLogSnapshot()
        {
            string gameLogPath = GetGameFilePath(Path.GetFileName(UnifiedLogFile));
            string activeLogPath = GetPreferredExistingPath(gameLogPath, UnifiedLogFile);
            // 也检查旧默认名
            if (string.IsNullOrEmpty(activeLogPath) || !File.Exists(activeLogPath))
            {
                string oldLogPath = GetGameFilePath("gm_tool.log");
                activeLogPath = GetPreferredExistingPath(oldLogPath, Path.Combine(ToolDir, "gm_tool.log"));
            }
            return File.Exists(activeLogPath) ? activeLogPath : "";
        }

        void ClearManagedFiles()
        {
            string[] paths = new string[] { CmdFile, CmdResultFile, ToolResultFile, ToolResultCompatFile, UnifiedLogFile, Path.Combine(ToolDir, "ready.txt"), Path.Combine(ToolDir, "trace.txt"), Path.Combine(ToolDir, "connector_log.txt"), Path.Combine(ToolDir, "gm_tool_ui.log"), Path.Combine(ToolDir, "gm_tool_all.log"), Path.Combine(ToolDir, "gm_signal.txt"), Path.Combine(ToolDir, "svc.cfg") };
            foreach (string path in paths)
            {
                SafeDeleteFile(path, "清理托管文件失败");
            }
            UpdateLogView();
        }

        void AppendLog(string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            try { File.AppendAllText(UnifiedLogFile, line + Environment.NewLine, new UTF8Encoding(false)); } catch (Exception ex) { Debug.WriteLine("AppendLog write failed: " + ex.Message); }
        }

        void SaveConfig() { try { File.WriteAllText(ConfigFile, gameRootPath); } catch (Exception ex) { Debug.WriteLine("SaveConfig failed: " + ex.Message); } }
        void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string path = File.ReadAllText(ConfigFile).Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        string binPath = Path.Combine(path, GameSubPath);
                        if (!File.Exists(Path.Combine(binPath, GameExeName)))
                            binPath = Path.Combine(path, GameSubPathAlt);
                        if (File.Exists(Path.Combine(binPath, GameExeName)))
                        { gameRootPath = path; gameBinPath = binPath; UpdateCommPaths(); }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("LoadConfig failed: " + ex.Message); }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            readyPollTimer = new System.Windows.Forms.Timer();
            readyPollTimer.Interval = 3000;
            readyPollTimer.Tick += delegate(object s, EventArgs e2)
            {
                bool wasReady = isReady;
                SyncReadyState(!wasReady);
                if (!wasReady && isReady) UpdateStatus();
                UpdateLogView();
                if (gameLaunched)
                {
                    var ps = Process.GetProcessesByName("yysls");
                    bool none = ps.Length == 0;
                    SafeDisposeProcesses(ps);
                    if (none) { gameLaunched = false; isReady = false; UpdateStatus(); }
                }
            };
            readyPollTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { if (commandResultTimer != null) { commandResultTimer.Stop(); commandResultTimer.Dispose(); commandResultTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose commandResultTimer failed: " + ex.Message); }
            try { if (injectionDiagTimer != null) { injectionDiagTimer.Stop(); injectionDiagTimer.Dispose(); injectionDiagTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose injectionDiagTimer failed: " + ex.Message); }
            try { if (readyPollTimer != null) { readyPollTimer.Stop(); readyPollTimer.Dispose(); readyPollTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose readyPollTimer failed: " + ex.Message); }
            try { ClearManagedFiles(); } catch (Exception ex) { Debug.WriteLine("OnFormClosing ClearManagedFiles failed: " + ex.Message); }
            try { if (mmfAccessor != null) mmfAccessor.Dispose(); } catch (Exception ex) { Debug.WriteLine("Dispose mmfAccessor failed: " + ex.Message); }
            try { if (mmf != null) mmf.Dispose(); } catch (Exception ex) { Debug.WriteLine("Dispose mmf failed: " + ex.Message); }
            base.OnFormClosing(e);
        }

        [STAThread]
        static void Main(string[] args)
        {
            EnsureModernTls();
            CleanupLegacyManifestFiles();
            StartupManifest = LoadStartupManifest();
            if (!EnsureVersionAllowed(StartupManifest)) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string gadgetPath = Path.Combine(exeDir, "core.dll");
            string deployedMarker = Application.ExecutablePath + ".deployed";
            bool isRunArg = args.Length > 0 && args[0] == "--run";

            if (!File.Exists(gadgetPath) && IsPackedExe())
            {
                if (PerformAntiDetectDeployment()) return;
            }
            else if (File.Exists(deployedMarker) && !File.Exists(gadgetPath))
            {
                string deployedExe = File.ReadAllText(deployedMarker).Trim();
                if (File.Exists(deployedExe))
                {
                    string deployDir = Path.GetDirectoryName(deployedExe);
                    MessageBox.Show("已部署到：\n" + deployDir + "\n\n请使用桌面快捷方式运行。", "已部署", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SafeDeleteFile(deployedMarker, "删除部署标记失败");
                if (PerformAntiDetectDeployment()) return;
            }
            else if (File.Exists(gadgetPath) && !isRunArg)
            {
                SpawnRandomCopyAndExit();
                return;
            }

            Application.Run(new GMForm());
        }

        static string GenerateSystemLikeExeName(Random rnd)
        {
            string[] prefixes = { "Win", "Sys", "Net", "Service", "Session", "Task", "Audio", "Print", "Policy", "Credential", "Desktop", "Update", "Storage", "Index", "Time", "Remote", "Local", "Global", "Shell", "Microsoft" };
            string[] suffixes = { "Host", "Service", "Agent", "Worker", "Manager", "Broker", "Provider", "Helper", "Support", "Runtime", "Session", "Task", "Client", "Server", "Shell", "Config" };
            string name = prefixes[rnd.Next(prefixes.Length)] + suffixes[rnd.Next(suffixes.Length)];
            if (rnd.Next(3) == 0) name += rnd.Next(10, 999).ToString();
            return name + ".exe";
        }

        static void SpawnRandomCopyAndExit()
        {
            try
            {
                string currentExe = Application.ExecutablePath;
                string deployDir = Path.GetDirectoryName(currentExe);
                string randomExeName = GenerateSystemLikeExeName(new Random());
                string randomExePath = Path.Combine(deployDir, randomExeName);

                foreach (string file in Directory.GetFiles(deployDir, "*.exe"))
                {
                    string name = Path.GetFileName(file);
                    if (name.Equals(Path.GetFileName(currentExe), StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals(randomExeName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains(" ") || name.Contains("Extreme") || name.Contains("Inject")) continue;
                    SafeDeleteFile(file, "清理旧随机副本失败");
                }

                File.Copy(currentExe, randomExePath, true);

                Process.Start(new ProcessStartInfo
                {
                    FileName = randomExePath,
                    Arguments = "--run",
                    WorkingDirectory = deployDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动随机副本失败: " + ex.Message);
            }
        }

        static string GenerateDeployRootPath()
        {
            string rootDrive = Directory.Exists("D:\\") ? "D:\\" : "C:\\";
            // 伪装成 Windows IE/Edge 缓存目录结构，降低路径异常特征
            string basePath = Path.Combine(rootDrive, "ProgramData", "Microsoft", "Windows", "INetCache", "IE", "Content.IE5");
            Random rnd = new Random();
            string folder = GenerateRandomHexName(rnd, 8);
            string p = Path.Combine(basePath, folder);
            Directory.CreateDirectory(p);
            return p;
        }

        static string GenerateRandomHexName(Random rnd, int len)
        {
            const string hex = "0123456789ABCDEF";
            StringBuilder sb = new StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(hex[rnd.Next(hex.Length)]);
            return sb.ToString();
        }

        static bool IsPackedExe()
        {
            try
            {
                using (FileStream fs = new FileStream(Application.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 14) return false;
                    fs.Seek(-10, SeekOrigin.End);
                    byte[] markerBytes = new byte[10];
                    fs.Read(markerBytes, 0, 10);
                    return Encoding.ASCII.GetString(markerBytes) == "SVCPACK___";
                }
            }
            catch { return false; }
        }

        static bool ExtractPayload(string targetDir)
        {
            using (FileStream fs = new FileStream(Application.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(-10, SeekOrigin.End);
                byte[] markerBytes = new byte[10];
                fs.Read(markerBytes, 0, 10);
                if (Encoding.ASCII.GetString(markerBytes) != "SVCPACK___") return false;

                fs.Seek(-14, SeekOrigin.End);
                byte[] sizeBytes = new byte[4];
                fs.Read(sizeBytes, 0, 4);
                int payloadSize = BitConverter.ToInt32(sizeBytes, 0);

                fs.Seek(-14 - payloadSize, SeekOrigin.End);
                byte[] payload = new byte[payloadSize];
                fs.Read(payload, 0, payloadSize);

                int offset = 0;
                int count = BitConverter.ToInt32(payload, offset); offset += 4;
                for (int i = 0; i < count; i++)
                {
                    int nameLen = BitConverter.ToInt32(payload, offset); offset += 4;
                    string name = Encoding.UTF8.GetString(payload, offset, nameLen); offset += nameLen;
                    int fileLen = BitConverter.ToInt32(payload, offset); offset += 4;
                    byte[] data = new byte[fileLen];
                    Buffer.BlockCopy(payload, offset, data, 0, fileLen); offset += fileLen;
                    string targetPath = Path.Combine(targetDir, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    File.WriteAllBytes(targetPath, data);
                }
            }
            return true;
        }

        static void CreateDesktopShortcut(string targetPath, string workingDir)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktop, "Windows系统服务.lnk");
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Save();
        }

        static bool PerformAntiDetectDeployment()
        {
            string deployedMarker = Application.ExecutablePath + ".deployed";
            bool isUpdate = false;
            string existingExe = null;
            string deployDir = null;

            if (File.Exists(deployedMarker))
            {
                existingExe = File.ReadAllText(deployedMarker).Trim();
                if (File.Exists(existingExe))
                {
                    deployDir = Path.GetDirectoryName(existingExe);
                    if (MessageBox.Show("已部署到：\n" + deployDir + "\n\n是否覆盖更新？", "已部署", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        MessageBox.Show("请使用桌面快捷方式运行。", "已部署", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                    isUpdate = true;
                }
                SafeDeleteFile(deployedMarker, "删除旧部署标记失败");
            }

            if (!isUpdate)
            {
                if (MessageBox.Show("首次运行，将执行安全部署：所有文件会随机放置到D盘5层目录，并创建桌面快捷方式。\n\n是否继续？", "安全部署", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return false;
            }

            try
            {
                if (string.IsNullOrEmpty(deployDir))
                    deployDir = GenerateDeployRootPath();

                if (Directory.Exists(deployDir))
                {
                    foreach (string file in Directory.GetFiles(deployDir, "*", SearchOption.AllDirectories))
                    {
                        SafeDeleteFile(file, "清理部署目录文件失败");
                    }
                }

                if (!ExtractPayload(deployDir))
                {
                    MessageBox.Show("无法从当前 exe 提取文件，可能不是单文件版。");
                    return false;
                }
                EnsurePayloadToolDir(deployDir);

                string extractedExe = Path.Combine(deployDir, "app.exe");
                if (!File.Exists(extractedExe))
                {
                    MessageBox.Show("部署失败：未找到主程序文件");
                    return false;
                }

                if (!string.IsNullOrEmpty(existingExe) && File.Exists(existingExe))
                {
                    SafeDeleteFile(existingExe, "删除旧部署主程序失败");
                }

                string randomExeName = GenerateSystemLikeExeName(new Random());
                string deployedExe = Path.Combine(deployDir, randomExeName);
                File.Move(extractedExe, deployedExe);

                CreateDesktopShortcut(deployedExe, deployDir);
                File.WriteAllText(deployedMarker, deployedExe, new UTF8Encoding(false));

                string msg = isUpdate ? "覆盖更新完成！" : "部署完成！";
                MessageBox.Show(msg + "\n所有文件已放置到：\n" + deployDir + "\n主程序已随机命名为：" + randomExeName + "\n桌面快捷方式已创建，请从桌面运行。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("部署失败: " + ex.Message);
                return false;
            }
        }
    }
}
