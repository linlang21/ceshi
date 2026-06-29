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
            public string Notice;
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
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);
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
        // ===== 按键模拟 (PostMessageW 方案, 绕过游戏位移回滚) =====
        // 关键发现: AAA.exe 用 PostMessageW 而非 SendInput, 可后台发消息无需游戏前台
        // banyi 早期用 SendInput 会被游戏拉回, 改用 PostMessageW 对齐 AAA 行为
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr FindWindowA(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const ushort VK_SPACE = 0x20;
        const ushort VK_Q = 0x51;
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;
        const uint WM_SYSKEYDOWN = 0x0104;
        const uint WM_SYSKEYUP = 0x0105;

        // ===== 低级键盘钩子 (WH_KEYBOARD_LL, 全局监听, 游戏前台也能捕获) =====
        // 用于"飞天遁地"和"瞬移"模式: 勾选后监听小键盘上下/Alt+方向键触发微调传送
        const int WH_KEYBOARD_LL = 13;
        const uint WM_KEYDOWN_LL = 0x0100;
        const uint WM_KEYUP_LL = 0x0101;
        const uint WM_SYSKEYDOWN_LL = 0x0104;
        const uint WM_SYSKEYUP_LL = 0x0105;
        const ushort VK_UP = 0x26;
        const ushort VK_DOWN = 0x28;
        const ushort VK_LEFT = 0x25;
        const ushort VK_RIGHT = 0x27;
        const ushort VK_MENU = 0x12;         // Alt

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        IntPtr nudgeKbHook = IntPtr.Zero;
        bool flyModeEnabled = false;     // 飞天遁地: 小键盘上下瞬移
        bool nudgeModeEnabled = false;   // 瞬移: Alt+方向键东南西北瞬移
        // 防止按键长按连续触发 (一次按下只传送一次, 抬起后才能再次触发)
        bool flyKeyUp = true, flyKeyDown = true;
        bool nudgeKeyLeft = true, nudgeKeyRight = true, nudgeKeyUp = true, nudgeKeyDown = true;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }
        [StructLayout(LayoutKind.Explicit)]
        struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [DllImport("ntdll.dll")]
        static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, byte[] processInformation, int processInformationLength, out int returnLength);
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;
        const uint PAGE_EXECUTE_READWRITE = 0x40;
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
        DataGridView dgvCoords;
        TextBox txtCoordRemark, txtCoordFileName;
        Label lblLiveCoord;  // 实时坐标显示 (替代原 X/Y/Z 输入框, 实时刷新)
        System.Windows.Forms.Timer liveCoordTimer;  // 实时坐标刷新定时器
        CheckBox chkEnableMemory;
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
                Notice = DefaultNoticeText,
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
                    case "notice":
                        info.Notice = NormalizeManifestText(value);
                        break;
                    case "block_message":
                        info.BlockMessage = NormalizeManifestText(value);
                        break;
                    case "block_on_manifest_error":
                        bool blockOnError;
                        if (bool.TryParse(value, out blockOnError)) info.BlockOnManifestError = blockOnError;
                        break;
                    case "cache_max_age_minutes":
                        // 读取但不存储，仅用于缓存有效期判断
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
            merged.Notice = source.Notice;
            merged.BlockMessage = source.BlockMessage;
            merged.BlockOnManifestError = source.BlockOnManifestError;
            merged.RemoteLoaded = source.RemoteLoaded;
            merged.ManifestSource = source.ManifestSource;
            merged.ManifestError = source.ManifestError;

            if (overrideInfo == null) return merged;
            if (!string.IsNullOrWhiteSpace(overrideInfo.LatestVersion)) merged.LatestVersion = overrideInfo.LatestVersion;
            if (!string.IsNullOrWhiteSpace(overrideInfo.Notice)) merged.Notice = overrideInfo.Notice;
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
                sb.AppendLine("notice=" + (string.IsNullOrWhiteSpace(manifest.Notice) ? DefaultNoticeText : manifest.Notice).Replace(Environment.NewLine, "\\n"));
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

            Version latestVersion = ParseVersionText(effectiveManifest.LatestVersion);
            if (CurrentVersion.CompareTo(latestVersion) >= 0) return true;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.IsNullOrWhiteSpace(effectiveManifest.BlockMessage) ? "当前版本已停用，请更新到最新版后再使用。" : effectiveManifest.BlockMessage);
            sb.AppendLine();
            sb.AppendLine("最新版本：v" + effectiveManifest.LatestVersion);
            MessageBox.Show(sb.ToString().TrimEnd(), "版本已停用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        static string BuildAnnouncementText(VersionManifestInfo manifest)
        {
            VersionManifestInfo effectiveManifest = manifest ?? CreateDefaultManifest();
            if (!string.IsNullOrWhiteSpace(effectiveManifest.Notice))
            {
                return effectiveManifest.Notice.Trim();
            }
            return "";
        }

        public GMForm()
        {
            Text = "FridaGM 工具 v" + CurrentVersionText;
            Size = new Size(560, 720);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 4;

            // === GM 命令 ===
            grpGM = new Panel { Location = new Point(8, y), Size = new Size(536, 674), BackColor = Color.White };
            const int rowStep = 34;
            const int sectionGap = 8;
            chkGod = new CheckBox { Text = "无敌", Size = new Size(118, 28) };
            chkGod.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkGod.Text, BuildCombatExperimentLua(chkGod.Checked ? "god" : "god_off")); };
            btnStamina = new Button { Text = "锁体力消耗", Size = new Size(118, 28) };
            btnStamina.Click += (s, e) => SendExperimentCommand("锁体力消耗", BuildCombatExperimentLua("stamina_lock"));
            btnStaminaDive = new Button { Text = "无限潜水资源", Size = new Size(118, 28) };
            btnStaminaDive.Click += (s, e) => SendExperimentCommand("无限潜水资源", BuildCombatExperimentLua("stamina_dive"));
            chkInvis = new CheckBox { Text = "隐身", Size = new Size(118, 28) };
            chkInvis.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkInvis.Text, BuildCombatExperimentLua(chkInvis.Checked ? "invis" : "invis_off")); };

            btnStaminaEmpty = new Button { Text = "清空战斗资源", Size = new Size(118, 28) };
            btnStaminaEmpty.Click += (s, e) => SendExperimentCommand("清空战斗资源", BuildCombatExperimentLua("stamina_empty"));
            btnStaminaResetAll = new Button { Text = "恢复体力设置", Size = new Size(118, 28) };
            btnStaminaResetAll.Click += (s, e) => SendExperimentCommand("恢复体力设置", BuildCombatExperimentLua("stamina_reset_all"));

            chkNpcDumb = new CheckBox { Text = "NPC变笨", Size = new Size(118, 28) };
            chkNpcDumb.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkNpcDumb.Text, BuildYyLaoLiuLua(chkNpcDumb.Checked ? "yy_npcdumb" : "yy_npcdumb_off")); };
            chkSuperDodge = new CheckBox { Text = "超级闪避", Size = new Size(118, 28) };
            chkSuperDodge.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) SendExperimentCommand(chkSuperDodge.Text, BuildCombatExperimentLua(chkSuperDodge.Checked ? "super_dodge" : "super_dodge_off")); };

            btnAtkBuff = new Button { Text = "攻击Buff", Size = new Size(118, 28) };
            btnAtkBuff.Click += (s, e) => SendExperimentCommand("攻击Buff整合", BuildCombatExperimentLua("atkbuff_combo"));
            btnDefBuff = new Button { Text = "防御Buff", Size = new Size(118, 28) };
            btnDefBuff.Click += (s, e) => SendExperimentCommand("防御Buff", BuildCombatExperimentLua("defbuff"));
            btnMinBuff = new Button { Text = "最小Buff", Size = new Size(118, 28), BackColor = Color.LightGreen };
            btnMinBuff.Click += (s, e) => SendExperimentCommand("最小Buff", BuildLoopFeatureLua("minimal_buff"));

            btnGatherBuff = new Button { Text = "采集Buff", Size = new Size(118, 28) };
            btnGatherBuff.Click += (s, e) => SendExperimentCommand("采集Buff", BuildLoopFeatureLua("gather_buff"));

            btnAuxBuff = new Button { Text = "辅助Buff", Size = new Size(118, 28) };
            btnAuxBuff.Click += (s, e) => SendExperimentCommand("辅助Buff", BuildLoopFeatureLua("aux_buff"));
            btnUnknownBuff = new Button { Text = "未知Buff", Size = new Size(118, 28) };
            btnUnknownBuff.Click += (s, e) => SendExperimentCommand("未知Buff", BuildLoopFeatureLua("unknown_buff"));
            var btnRemoveAllBuffs = new Button { Text = "移除全部Buff", Size = new Size(118, 28) };
            btnRemoveAllBuffs.Click += (s, e) => SendExperimentCommand("移除全部Buff", BuildLoopFeatureLua("remove_all_buffs"));
            btnStealthFlags = new Button { Text = "关闭安全标志", Size = new Size(118, 28) };
            btnStealthFlags.Click += (s, e) => SendExperimentCommand("关闭安全标志", BuildLoopFeatureLua("stealth_flags"));
            var btnYyRemoveBuff = new Button { Text = "备用移除Buff", Size = new Size(118, 28) };
            btnYyRemoveBuff.Click += (s, e) => SendExperimentCommand("备用移除Buff", BuildYyLaoLiuBuffToolLua("yy_remove_buffs"));

            btnLoopBuff = new Button { Text = "循环强力Buff", Size = new Size(118, 28) };
            btnLoopBuff.Click += (s, e) => SendExperimentCommand("循环强力Buff", BuildLoopFeatureLua("loop_buff"));
            btnLoopDefense = new Button { Text = "循环防御Buff", Size = new Size(118, 28) };
            btnLoopDefense.Click += (s, e) => SendExperimentCommand("循环防御Buff", BuildLoopFeatureLua("loop_defense"));
            btnLoopLoot = new Button { Text = "循环自动拾取", Size = new Size(118, 28), BackColor = Color.FromArgb(255, 200, 200) };
            btnLoopLoot.Click += (s, e) => SendExperimentCommand("循环自动拾取", BuildLoopFeatureLua("loop_loot"));
            btnLoopRecover = new Button { Text = "循环自动恢复", Size = new Size(118, 28) };
            btnLoopRecover.Click += (s, e) => SendExperimentCommand("循环自动恢复", BuildLoopFeatureLua("loop_recover"));

            btnYyAutoLoot = new Button { Text = "自动拾取", Size = new Size(118, 28) };
            btnYyAutoLoot.Click += (s, e) => SendExperimentCommand("自动拾取", BuildLoopFeatureLua("loot_once"));
            btnYyRecover = new Button { Text = "一键恢复", Size = new Size(118, 28) };
            btnYyRecover.Click += (s, e) => SendExperimentCommand("一键恢复", BuildYyLaoLiuLua("yy_recover"));

            btnRhythmGame = new Button { Text = "NPC节奏游戏", Size = new Size(118, 28) };
            btnRhythmGame.Click += (s, e) => SendExperimentCommand("NPC节奏游戏", BuildGameFeatureLua("rhythm_game"));
            btnChessWin = new Button { Text = "象棋秒赢", Size = new Size(118, 28) };
            btnChessWin.Click += (s, e) => SendExperimentCommand("象棋秒赢", BuildGameFeatureLua("chess_win"));
            btnPitchPot = new Button { Text = "投壶圈变大", Size = new Size(118, 28) };
            btnPitchPot.Click += (s, e) => SendExperimentCommand("投壶圈变大", BuildGameFeatureLua("pitch_pot_easy"));

            chkOneHit = new CheckBox { Text = "一击必杀", Size = new Size(118, 28) };
            chkOneHit.CheckedChanged += (s, e) => { if (!suppressCheckboxEvents) { if (chkOneHit.Checked) SendExperimentCommand("一击必杀", BuildCombatExperimentLua("onehit")); else SendExperimentCommand("还原一击必杀", BuildLoopFeatureLua("onehit_off")); } };

            cmbAtkMul = new ComboBox { Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAtkMul.Items.AddRange(new object[] { "x2", "x4", "x8" });
            cmbAtkMul.SelectedIndex = 0;
            btnApplyAtkMul = new Button { Text = "应用倍率", Size = new Size(100, 28) };
            btnApplyAtkMul.Click += (s, e) => ApplyAttackMultiplierSelection();
            btnResetAtkMul = new Button { Text = "还原倍率", Size = new Size(100, 28) };
            btnResetAtkMul.Click += (s, e) => SendExperimentCommand("还原攻击倍率", BuildCombatExperimentLua("atk_mul_reset"));

            txtDialogSpeed = new TextBox { Size = new Size(120, 24), Text = "80" };
            btnApplyDialogSpeed = new Button { Text = "应用速度", Size = new Size(100, 28) };
            btnApplyDialogSpeed.Click += (s, e) => ApplyDialogSpeedSelection();
            btnResetDialogSpeed = new Button { Text = "还原速度", Size = new Size(100, 28) };
            btnResetDialogSpeed.Click += (s, e) => SendExperimentCommand("还原速度", BuildLoopFeatureLua("dialog_speed_reset"));

            cmbAtkSpeed = new ComboBox { Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAtkSpeed.Items.AddRange(new object[] { "x1.5", "x3", "x5", "x7.5", "x10", "x30" });
            cmbAtkSpeed.SelectedIndex = 0;
            btnApplyAtkSpeed = new Button { Text = "应用速度", Size = new Size(100, 28) };
            btnApplyAtkSpeed.Click += (s, e) => ApplyAtkSpeedSelection();
            btnResetAtkSpeed = new Button { Text = "还原攻击速度", Size = new Size(100, 28) };
            btnResetAtkSpeed.Click += (s, e) => SendExperimentCommand("还原攻击速度", BuildCombatExperimentLua("atk_speed_reset"));
            grpGM.Size = new Size(536, 540);
            tabGM = new Panel { Location = new Point(6, 6), Size = new Size(524, 528), BackColor = Color.White };
            var tabNav = new Panel { Location = new Point(0, 0), Size = new Size(524, 42), BackColor = Color.White };
            var btnTabInit = new Button { Text = "快速启动", Location = new Point(0, 6), Size = new Size(100, 28), Tag = "nav" };
            var btnTabBattle = new Button { Text = "功能", Location = new Point(105, 6), Size = new Size(100, 28), Tag = "nav" };
            var btnTabBuff = new Button { Text = "Buff", Location = new Point(210, 6), Size = new Size(100, 28), Tag = "nav" };
            var btnTabTools = new Button { Text = "工具", Location = new Point(315, 6), Size = new Size(100, 28), Tag = "nav" };
            var btnTabCoord = new Button { Text = "传送", Location = new Point(420, 6), Size = new Size(100, 28), Tag = "nav" };
            tabNav.Controls.Add(btnTabInit);
            tabNav.Controls.Add(btnTabBattle);
            tabNav.Controls.Add(btnTabBuff);
            tabNav.Controls.Add(btnTabTools);
            tabNav.Controls.Add(btnTabCoord);
            tabGM.Controls.Add(tabNav);

            tabInit = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 486), BackColor = Color.White };
            var tabBattle = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 486), BackColor = Color.White };
            var tabBuff = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 486), BackColor = Color.White };
            var tabTools = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 486), BackColor = Color.White };
            var tabCoord = new ThinScrollPanel { Location = new Point(0, 42), Size = new Size(524, 486), BackColor = Color.White };
            tabGM.Controls.Add(tabInit);
            tabGM.Controls.Add(tabBattle);
            tabGM.Controls.Add(tabBuff);
            tabGM.Controls.Add(tabTools);
            tabGM.Controls.Add(tabCoord);

            Button[] tabButtons = new Button[] { btnTabInit, btnTabBattle, btnTabBuff, btnTabTools, btnTabCoord };
            Panel[] tabPages = new Panel[] { tabInit, tabBattle, tabBuff, tabTools, tabCoord };
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
            btnTabCoord.Click += (s, e) => activateTab(tabCoord, btnTabCoord);

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
            Button btnInitCoordHook = new Button { Text = "初始化内存", Location = new Point(12, yInit[0]), Size = new Size(130, 26), Tag = "init" };
            btnInitCoordHook.Click += (s, ev) => InitCoordHook();
            tabInit.Controls.Add(btnInitCoordHook);
            yInit[0] += 30;
            chkEnableMemory = new CheckBox { Text = "启用内存功能", Location = new Point(12, yInit[0]), Size = new Size(130, 22), ForeColor = Color.FromArgb(192, 0, 0), Checked = false };
            chkEnableMemory.CheckedChanged += chkEnableMemory_CheckedChanged;
            tabInit.Controls.Add(chkEnableMemory);
            yInit[0] += 26;
            tabInit.Controls.Add(new Label { Text = "── 公告 ──", Location = new Point(12, yInit[0]), Size = new Size(500, 14), ForeColor = Color.FromArgb(120, 120, 120), Font = new Font("Microsoft YaHei", 7, FontStyle.Bold) });
            yInit[0] += 18;
            var noticePanel = new Panel { Location = new Point(12, yInit[0]), Size = new Size(492, 76), BackColor = Color.FromArgb(248, 250, 252) };
            lblNotice = new Label { Text = BuildAnnouncementText(StartupManifest), Location = new Point(12, 10), Size = new Size(468, 56), ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Microsoft YaHei UI", 9F) };
            noticePanel.Controls.Add(lblNotice);
            tabInit.Controls.Add(noticePanel);

            int[] yBattle = new int[] { 4 };
            int[] yBuff = new int[] { 4 };
            int[] yTools = new int[] { 4 };
            int[] yCoord = new int[] { 4 };
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
            btnCutsceneKill = new Button { Text = "终止过场动画", Size = new Size(118, 28) };
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

            addTabSection(tabCoord, "坐标管理", yCoord);

            dgvCoords = new DataGridView {
                Location = new Point(12, yCoord[0]),
                Size = new Size(360, 330),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Microsoft YaHei UI", 9F),
                Tag = "mem"
            };
            dgvCoords.Columns.Add("colIndex", "序号");
            dgvCoords.Columns.Add("colX", "X");
            dgvCoords.Columns.Add("colY", "Y");
            dgvCoords.Columns.Add("colZ", "Z");
            dgvCoords.Columns.Add("colRemark", "备注");
            dgvCoords.RowTemplate.Height = 20;
            dgvCoords.ColumnHeadersHeight = 22;
            dgvCoords.Columns[0].FillWeight = 50;
            dgvCoords.Columns[1].FillWeight = 80;
            dgvCoords.Columns[2].Visible = false;  // 隐藏 Y, 节省空间
            dgvCoords.Columns[3].Visible = false;  // 隐藏 Z, 节省空间
            dgvCoords.Columns[4].FillWeight = 200;
            tabCoord.Controls.Add(dgvCoords);
            int listRightX = 12 + 360 + 8;
            int listBtnW = 110, listBtnH = 28;
            // 实时坐标显示 (放选择文件按钮上方, 只显示 xyz 数值)
            lblLiveCoord = new Label { Text = "X=--  Y=--  Z=--", Location = new Point(listRightX, yCoord[0]), Size = new Size(listBtnW, 20), ForeColor = Color.Blue, Tag = "mem" };
            tabCoord.Controls.Add(lblLiveCoord);
            int dgvTopY = yCoord[0] + 24;
            yCoord[0] = dgvTopY + 330 + 8;

            int btnH = 26;
            Button btnSelectCoordFile = new Button { Text = "选择文件", Location = new Point(listRightX, dgvTopY), Size = new Size(listBtnW, listBtnH), Tag = "mem" };
            btnSelectCoordFile.Click += (s, ev) => SelectCoordFile();
            tabCoord.Controls.Add(btnSelectCoordFile);

            int opGap = 6;
            Button btnReadPos = new Button { Text = "读取当前", Location = new Point(listRightX, dgvTopY + (listBtnH + 4) * 2), Size = new Size(listBtnW, listBtnH), Tag = "mem" };
            btnReadPos.Click += (s, ev) => ReadAndFillPosition();
            tabCoord.Controls.Add(btnReadPos);

            int inputY = yCoord[0];
            int inputH = 22;
            tabCoord.Controls.Add(new Label { Text = "备注:", Location = new Point(12, inputY + 3), Size = new Size(36, 20), Tag = "mem" });
            txtCoordRemark = new TextBox { Location = new Point(52, inputY + 1), Size = new Size(120, inputH), Text = "示例", Tag = "mem" };
            tabCoord.Controls.Add(txtCoordRemark);

            tabCoord.Controls.Add(new Label { Text = "文件名:", Location = new Point(180, inputY + 3), Size = new Size(52, 20), Tag = "mem" });
            txtCoordFileName = new TextBox { Location = new Point(236, inputY + 1), Size = new Size(140, inputH), Text = "示例", Tag = "mem" };
            tabCoord.Controls.Add(txtCoordFileName);

            Button btnSaveToDesktop = new Button { Text = "保存到桌面", Location = new Point(382, inputY - 1), Size = new Size(100, inputH + 4), Tag = "mem" };
            btnSaveToDesktop.Click += (s, ev) => SaveCurrentCoordToDesktop();
            tabCoord.Controls.Add(btnSaveToDesktop);

            inputY += inputH + 6;

            // 左按钮: 传送到上一条坐标 (AAA 辅助风格)
            Button btnTeleportPrev = new Button { Text = "← 传送上一条", Location = new Point(12, inputY), Size = new Size(100, btnH), Tag = "mem" };
            btnTeleportPrev.Click += (s, ev) => TeleportAdjacent(-1);
            tabCoord.Controls.Add(btnTeleportPrev);

            Button btnTeleportSelected = new Button { Text = "传送到选中", Location = new Point(12 + 100 + opGap, inputY), Size = new Size(100, btnH), Tag = "mem" };
            btnTeleportSelected.Click += (s, ev) => TeleportToSelectedCoord();
            tabCoord.Controls.Add(btnTeleportSelected);

            // 右按钮: 传送到下一条坐标 (AAA 辅助风格)
            Button btnTeleportNext = new Button { Text = "传送下一条 →", Location = new Point(12 + (100 + opGap) * 2, inputY), Size = new Size(100, btnH), Tag = "mem" };
            btnTeleportNext.Click += (s, ev) => TeleportAdjacent(1);
            tabCoord.Controls.Add(btnTeleportNext);

            inputY += btnH + 4;
            // 微调传送模式 (对齐 AAA.exe 的 Alt+方向键/空格/C 功能)
            // 两个勾选框 + ? 提示图标, 勾选后通过全局键盘钩子触发微调传送
            var nudgeToolTip = new ToolTip { InitialDelay = 100, ReshowDelay = 100, AutoPopDelay = 10000 };
            CheckBox chkFlyMode = new CheckBox { Text = "飞天遁地", Location = new Point(12, inputY + 2), Size = new Size(80, btnH), Tag = "mem" };
            tabCoord.Controls.Add(chkFlyMode);
            Label lblFlyHelp = new Label { Text = "?", Location = new Point(12 + 80 + 2, inputY + 4), Size = new Size(14, 16), ForeColor = Color.Blue, Cursor = Cursors.Help, Tag = "mem" };
            tabCoord.Controls.Add(lblFlyHelp);
            nudgeToolTip.SetToolTip(lblFlyHelp, "勾选后:\n  方向键 ↑ = 向上瞬移 (Y+2)\n  方向键 ↓ = 向下瞬移 (Y-6)\n游戏中按方向键上下即可, 取消勾选关闭");

            CheckBox chkNudgeMode = new CheckBox { Text = "瞬移", Location = new Point(180, inputY + 2), Size = new Size(60, btnH), Tag = "mem" };
            tabCoord.Controls.Add(chkNudgeMode);
            Label lblNudgeHelp = new Label { Text = "?", Location = new Point(180 + 60 + 2, inputY + 4), Size = new Size(14, 16), ForeColor = Color.Blue, Cursor = Cursors.Help, Tag = "mem" };
            tabCoord.Controls.Add(lblNudgeHelp);
            nudgeToolTip.SetToolTip(lblNudgeHelp, "勾选后按住 Alt + 方向键:\n  Alt+↑ = 向北 (Z-3)\n  Alt+↓ = 向南 (Z+3)\n  Alt+← = 向西 (X-3)\n  Alt+→ = 向东 (X+3)\n取消勾选关闭");

            chkFlyMode.CheckedChanged += (s, ev) =>
            {
                flyModeEnabled = chkFlyMode.Checked;
                UpdateNudgeHookState();
                AppendLog("[微调] 飞天遁地模式: " + (flyModeEnabled ? "开启" : "关闭"));
            };
            chkNudgeMode.CheckedChanged += (s, ev) =>
            {
                nudgeModeEnabled = chkNudgeMode.Checked;
                UpdateNudgeHookState();
                AppendLog("[微调] 瞬移模式: " + (nudgeModeEnabled ? "开启" : "关闭"));
            };

            yCoord[0] = inputY + btnH + 8;

            txtCoordInput = new TextBox { Text = "" };

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
            injectedPID = targetPid;
            AppendLog("目标进程 PID: " + targetPid);
            // 自动从运行中的进程检测游戏路径
            if (string.IsNullOrEmpty(gameRootPath))
            {
                try
                {
                    string exePath = procs[0].MainModule.FileName;
                    string binDir = Path.GetDirectoryName(exePath);
                    string rootDir = Path.GetDirectoryName(binDir);
                    if (File.Exists(Path.Combine(binDir, GameExeName)))
                    {
                        gameRootPath = rootDir;
                        gameBinPath = binDir;
                        UpdateCommPaths();
                        AppendLog("自动检测游戏路径: " + gameRootPath);
                    }
                }
                catch (Exception ex) { AppendLog("自动检测路径失败: " + ex.Message); }
            }
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
                        if (IsTerminalResultFile(CmdResultFile) || File.Exists(ToolResultFile) || ticks >= maxPollTicks)
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
            string resultPath = File.Exists(ToolResultFile) ? ToolResultFile : "";
            if (string.IsNullOrEmpty(resultPath)) return;

            var lines = File.ReadAllLines(resultPath);
            if (lines.Length == 0) return;

            if (pendingReadPosition)
            {
                pendingReadPosition = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("POS\t"))
                    {
                        string posStr = line.Substring(4).Trim();
                        string[] parts = posStr.Split(',');
                        if (parts.Length >= 3)
                        {
                            double px, py, pz;
                            if (double.TryParse(parts[0].Trim(), out px) &&
                                double.TryParse(parts[1].Trim(), out py) &&
                                double.TryParse(parts[2].Trim(), out pz))
                            {
                                lastReadX = px; lastReadY = py; lastReadZ = pz;
                                AppendLog("当前坐标: X=" + px.ToString("F1") + " Y=" + py.ToString("F1") + " Z=" + pz.ToString("F1"));
                            }
                        }
                        break;
                    }
                }
            }

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

-- 先读取当前位置用于对比
local function getpos()
  local p = mp.get_position and {pcall(function() return mp:get_position() end)}
  if p and p[1] and p[2] then return p[2] end
  if mp.position then return mp.position end
  return nil
end
local before = getpos()
if before then
  __add('before', string.format('%.1f,%.1f,%.1f', before.x or 0, before.y or 0, before.z or 0))
end

-- 尝试停止当前移动/导航
for _, stopname in ipairs({'stop_movement', 'stop', 'stop_nav', 'cancel_move', 'halt', 'idle'}) do
  if mp[stopname] then pcall(function() mp[stopname](mp) end) end
end

-- 尝试找到movement组件
local mover = nil
if mp.__components__ then
  for _, comp in pairs(mp.__components__) do
    if type(comp) == 'table' or type(comp) == 'userdata' then
      for _, name in ipairs({'movement', 'navigator', 'motor', 'character_movement', 'locomotion'}) do
        if comp[name] or comp['get_' .. name] then mover = comp; break end
      end
      if mover then break end
    end
  end
end

local succeeded = false
local function try_set(name, fn)
  if succeeded then return end
  local ok, err = pcall(fn)
  if ok then
    -- 等一小段时间后读回位置验证
    for i=1,20 do end -- busy wait tiny bit
    local after = getpos()
    if after and before then
      local dx = (after.x or 0) - (before.x or 0)
      local dy = (after.y or 0) - (before.y or 0)
      local dz = (after.z or 0) - (before.z or 0)
      local dist = math.sqrt(dx*dx + dy*dy + dz*dz)
      __add('check', name .. ' after=(' .. string.format('%.1f,%.1f,%.1f', after.x or 0, after.y or 0, after.z or 0) .. ') dist=' .. string.format('%.1f', dist))
      if dist > 1 then
        __add('传送', name .. ' OK -> ' .. string.format('%.1f,%.1f,%.1f', tx, ty, tz)); succeeded = true; return
      end
    else
      __add('传送', name .. ' called (cannot verify) -> ' .. string.format('%.1f,%.1f,%.1f', tx, ty, tz)); succeeded = true; return
    end
  end
end

-- 尝试XZY顺序 (UE4常见: X=Forward, Z=Right, Y=Up)
try_set('set_position(xzy)', function() mp:set_position(tx, tz, ty) end)
try_set('set_position(xyz)', function() mp:set_position(tx, ty, tz) end)

-- 更多API名，两种顺序
for _, name in ipairs({'tp_to', 'warp_to', 'teleport_to', 'move_to', 'goto_pos', 'set_pos', 'transfer_to', 'jump_to'}) do
  if mp[name] then
    try_set(name .. '(xyz)', function() mp[name](mp, tx, ty, tz) end)
    try_set(name .. '(xzy)', function() mp[name](mp, tx, tz, ty) end)
  end
end

-- 尝试mover组件方法
if mover then
  for _, name in ipairs({'set_position', 'set_world_position', 'teleport', 'move_to'}) do
    if mover[name] then
      try_set('mover.' .. name .. '(xyz)', function() mover[name](mover, tx, ty, tz) end)
      try_set('mover.' .. name .. '(xzy)', function() mover[name](mover, tx, tz, ty) end)
    end
  end
end

-- 尝试通过G的GM命令传送
__try('gm_tp', function()
  if type(G.gm) == 'table' or type(G.gm) == 'userdata' then
    if G.gm.tp then G.gm:tp(tx, ty, tz); __add('传送', 'G.gm:tp'); succeeded = true end
    if G.gm.teleport then G.gm:teleport(tx, ty, tz); __add('传送', 'G.gm:teleport'); succeeded = true end
  end
  if G.tp then G:tp(tx, ty, tz); __add('传送', 'G:tp'); succeeded = true end
  if G.teleport then G:teleport(tx, ty, tz); __add('传送', 'G:teleport'); succeeded = true end
end)

if not succeeded then
  -- 最终尝试: 直接写position字段+停止移动
  pcall(function()
    if mp.stop_movement then mp:stop_movement() end
    if mp.position then
      local pos = mp.position
      if type(pos) == 'table' then
        pos.x = tx; pos.y = ty; pos.z = tz
      elseif type(pos) == 'userdata' then
        if pos.set_x then pos:set_x(tx) end
        if pos.set_y then pos:set_y(ty) end
        if pos.set_z then pos:set_z(tz) end
      end
      __add('传送', 'direct position write -> ' .. string.format('%.1f,%.1f,%.1f', tx, ty, tz))
      succeeded = true
    end
  end)
end

-- 传送后再停止一次移动
for _, stopname in ipairs({'stop_movement', 'stop', 'idle'}) do
  if mp[stopname] then pcall(function() mp[stopname](mp) end) end
end

local after = getpos()
if after then
  __add('after', string.format('%.1f,%.1f,%.1f', after.x or 0, after.y or 0, after.z or 0))
end
if not succeeded then __add('传送', '失败: 所有API尝试完毕，位置未变化') end
");
        }

        string BuildReadPositionLua()
        {
            return BuildLuaEnvelope("read_pos", @"
__try('read_position', function()
  local mp = type(G) == 'table' and G.main_player or nil
  if not mp then error('main_player missing') end
  local pos = nil
  local posSrc = ''
  if mp.get_position then
    local ok, p = pcall(function() return mp:get_position() end)
    if ok and p then pos = p; posSrc = 'get_position()' end
  end
  if not pos and mp.position then
    pos = mp.position; posSrc = '.position'
  end
  if not pos and mp.pos then
    pos = mp.pos; posSrc = '.pos'
  end
  if not pos and mp.coord then
    pos = mp.coord; posSrc = '.coord'
  end
  if pos then
    local x = pos.x or pos.X or 0
    local y = pos.y or pos.Y or 0
    local z = pos.z or pos.Z or 0
    __add('POS', string.format('%.1f,%.1f,%.1f', x, y, z))
    __add('info', 'pos from ' .. posSrc)
  else
    __add('POS', '0,0,0')
    __add('error', '无法获取位置, 列出mp字段')
    local fields = {}
    for k, v in pairs(mp) do
      if type(v) ~= 'function' then fields[#fields+1] = k .. ':' .. type(v) end
    end
    __add('fields', table.concat(fields, ', '))
  end
end)
");
        }

        string BuildPressKeyLua(string key)
        {
            return BuildLuaEnvelope("press_key", @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then __add('error', 'main_player missing'); return end
local apis = {
  {'press_key', function() if mp.press_key then mp:press_key('" + EscapeLuaString(key) + @"') end end},
  {'key_press', function() if mp.key_press then mp:key_press('" + EscapeLuaString(key) + @"') end end},
  {'simulate_key', function() if mp.simulate_key then mp:simulate_key('" + EscapeLuaString(key) + @"') end end},
}
for _, api in ipairs(apis) do
  local name, fn = api[1], api[2]
  local ok, err = pcall(fn)
  if ok then __add('按键', name .. ' -> " + EscapeLuaString(key) + @"'); return end
end
__add('按键', '无可用按键API')
");
        }

        string BuildJackBuffLua(bool enable)
        {
            return BuildLuaEnvelope("jack_buff", @"
local mp = type(G) == 'table' and G.main_player or nil
if not mp then __add('error', 'main_player missing'); return end
local ok = false
if " + (enable ? "true" : "false") + @" then
  local buffIds = {720001, 720002, 720003, 110101, 110102}
  for _, id in ipairs(buffIds) do
    local r, e = pcall(function() if mp.add_buff then mp:add_buff(id) end end)
    if r then ok = true end
  end
else
  local buffIds = {720001, 720002, 720003, 110101, 110102}
  for _, id in ipairs(buffIds) do
    pcall(function()
      if mp.buff_remove_by_No then mp:buff_remove_by_No(id)
      elseif mp.remove_buff then mp:remove_buff(id) end
    end)
  end
  ok = true
end
__add('千斤顶', " + (enable ? "'已开启'" : "'已关闭'") + @")
");
        }

        // ===== Coordinate Management Methods =====

        string coordFilePath = "";
        double lastReadX, lastReadY, lastReadZ;
        bool pendingReadPosition = false;

        void SelectCoordFile()
        {
            var dlg = new OpenFileDialog { Filter = "坐标文件|*.txt;*.ini|文本文件|*.txt|INI文件|*.ini|所有文件|*.*", Title = "选择坐标文件" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                coordFilePath = dlg.FileName;
                LoadCoordsFromFile(coordFilePath);
            }
        }

        void OpenCoordFile()
        {
            if (string.IsNullOrEmpty(coordFilePath)) { MessageBox.Show("请先选择坐标文本文件"); return; }
            if (!File.Exists(coordFilePath)) { MessageBox.Show("文件不存在: " + coordFilePath); return; }
            try { System.Diagnostics.Process.Start("notepad.exe", coordFilePath); }
            catch (Exception ex) { MessageBox.Show("打开失败: " + ex.Message); }
        }

        // 把当前实时坐标 (lastReadX/Y/Z) 添加到坐标列表
        void SetCustomCurrent()
        {
            if (lastReadX == 0 && lastReadY == 0 && lastReadZ == 0)
            {
                MessageBox.Show("当前坐标无效, 请先初始化坐标");
                return;
            }
            string remark = txtCoordRemark.Text.Trim();
            int idx = dgvCoords.Rows.Count + 1;
            dgvCoords.Rows.Add(idx, lastReadX.ToString("F1"), lastReadY.ToString("F1"), lastReadZ.ToString("F1"), remark);
            AppendLog(string.Format("[坐标] 已添加: X={0:F1} Y={1:F1} Z={2:F1} {3}", lastReadX, lastReadY, lastReadZ, remark));
        }

        void LoadCoordsFromFile(string path)
        {
            try
            {
                // 自动编码检测: UTF8 优先(带 BOM 或可成功解码), 失败回退 ANSI(GBK/Default)
                // 解决记事本默认保存为 ANSI(GBK) 的中文坐标文件乱码问题
                string[] lines;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    // UTF-8 BOM
                    lines = File.ReadAllLines(path, Encoding.UTF8);
                }
                else
                {
                    // 尝试 UTF8 严格解码, 失败则用 ANSI(GBK)
                    try
                    {
                        string test = Encoding.UTF8.GetString(bytes);
                        // UTF8 解码后若含替换字符, 视为非 UTF8 文件
                        if (test.IndexOf('\uFFFD') >= 0)
                            lines = File.ReadAllLines(path, Encoding.Default);
                        else
                            lines = File.ReadAllLines(path, Encoding.UTF8);
                    }
                    catch
                    {
                        lines = File.ReadAllLines(path, Encoding.Default);
                    }
                }

                dgvCoords.Rows.Clear();
                int idx = 1;
                bool isIni = path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (trimmed.StartsWith("#")) continue;
                    // ini 的节头 [xxx] 跳过
                    if (isIni && trimmed.StartsWith("[")) continue;
                    // ini 的注释 ';' 跳过; txt 里 ';' 也当注释(保持向后兼容)
                    if (trimmed.StartsWith(";")) continue;

                    // ini 格式: key=value, value 可能是 "X,Y,Z" 或 "名称,X,Y,Z"
                    string content = trimmed;
                    if (isIni && content.Contains("="))
                    {
                        int eq = content.IndexOf('=');
                        content = content.Substring(eq + 1).Trim();
                        if (string.IsNullOrEmpty(content)) continue;
                    }

                    char[] seps = new char[] { ',', ' ', '\t' };
                    string[] parts = content.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        double cx, cy, cz;
                        string remark = "";
                        int off = 0;
                        if (parts.Length >= 4 && !double.TryParse(parts[0], out cx))
                        {
                            remark = parts[0]; off = 1;
                        }
                        if (double.TryParse(parts[off], out cx) &&
                            double.TryParse(parts[off + 1], out cy) &&
                            double.TryParse(parts[off + 2], out cz))
                        {
                            if (off == 0 && parts.Length > 3) remark = string.Join(" ", parts, 3, parts.Length - 3);
                            // ini 模式下若无备注, 用 key 名作为备注
                            if (isIni && string.IsNullOrEmpty(remark) && trimmed.Contains("="))
                            {
                                int eq = trimmed.IndexOf('=');
                                string key = trimmed.Substring(0, eq).Trim();
                                if (!string.IsNullOrEmpty(key)) remark = key;
                            }
                            dgvCoords.Rows.Add(idx++, cx.ToString("F1"), cy.ToString("F1"), cz.ToString("F1"), remark);
                        }
                    }
                }
                AppendLog("已加载坐标文件: " + path + " (" + (idx - 1) + "个坐标)");
            }
            catch (Exception ex) { AppendLog("加载坐标文件失败: " + ex.Message); }
        }

        void AddCoordToList()
        {
            // 复用 SetCustomCurrent (添加当前实时坐标到列表)
            SetCustomCurrent();
        }

        void TeleportToSelectedCoord()
        {
            if (dgvCoords.SelectedRows.Count == 0) { MessageBox.Show("请先选择一个坐标"); return; }
            var row = dgvCoords.SelectedRows[0];
            if (row.Cells[1].Value == null) return;
            double tx, ty, tz;
            string sx = row.Cells[1].Value != null ? row.Cells[1].Value.ToString() : "";
            string sy = row.Cells[2].Value != null ? row.Cells[2].Value.ToString() : "";
            string sz = row.Cells[3].Value != null ? row.Cells[3].Value.ToString() : "";
            if (!double.TryParse(sx, out tx) || !double.TryParse(sy, out ty) || !double.TryParse(sz, out tz))
            { MessageBox.Show("坐标数据无效"); return; }
            if (WriteMemCoord(tx, ty, tz))
            {
                string remark = row.Cells[4].Value != null ? row.Cells[4].Value.ToString() : "";
                AppendLog("传送成功 -> " + (string.IsNullOrEmpty(remark) ? "" : remark + " ") + string.Format("X={0:F1} Y={1:F1} Z={2:F1}", tx, ty, tz));
            }
            else
            {
                AppendLog("传送失败");
            }
        }

        // 左右按钮: 传送到上一条/下一条坐标 (循环切换)
        // direction: -1=上一条, +1=下一条
        void TeleportAdjacent(int direction)
        {
            int count = dgvCoords.Rows.Count;
            if (count == 0) { MessageBox.Show("坐标列表为空"); return; }
            int curIdx = -1;
            if (dgvCoords.SelectedRows.Count > 0)
                curIdx = dgvCoords.SelectedRows[0].Index;
            // 计算目标行索引(循环)
            int nextIdx;
            if (curIdx < 0)
                nextIdx = direction > 0 ? 0 : count - 1;
            else
                nextIdx = (curIdx + direction + count) % count;
            // 切换选中并传送
            dgvCoords.ClearSelection();
            dgvCoords.Rows[nextIdx].Selected = true;
            dgvCoords.FirstDisplayedScrollingRowIndex = nextIdx;
            TeleportToSelectedCoord();
        }

        void SaveCurrentCoordToDesktop()
        {
            string fileName = txtCoordFileName.Text.Trim();
            if (string.IsNullOrEmpty(fileName)) fileName = "coords";
            if (!fileName.EndsWith(".txt")) fileName += ".txt";
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktop, fileName);
            try
            {
                using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                {
                    sw.WriteLine("# 坐标文件 格式: 名称,X,Y,Z 或 X,Y,Z");
                    foreach (DataGridViewRow row in dgvCoords.Rows)
                    {
                        string remark = row.Cells[4].Value != null ? row.Cells[4].Value.ToString() : "";
                        string line = string.Format("{0},{1},{2},{3}",
                            string.IsNullOrEmpty(remark) ? ("坐标" + row.Cells[0].Value) : remark,
                            row.Cells[1].Value, row.Cells[2].Value, row.Cells[3].Value);
                        sw.WriteLine(line);
                    }
                }
                AppendLog("坐标已保存到: " + path);
                coordFilePath = path;
            }
            catch (Exception ex) { MessageBox.Show("保存失败: " + ex.Message); }
        }

        void SendCommandNoLog(string luaCode)
        {
            if (!isReady) return;
            try { SendCommand(luaCode); } catch { }
        }

        void ReadAndFillPosition()
        {
            double px, py, pz;
            bool needDebug = (lastReadX == 0 && lastReadY == 0 && lastReadZ == 0);
            if (!ReadMemCoord(out px, out py, out pz, needDebug))
            {
                // 第一次失败用诊断模式重试
                if (!needDebug && !ReadMemCoord(out px, out py, out pz, true))
                {
                    MessageBox.Show("读取坐标失败，请确认:\n1. 已点击'初始化坐标'\n2. 以管理员身份运行\n3. 游戏已进入场景");
                    return;
                }
            }
            if (px == 0 && py == 0 && pz == 0)
            {
                AppendLog("警告: 读取到的坐标全为0，可能未进入游戏场景或偏移已过期");
            }
            lastReadX = px; lastReadY = py; lastReadZ = pz;
            AppendLog("当前坐标: X=" + px.ToString("F1") + " Y(高)=" + py.ToString("F1") + " Z=" + pz.ToString("F1"));
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

        double ReadDouble(IntPtr hProcess, long address)
        {
            byte[] buffer = new byte[8];
            if (!ReadMemory(hProcess, address, buffer)) return double.NaN;
            return BitConverter.ToDouble(buffer, 0);
        }

        bool WriteDouble(IntPtr hProcess, long address, double value)
        {
            return WriteMemory(hProcess, address, BitConverter.GetBytes(value));
        }

        // ===== 内存坐标读写 (代码洞Hook + Double) =====
        // 原理: 通过AOB找到 vmovsd [rcx+0x340],xmm0 写入指令，hook后把rcx写入固定内存
        // C#读取该固定内存获取当前玩家坐标对象基址，再+0x340/0x348/0x350读写Double坐标
        // 这是AAA.exe的"自动适配"原理——不需要静态指针链，每次启动hook自动捕获
        // 坐标为 Double(8字节)，CT表注释确认是Double类型
        // AOB特征码: C5 F8 11 81 40 03 00 00 (vmovsd [rcx+0x340], xmm0)
        const string COORD_AOB = "C5 F8 11 81 40 03 00 00";
        const int COORD_AOB_LEN = 8;
        // 偏移顺序从 AAA.exe 实际写入捕获确认: X=0x340, Z=0x348, Y=0x350
        const long COORD_OFFSET_X = 0x340;
        const long COORD_OFFSET_Z = 0x348;  // Z(高度,小值约-47)
        const long COORD_OFFSET_Y = 0x350;  // Y(纵向,大值约-1900)
        // 坐标合理性上限(优化项8): 超过此值视为非法坐标, 用于指针链/AOB 验证
        const double COORD_SANITY_MAX = 100000.0;

        // ===== 静态指针链方案 (从 AAA.exe 提取, 2026-06-29) =====
        // 指针链: yysls.exe + STATIC_OFFSET → P1 → P1+0x58 → P2 → P2+0x00 → OBJ(玩家)
        // OBJ+0x340=X(Double), OBJ+0x348=Z(Double), OBJ+0x350=Y(Double)
        // 优点: 直接获取玩家对象(非相机), 无需Frida注入, 无需Hook触发
        // AOB扫描确认 yysls 代码段有17处 RIP-relative 引用此偏移, 游戏更新后可自动定位
        const long STATIC_OFFSET_DEFAULT = 0x083F46D8;  // 从 AAA.exe 抓取的硬编码偏移
        const long PTR_STEP1_OFFSET = 0x58;
        const long PTR_STEP2_OFFSET = 0x00;
        long cachedStaticOffset = 0;  // 运行时确定的静态偏移(硬编码或AOB扫描)
        bool lastResolveWasPtrChain = false;  // 上次ResolveCoordBase是否用静态指针链
        // 优化项4: OBJ 基址结果缓存(500ms TTL), 避免高频读坐标时重复解三层指针链
        // PID 校验: 游戏重启/PID 变化时自动失效(地址在新进程空间无意义)
        long cachedObjBase = 0;
        int cachedObjBasePid = 0;
        DateTime cachedObjBaseExpireAt = DateTime.MinValue;

        // 相机到玩家的偏移校正(相机坐标 + 偏移 = 玩家坐标)
        // 实测: 相机X=-2606.4, 玩家X=-2589.58 → 偏移=+16.82
        //       相机Y=-43.8, 玩家Y=-41.9 → 偏移=+1.9
        //       相机Z=-1861.7, 玩家Z=-1811.02 → 偏移=+50.68
        const double CAM_OFFSET_X = 16.82;
        const double CAM_OFFSET_Y = 1.9;
        const double CAM_OFFSET_Z = 50.68;

        // 注入时记录的目标进程ID，用于R/RH双客户端场景下精确定位
        int injectedPID = 0;
        long cachedModuleBase = 0; // 缓存游戏模块基址
        IntPtr coordHookStoreAddr = IntPtr.Zero; // 存储rcx的固定内存地址(多slot共享)
        int coordHookActiveSlot = 0; // 当前使用的slot索引

        // ===== 长驻进程句柄 (优化项1: ReadMemCoord/WriteMemCoord 复用) =====
        // 避免每次读写坐标都 OpenProcess/CloseHandle; 失效(PID变化/进程退出)时自动重开
        // 生命周期: 首次 AcquireCoordHandle 打开 → 复用 → 失效重开 / OnFormClosing 释放
        IntPtr persistentCoordHProcess = IntPtr.Zero;
        int persistentCoordPid = 0;
        List<long> coordHookInjectAddrs = new List<long>(); // 被hook的指令地址列表
        List<IntPtr> coordHookCaveAddrs = new List<IntPtr>(); // 代码洞地址列表
        List<byte[]> coordHookOrigBytesList = new List<byte[]>(); // 原始指令字节列表

        // 解析 AOB 字符串为字节数组 + 通配掩码; "??" 表示任意字节
        static bool ParseAOB(string aob, out byte[] pattern, out bool[] mask)
        {
            pattern = null; mask = null;
            if (string.IsNullOrEmpty(aob)) return false;
            string[] tokens = aob.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return false;
            var pat = new byte[tokens.Length];
            var msk = new bool[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (t == "??" || t == "?")
                {
                    pat[i] = 0; msk[i] = false;
                }
                else
                {
                    byte b;
                    if (!byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
                    pat[i] = b; msk[i] = true;
                }
            }
            pattern = pat; mask = msk;
            return true;
        }

        // 在 [moduleBase, moduleBase+moduleSize) 范围内分块扫描 AOB，返回所有命中绝对地址
        List<long> AOBScanRegions(IntPtr hProcess, long moduleBase, int moduleSize, byte[] pattern, bool[] mask)
        {
            var hits = new List<long>();
            if (pattern == null || pattern.Length == 0) return hits;
            const int chunkSize = 0x10000;
            int overlap = pattern.Length - 1;
            byte[] chunk = new byte[chunkSize + overlap];
            long end = moduleBase + moduleSize;
            for (long start = moduleBase; start < end; start += chunkSize)
            {
                int toRead = (int)Math.Min(chunkSize + overlap, end - start);
                IntPtr bytesRead;
                if (!ReadProcessMemory(hProcess, new IntPtr(start), chunk, new IntPtr(toRead), out bytesRead)) continue;
                int read = (int)bytesRead.ToInt64();
                for (int i = 0; i <= read - pattern.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (mask[j] && chunk[i + j] != pattern[j]) { match = false; break; }
                    }
                    if (match) hits.Add(start + i);
                }
            }
            return hits;
        }

        // AOB扫描第一个匹配
        long AOBScanFirst(IntPtr hProcess, long moduleBase, int moduleSize, byte[] pattern, bool[] mask)
        {
            var hits = AOBScanRegions(hProcess, moduleBase, moduleSize, pattern, mask);
            return hits.Count > 0 ? hits[0] : 0;
        }

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
        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

        // 在targetAddr附近分配可执行内存(±2GB范围内，用于JMP)
        IntPtr AllocateNearMemory(IntPtr hProcess, long targetAddr, int size)
        {
            long searchStart = targetAddr - 0x7FFF0000;
            if (searchStart < 0x10000) searchStart = 0x10000;
            long searchEnd = targetAddr + 0x7FFF0000;
            long addr = searchStart;
            MEMORY_BASIC_INFORMATION mbi;
            while (addr < searchEnd)
            {
                int result = VirtualQueryEx(hProcess, new IntPtr(addr), out mbi, new IntPtr(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))));
                if (result == 0) break;
                // MEM_FREE = 0x10000
                if (mbi.State == 0x10000 && mbi.RegionSize.ToInt64() >= size)
                {
                    long allocAddr = (mbi.BaseAddress.ToInt64() + 0xFFFF) & ~0xFFFFL;
                    IntPtr allocated = VirtualAllocEx(hProcess, new IntPtr(allocAddr), (uint)size, MEM_COMMIT | 0x2000, PAGE_EXECUTE_READWRITE);
                    if (allocated != IntPtr.Zero) return allocated;
                }
                addr = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
            }
            // 回退: 不限制位置
            return VirtualAllocEx(hProcess, IntPtr.Zero, (uint)size, MEM_COMMIT | 0x2000, PAGE_EXECUTE_READWRITE);
        }

        // 初始化坐标: AOB扫描所有vmovsd指令 + 逐个尝试hook找到热路径
        void InitCoordHook()
        {
            // 如果已经初始化过，先清理旧hook
            if (coordHookInjectAddrs.Count > 0 || coordHookStoreAddr != IntPtr.Zero)
            {
                CleanupCoordHook();
            }

            IntPtr hProcess; Process proc;
            if (!OpenGameProcessForCoord(out hProcess, out proc)) return;
            try
            {
                long moduleBase = proc.MainModule.BaseAddress.ToInt64();
                cachedModuleBase = moduleBase;
                AppendLog(string.Format("[坐标] 模块基址=0x{0:X}", moduleBase));

                // v38.1: 优先尝试静态指针链方案, 成功则跳过 Hook 后备初始化(避免 7 秒 AOB 扫描卡顿)
                long ptrChainObj;
                if (ResolveCoordBaseByPtrChain(hProcess, out ptrChainObj, true))
                {
                    lastResolveWasPtrChain = true;
                    double x = ReadDouble(hProcess, ptrChainObj + COORD_OFFSET_X);
                    double y = ReadDouble(hProcess, ptrChainObj + COORD_OFFSET_Y);
                    double z = ReadDouble(hProcess, ptrChainObj + COORD_OFFSET_Z);
                    AppendLog(string.Format("[坐标] 静态指针链初始化成功: OBJ=0x{0:X}", ptrChainObj));
                    AppendLog(string.Format("[坐标] X={0:F1} Y(高)={1:F1} Z={2:F1}", x, y, z));
                    AppendLog("[坐标] 使用静态指针链方案, 跳过Hook初始化(无需AOB扫描)");
                    AppendLog("[坐标] 游戏运行期间自动跟踪玩家坐标对象");
                    return;
                }
                AppendLog("[坐标] 静态指针链失败, 回退到Hook后备方案...");

                // AOB扫描所有 vmovsd [rcx+0x340], xmm0 指令
                byte[] pattern; bool[] mask;
                ParseAOB(COORD_AOB, out pattern, out mask);
                AppendLog("[坐标] 正在扫描AOB: " + COORD_AOB);
                List<long> hits = AOBScanRegions(hProcess, moduleBase, proc.MainModule.ModuleMemorySize, pattern, mask);
                if (hits.Count == 0)
                {
                    AppendLog("[坐标] 未找到AOB! 游戏可能已更新或已被hook");
                    AppendLog("[坐标] 若游戏未重启，请先重启游戏清除旧hook");
                    return;
                }
                AppendLog(string.Format("[坐标] 找到 {0} 个匹配指令:", hits.Count));
                for (int i = 0; i < hits.Count; i++)
                {
                    AppendLog(string.Format("  [{0}] 0x{1:X} (base+0x{2:X})", i, hits[i], hits[i] - moduleBase));
                }

                // 分配共享的存储rcx内存
                // 改为多slot: 每个hook独占一个slot(16字节)，避免覆盖
                // slot布局: [0]=hook0_rcx, [8]=hook1_rcx, [16]=hook2_rcx...
                int slotCount = hits.Count;
                coordHookStoreAddr = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)(slotCount * 16), MEM_COMMIT, PAGE_READWRITE);
                if (coordHookStoreAddr == IntPtr.Zero)
                {
                    AppendLog("[坐标] 分配存储内存失败");
                    return;
                }
                WriteMemory(hProcess, coordHookStoreAddr.ToInt64(), new byte[slotCount * 16]);

                // 同时hook所有指令，每个写入独立slot
                for (int i = 0; i < hits.Count; i++)
                {
                    long injectAddr = hits[i];
                    AppendLog(string.Format("[坐标] 安装hook [{0}] 0x{1:X}...", i, injectAddr));
                    if (TryInstallHook(hProcess, injectAddr, i))
                    {
                        coordHookActiveSlot = i; // 默认用第一个
                    }
                }

                // 等待500ms让所有hook触发
                System.Threading.Thread.Sleep(500);

                // 显示每个hook捕获的坐标
                AppendLog(string.Format("[坐标] 各hook捕获结果:"));
                int bestSlot = -1;
                double bestDiff = double.MaxValue;
                for (int i = 0; i < slotCount; i++)
                {
                    long slotAddr = coordHookStoreAddr.ToInt64() + i * 16;
                    long capturedRcx = ReadInt64(hProcess, slotAddr);
                    if (capturedRcx != 0)
                    {
                        double x = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_X);
                        double y = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Y);
                        double z = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Z);
                        AppendLog(string.Format("  [slot {0}] rcx=0x{1:X} X={2:F1} Y(高)={3:F1} Z={4:F1}",
                            i, capturedRcx, x, y, z));
                        if (bestSlot < 0) { bestSlot = i; bestDiff = 0; }
                    }
                    else
                    {
                        AppendLog(string.Format("  [slot {0}] 未触发", i));
                    }
                }

                if (bestSlot >= 0)
                {
                    coordHookActiveSlot = bestSlot;
                    long capturedRcx = ReadInt64(hProcess, coordHookStoreAddr.ToInt64() + bestSlot * 16);
                    double x = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_X);
                    double y = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Y);
                    double z = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Z);
                    AppendLog(string.Format("[坐标] 初始化成功! 使用slot {0}, rcx=0x{1:X}", bestSlot, capturedRcx));
                    AppendLog(string.Format("[坐标] X={0:F1} Y(高)={1:F1} Z={2:F1}", x, y, z));
                    AppendLog("[坐标] 如坐标不对，可在'验证'中切换slot");
                    AppendLog("[坐标] Hook已安装，游戏运行期间自动跟踪玩家坐标对象");
                }
                else
                {
                    AppendLog("[坐标] 所有指令均未触发，请在游戏中移动后再次点击'初始化坐标'");
                }
            }
            catch (Exception ex) { AppendLog("[坐标] 初始化异常: " + ex.Message); }
            finally { CloseHandle(hProcess); SafeDisposeProcess(proc); }
        }

        // 尝试在指定地址安装代码洞hook，slotIndex指定rcx写入的slot
        bool TryInstallHook(IntPtr hProcess, long injectAddr, int slotIndex)
        {
            // 读取原始指令字节
            byte[] origBytes = new byte[COORD_AOB_LEN];
            if (!ReadMemory(hProcess, injectAddr, origBytes))
            {
                AppendLog("[坐标] 读取原始指令失败");
                return false;
            }

            // 分配代码洞
            IntPtr caveAddr = AllocateNearMemory(hProcess, injectAddr, 64);
            if (caveAddr == IntPtr.Zero)
            {
                AppendLog("[坐标] 分配代码洞失败");
                return false;
            }

            // 构建代码洞:
            // 1. mov r11, storeAddr+slotIndex*16; mov [r11], reg (保存寄存器到指定slot)
            // 2. 原始指令 (movss [reg+0x340], xmm0)
            // 3. jmp 返回地址
            // 解析ModRM字节(origBytes[3])获取寄存器: rm = modrm & 0x07
            byte modrm = origBytes[3];
            int rm = modrm & 0x07;
            // mov [r11], reg 的字节: 49 89 <modrm_for_mov>
            // mod=00, reg=目标寄存器, rm=011(r11因为REX.B)
            // modrm_for_mov = 0x00 | (reg << 3) | 0x03
            // reg编码: rax=0,rcx=1,rdx=2,rbx=3,rsp=4,rbp=5,rsi=6,rdi=7
            byte[] saveRegByte = new byte[] {
                0x03, // rax: 49 89 03
                0x0B, // rcx: 49 89 0B
                0x13, // rdx: 49 89 13
                0x1B, // rbx: 49 89 1B
                0x23, // rsp: 49 89 23
                0x2B, // rbp: 49 89 2B
                0x33, // rsi: 49 89 33
                0x3B, // rdi: 49 89 3B
            };
            string[] regNames = { "rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi" };
            byte saveByte = (rm < 8) ? saveRegByte[rm] : (byte)0x0B;
            AppendLog(string.Format("[坐标]   ModRM=0x{0:X2} 寄存器={1}", modrm, (rm < 8) ? regNames[rm] : "??"));

            long storeAddr = coordHookStoreAddr.ToInt64() + slotIndex * 16;
            long caveAddrLong = caveAddr.ToInt64();
            long returnAddr = injectAddr + COORD_AOB_LEN;
            List<byte> cave = new List<byte>();
            // mov r11, storeAddr (49 BB <8字节>)
            cave.Add(0x49); cave.Add(0xBB);
            cave.AddRange(BitConverter.GetBytes(storeAddr));
            // mov [r11], reg (49 89 <saveByte>)
            cave.Add(0x49); cave.Add(0x89); cave.Add(saveByte);
            // 原始指令
            cave.AddRange(origBytes);
            // jmp returnAddr
            long jmpOffset = returnAddr - (caveAddrLong + cave.Count + 5);
            cave.Add(0xE9);
            cave.AddRange(BitConverter.GetBytes((int)jmpOffset));

            if (!WriteMemory(hProcess, caveAddrLong, cave.ToArray()))
            {
                AppendLog("[坐标] 写入代码洞失败");
                VirtualFreeEx(hProcess, caveAddr, 0, MEM_RELEASE);
                return false;
            }

            // 修改原始指令为JMP到代码洞
            uint oldProtect;
            VirtualProtectEx(hProcess, new IntPtr(injectAddr), (uint)COORD_AOB_LEN, PAGE_EXECUTE_READWRITE, out oldProtect);
            long jmpToCave = caveAddrLong - (injectAddr + 5);
            byte[] jmpBytes = new byte[COORD_AOB_LEN];
            jmpBytes[0] = 0xE9;
            Array.Copy(BitConverter.GetBytes((int)jmpToCave), 0, jmpBytes, 1, 4);
            for (int i = 5; i < COORD_AOB_LEN; i++) jmpBytes[i] = 0x90;
            bool ok = WriteMemory(hProcess, injectAddr, jmpBytes);
            VirtualProtectEx(hProcess, new IntPtr(injectAddr), (uint)COORD_AOB_LEN, oldProtect, out oldProtect);

            if (!ok)
            {
                AppendLog("[坐标] 写入JMP失败");
                VirtualFreeEx(hProcess, caveAddr, 0, MEM_RELEASE);
                return false;
            }

            // 记录hook信息
            coordHookInjectAddrs.Add(injectAddr);
            coordHookCaveAddrs.Add(caveAddr);
            coordHookOrigBytesList.Add(origBytes);
            return true;
        }

        // 清理坐标hook: 恢复原始指令 + 释放内存
        void CleanupCoordHook()
        {
            IntPtr hProcess; Process proc;
            if (!OpenGameProcessForCoord(out hProcess, out proc)) return;
            try { CleanupCoordHookCore(hProcess); }
            finally { CloseHandle(hProcess); SafeDisposeProcess(proc); }
        }

        void CleanupCoordHook(IntPtr hProcess)
        {
            CleanupCoordHookCore(hProcess);
        }

        void CleanupCoordHookCore(IntPtr hProcess)
        {
            // 恢复所有原始指令
            for (int i = 0; i < coordHookInjectAddrs.Count && i < coordHookOrigBytesList.Count; i++)
            {
                try
                {
                    uint oldProtect;
                    long addr = coordHookInjectAddrs[i];
                    byte[] bytes = coordHookOrigBytesList[i];
                    VirtualProtectEx(hProcess, new IntPtr(addr), (uint)bytes.Length, PAGE_EXECUTE_READWRITE, out oldProtect);
                    WriteMemory(hProcess, addr, bytes);
                    VirtualProtectEx(hProcess, new IntPtr(addr), (uint)bytes.Length, oldProtect, out oldProtect);
                }
                catch { }
            }
            coordHookInjectAddrs.Clear();
            coordHookOrigBytesList.Clear();

            // 释放所有代码洞
            for (int i = 0; i < coordHookCaveAddrs.Count; i++)
            {
                try { VirtualFreeEx(hProcess, coordHookCaveAddrs[i], 0, MEM_RELEASE); } catch { }
            }
            coordHookCaveAddrs.Clear();

            // 释放存储内存
            if (coordHookStoreAddr != IntPtr.Zero)
            {
                try { VirtualFreeEx(hProcess, coordHookStoreAddr, 0, MEM_RELEASE); } catch { }
                coordHookStoreAddr = IntPtr.Zero;
            }
        }

        // 验证: 输出hook状态和当前坐标
        void VerifyCoordAOB()
        {
            IntPtr hProcess; Process proc;
            if (!OpenGameProcessForCoord(out hProcess, out proc)) return;
            try
            {
                long moduleBase = proc.MainModule.BaseAddress.ToInt64();
                cachedModuleBase = moduleBase;
                AppendLog(string.Format("[验证] yysls.exe=0x{0:X}", moduleBase));
                AppendLog(string.Format("[验证] Hook状态: {0}个hook, 当前slot={1}, store=0x{2:X}",
                    coordHookInjectAddrs.Count, coordHookActiveSlot, coordHookStoreAddr.ToInt64()));

                if (coordHookStoreAddr == IntPtr.Zero)
                {
                    AppendLog("[验证] 未初始化，请先点击'初始化坐标'");
                    return;
                }

                // 显示所有slot的坐标
                AppendLog(string.Format("[验证] 各slot捕获结果:"));
                for (int i = 0; i < coordHookInjectAddrs.Count; i++)
                {
                    long slotAddr = coordHookStoreAddr.ToInt64() + i * 16;
                    long capturedRcx = ReadInt64(hProcess, slotAddr);
                    if (capturedRcx != 0)
                    {
                        double x = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_X);
                        double y = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Y);
                        double z = ReadDouble(hProcess, capturedRcx + COORD_OFFSET_Z);
                        string mark = (i == coordHookActiveSlot) ? " *" : "";
                        AppendLog(string.Format("  [slot {0}] rcx=0x{1:X} X={2:F2} Y(高)={3:F2} Z={4:F2}{5}",
                            i, capturedRcx, x, y, z, mark));
                    }
                    else
                    {
                        AppendLog(string.Format("  [slot {0}] 未触发", i));
                    }
                }

                // 显示当前slot的详细内存
                long curRcx = ReadInt64(hProcess, coordHookStoreAddr.ToInt64() + coordHookActiveSlot * 16);
                if (curRcx == 0)
                {
                    AppendLog("[验证] 当前slot未触发，请在游戏中移动");
                    return;
                }
                AppendLog(string.Format("[验证] 当前slot {0} 详细内存(Double):", coordHookActiveSlot));
                for (long off = 0x330; off <= 0x360; off += 8)
                {
                    double v = ReadDouble(hProcess, curRcx + off);
                    AppendLog(string.Format("  [rcx+0x{0:X}] = {1:F3}", off, v));
                }

                // ===== 从相机对象回溯搜索玩家对象 =====
                // 相机对象(rcx)内部某处存有指向玩家对象的指针
                // 玩家对象的+0x340/+0x348/+0x350是Float坐标(CT表确认)
                // 扫描rcx前0x800字节的所有8字节值作为指针,检查目标+0x340是否有Float坐标
                AppendLog("[验证] 从相机对象搜索玩家对象(扫描指针链)...");
                double camX = ReadDouble(hProcess, curRcx + COORD_OFFSET_X);
                double camY = ReadDouble(hProcess, curRcx + COORD_OFFSET_Y);
                double camZ = ReadDouble(hProcess, curRcx + COORD_OFFSET_Z);
                AppendLog(string.Format("[验证] 相机坐标: X={0:F1} Y={1:F1} Z={2:F1}", camX, camY, camZ));

                int foundPlayers = 0;
                long bestPlayerPtr = 0;
                double bestPlayerDiff = double.MaxValue;
                // 扫描相机对象前0x2000字节(扩大范围),找指向玩家对象的指针
                // 玩家对象的+0x340/+0x348/+0x350存Float坐标
                AppendLog("[验证] 扫描相机对象0x2000字节找玩家指针...");
                for (long off = 0; off < 0x2000; off += 8)
                {
                    long maybePtr;
                    try { maybePtr = ReadInt64(hProcess, curRcx + off); }
                    catch { continue; }
                    if (maybePtr < 0x10000 || maybePtr > 0x7FFFFFFFFFFF) continue;
                    // 读Float坐标(用try防止不可读地址)
                    float px, py, pz;
                    try
                    {
                        px = ReadFloat(hProcess, maybePtr + 0x340);
                        py = ReadFloat(hProcess, maybePtr + 0x348);
                        pz = ReadFloat(hProcess, maybePtr + 0x350);
                    }
                    catch { continue; }
                    // 验证是否像坐标(范围合理)
                    if (Math.Abs(px) < 100000 && Math.Abs(py) < 100000 && Math.Abs(pz) < 100000 &&
                        (px != 0 || py != 0 || pz != 0))
                    {
                        // 同时尝试Double读取
                        double dx = 0, dy = 0, dz = 0;
                        try
                        {
                            dx = ReadDouble(hProcess, maybePtr + 0x340);
                            dy = ReadDouble(hProcess, maybePtr + 0x348);
                            dz = ReadDouble(hProcess, maybePtr + 0x350);
                        }
                        catch {}
                        double diffF = Math.Abs(px - camX) + Math.Abs(py - camY) + Math.Abs(pz - camZ);
                        double diffD = Math.Abs(dx - camX) + Math.Abs(dy - camY) + Math.Abs(dz - camZ);
                        AppendLog(string.Format("  [rcx+0x{0:X}] -> 0x{1:X} F:X={2:F1} Y={3:F1} Z={4:F1} (差={5:F0}) | D:X={6:F1} Y={7:F1} Z={8:F1} (差={9:F0})",
                            off, maybePtr, px, py, pz, diffF, dx, dy, dz, diffD));
                        foundPlayers++;
                        // 选差值最小的(优先Double,因为相机是Double)
                        double minDiff = Math.Min(diffF, diffD);
                        if (minDiff < bestPlayerDiff) { bestPlayerDiff = minDiff; bestPlayerPtr = maybePtr; }
                    }
                }
                if (foundPlayers == 0)
                {
                    AppendLog("[验证] 未找到玩家对象,尝试搜索附近堆内存...");
                    // 扫描rcx指向的对象+0x340偏移处可能存的指针(二级间接)
                    // 略
                }
                else
                {
                    AppendLog(string.Format("[验证] 找到{0}个候选玩家对象,最佳: 0x{1:X} (差={2:F1})",
                        foundPlayers, bestPlayerPtr, bestPlayerDiff));
                    // 保存到额外slot(用slot 10)
                    long extraSlot = coordHookStoreAddr.ToInt64() + 10 * 16;
                    WriteMemory(hProcess, extraSlot, BitConverter.GetBytes(bestPlayerPtr));
                    AppendLog(string.Format("[验证] 玩家对象已存到slot 10,可在切换slot后测试传送"));
                }
            }
            catch (Exception ex) { AppendLog("[验证] 异常: " + ex.Message); }
            finally { CloseHandle(hProcess); SafeDisposeProcess(proc); }
        }

        // 切换当前使用的slot
        void SwitchCoordSlot(int slot)
        {
            // slot 0-9 是hook捕获的(Double相机坐标), slot 10+是回溯找到的(Float玩家对象)
            int maxSlot = Math.Max(coordHookInjectAddrs.Count - 1, 10);
            if (slot < 0 || slot > maxSlot)
            {
                AppendLog(string.Format("[坐标] slot {0} 不存在，有效范围: 0-{1}", slot, maxSlot));
                return;
            }
            coordHookActiveSlot = slot;
            string type = (slot >= 10) ? "Float玩家对象" : "Double相机";
            AppendLog(string.Format("[坐标] 已切换到slot {0} ({1})", slot, type));
        }

        bool OpenGameProcessForCoord(out IntPtr hProcess, out Process proc)
        {
            hProcess = IntPtr.Zero;
            proc = null;
            Process[] procs = Process.GetProcessesByName("yysls");
            if (procs.Length == 0) { AppendLog("坐标操作失败: 未找到游戏进程"); return false; }
            if (injectedPID != 0)
            {
                foreach (Process p in procs)
                {
                    if (p.Id == injectedPID) { proc = p; break; }
                }
            }
            if (proc == null) proc = procs[0];
            for (int i = 0; i < procs.Length; i++)
            {
                if (procs[i] != proc) SafeDisposeProcess(procs[i]);
            }
            hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, proc.Id);
            if (hProcess == IntPtr.Zero) { AppendLog("坐标操作失败: 无法打开进程(请以管理员运行)"); SafeDisposeProcess(proc); proc = null; return false; }
            return true;
        }

        // 获取坐标读写用的长驻进程句柄 (优化项1)
        // 与 OpenGameProcessForCoord 区别: 句柄长驻复用, 调用方不要 CloseHandle
        // 仅用于 ReadMemCoord/WriteMemCoord 热路径; 一次性操作(Init/Verify/Cleanup)仍用 OpenGameProcessForCoord
        // 失效条件: 进程退出 / PID 变化(游戏重启或切换注入目标) → 自动重开
        IntPtr AcquireCoordHandle()
        {
            // 解析目标 PID (优先 injectedPID, 否则取第一个 yysls 进程)
            int targetPid = 0;
            Process[] procs = Process.GetProcessesByName("yysls");
            if (procs.Length == 0) { AppendLog("坐标操作失败: 未找到游戏进程"); return IntPtr.Zero; }
            try
            {
                if (injectedPID != 0)
                {
                    foreach (Process p in procs) { if (p.Id == injectedPID) { targetPid = injectedPID; break; } }
                }
                if (targetPid == 0) targetPid = procs[0].Id;
            }
            finally { for (int i = 0; i < procs.Length; i++) SafeDisposeProcess(procs[i]); }

            // 复用长驻句柄: PID 一致且进程仍存活
            if (persistentCoordHProcess != IntPtr.Zero && persistentCoordPid == targetPid && IsCoordPidAlive(targetPid))
            {
                return persistentCoordHProcess;
            }

            // 长驻句柄失效或 PID 变化, 先释放旧的再重开
            if (persistentCoordHProcess != IntPtr.Zero)
            {
                try { CloseHandle(persistentCoordHProcess); } catch { }
                persistentCoordHProcess = IntPtr.Zero;
                persistentCoordPid = 0;
            }
            IntPtr h = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, targetPid);
            if (h == IntPtr.Zero) { AppendLog("坐标操作失败: 无法打开进程(请以管理员运行)"); return IntPtr.Zero; }
            persistentCoordHProcess = h;
            persistentCoordPid = targetPid;
            return h;
        }

        // 轻量检测 PID 是否仍存活 (用于长驻句柄失效判断)
        bool IsCoordPidAlive(int pid)
        {
            if (pid == 0) return false;
            try { Process p = Process.GetProcessById(pid); p.Dispose(); return true; }
            catch { return false; }
        }

        // 通过hook捕获的rcx值解析坐标基址
        bool ResolveCoordBase(IntPtr hProcess, out long ptr2, bool debug = false)
        {
            ptr2 = 0;
            lastResolveWasPtrChain = false;
            // 优先尝试静态指针链方案(从AAA.exe提取, 直接获取玩家对象)
            if (ResolveCoordBaseByPtrChain(hProcess, out ptr2, debug))
            {
                lastResolveWasPtrChain = true;
                return true;
            }
            // 后备: 使用Hook捕获的rcx(可能命中相机对象)
            if (coordHookStoreAddr != IntPtr.Zero)
            {
                long slotAddr = coordHookStoreAddr.ToInt64() + coordHookActiveSlot * 16;
                long capturedRcx = ReadInt64(hProcess, slotAddr);
                if (capturedRcx == 0)
                {
                    if (debug) AppendLog(string.Format("[坐标] 静态指针链失败且slot {0} 尚未捕获rcx", coordHookActiveSlot));
                    return false;
                }
                // 验证地址合理性
                if (capturedRcx < 0x10000 || capturedRcx > 0x7FFFFFFFFFFF)
                {
                    if (debug) AppendLog(string.Format("[坐标] rcx值异常: 0x{0:X}", capturedRcx));
                    return false;
                }
                ptr2 = capturedRcx;
                if (debug) AppendLog(string.Format("[坐标] 使用Hook后备方案, rcx=0x{0:X}", capturedRcx));
                return true;
            }
            if (debug) AppendLog("[坐标] 未初始化且静态指针链失败");
            return false;
        }

        // 静态指针链方案: yysls.exe + STATIC_OFFSET → +0x58 → +0x00 → OBJ(玩家)
        // 从 AAA.exe 提取, 直接获取玩家对象(非相机), 无需Frida注入
        bool ResolveCoordBaseByPtrChain(IntPtr hProcess, out long ptr2, bool debug = false)
        {
            ptr2 = 0;
            if (cachedModuleBase == 0) return false;

            // 优化项4: 500ms 内复用 OBJ 基址, 跳过三层指针链 RPM
            // PID 校验确保游戏重启后缓存自动失效
            if (cachedObjBase != 0 && cachedObjBasePid == persistentCoordPid && DateTime.Now < cachedObjBaseExpireAt)
            {
                ptr2 = cachedObjBase;
                if (debug) AppendLog(string.Format("[坐标] 使用缓存OBJ=0x{0:X}", cachedObjBase));
                return true;
            }

            // 确定静态偏移(硬编码优先, 失败则AOB扫描)
            long staticOffset = cachedStaticOffset != 0 ? cachedStaticOffset : STATIC_OFFSET_DEFAULT;

            // Step 1: [yysls_base + staticOffset] → P1
            long addr1 = cachedModuleBase + staticOffset;
            long p1 = ReadInt64(hProcess, addr1);
            if (p1 == 0)
            {
                // 硬编码偏移可能失效, 触发后台AOB扫描(优化项2: 不再阻塞 UI 线程)
                if (cachedStaticOffset == 0 && !aobScanInProgress)
                {
                    if (debug) AppendLog("[坐标] 硬编码偏移失效, 启动后台AOB扫描...");
                    StartAOBScanBackground();
                }
                else if (debug && aobScanInProgress)
                {
                    AppendLog("[坐标] AOB扫描进行中, 请稍后重试");
                }
                return false;
            }
            // P1 应是堆地址, 不是模块地址
            if (p1 < 0x10000 || (p1 >= cachedModuleBase && p1 < cachedModuleBase + 0x10000000))
            {
                if (debug) AppendLog(string.Format("[坐标] P1值异常: 0x{0:X}", p1));
                return false;
            }

            // Step 2: [P1 + 0x58] → P2
            long p2 = ReadInt64(hProcess, p1 + PTR_STEP1_OFFSET);
            if (p2 == 0 || p2 < 0x10000)
            {
                if (debug) AppendLog(string.Format("[坐标] P2无效: 0x{0:X}", p2));
                return false;
            }

            // Step 3: [P2 + 0x00] → OBJ(玩家对象)
            long obj = ReadInt64(hProcess, p2 + PTR_STEP2_OFFSET);
            if (obj == 0 || obj < 0x10000 || obj > 0x7FFFFFFFFFFF)
            {
                if (debug) AppendLog(string.Format("[坐标] OBJ无效: 0x{0:X}", obj));
                return false;
            }

            // 验证坐标合理性(读取X, 应在合理范围)
            double x = ReadDouble(hProcess, obj + COORD_OFFSET_X);
            if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) > COORD_SANITY_MAX)
            {
                if (debug) AppendLog(string.Format("[坐标] OBJ+0x340值异常: {0}", x));
                return false;
            }

            ptr2 = obj;
            cachedStaticOffset = staticOffset;  // 缓存成功偏移
            // 优化项4: 缓存 OBJ 基址, 500ms TTL
            cachedObjBase = obj;
            cachedObjBasePid = persistentCoordPid;
            cachedObjBaseExpireAt = DateTime.Now.AddMilliseconds(500);
            if (debug) AppendLog(string.Format("[坐标] 静态指针链成功: OBJ=0x{0:X} X={1:F1}", obj, x));
            return true;
        }

        // 优化项2: AOB 后台扫描标志, 防止重复触发; 扫描期间 ResolveCoordBaseByPtrChain 返回 false
        bool aobScanInProgress = false;

        // 优化项2: 后台启动 AOB 扫描, 完成后回 UI 线程更新 cachedStaticOffset
        // 扫描期间(约数百毫秒)传送/读坐标会短暂失败, 用户重试即可
        void StartAOBScanBackground()
        {
            if (aobScanInProgress) return;
            if (cachedModuleBase == 0) return;
            // 后台线程需独立 PID 打开句柄, 不依赖 UI 线程的 persistentCoordHProcess
            int targetPid = persistentCoordPid != 0 ? persistentCoordPid : injectedPID;
            if (targetPid == 0) { AppendLog("[坐标] AOB扫描取消: 无目标PID"); return; }
            aobScanInProgress = true;
            AppendLog("[坐标] AOB后台扫描启动...");
            long moduleBase = cachedModuleBase;
            System.Threading.Tasks.Task.Run(() =>
            {
                long foundOffset = 0;
                IntPtr h = IntPtr.Zero;
                try
                {
                    h = OpenProcess(PROCESS_VM_READ, false, targetPid);
                    if (h != IntPtr.Zero)
                        foundOffset = AOBScanStaticOffsetCore(h, moduleBase);
                }
                catch (Exception ex) { Debug.WriteLine("AOB background scan failed: " + ex.Message); }
                finally { if (h != IntPtr.Zero) try { CloseHandle(h); } catch { } }
                // 回 UI 线程更新缓存字段
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        aobScanInProgress = false;
                        if (foundOffset != 0)
                        {
                            cachedStaticOffset = foundOffset;
                            // 优化项4: 新偏移生效, 清空旧 OBJ 缓存强制下次重新解析
                            cachedObjBase = 0;
                            AppendLog(string.Format("[坐标] AOB后台扫描完成, STATIC_OFFSET=0x{0:X}", foundOffset));
                        }
                        else
                        {
                            AppendLog("[坐标] AOB后台扫描未找到有效偏移");
                        }
                    }));
                }
                catch { aobScanInProgress = false; }  // 窗体可能已关闭
            });
        }

        // AOB扫描定位 STATIC_OFFSET: 搜索 mov r64,[rip+disp32] 指向的目标地址
        // yysls 代码段有17处 RIP-relative 引用 STATIC_OFFSET, 可稳定定位
        // 优化项2: 重构为纯函数(参数化 moduleBase), 供后台线程调用, 不依赖 UI 字段
        long AOBScanStaticOffsetCore(IntPtr hProcess, long moduleBase)
        {
            // 读取前64MB代码段
            int scanSize = 0x4000000;  // 64MB
            byte[] code = new byte[scanSize];
            IntPtr bytesRead;
            if (!ReadProcessMemory(hProcess, new IntPtr(moduleBase), code, new IntPtr(scanSize), out bytesRead))
                return 0;
            int len = (int)bytesRead;

            // 统计每个被引用偏移的出现次数
            var offsetCounts = new System.Collections.Generic.Dictionary<long, int>();
            // 搜索 48 8B 05/0D/15/1D/25/2D/35/3D (mov r64,[rip+disp32])
            // 和 48 8D 05/0D/15/1D/25/2D/35/3D (lea r64,[rip+disp32])
            for (int i = 0; i < len - 7; i++)
            {
                if (code[i] != 0x48) continue;
                if (code[i + 1] != 0x8B && code[i + 1] != 0x8D) continue;
                byte modrm = code[i + 2];
                if ((modrm & 0xC7) != 0x05) continue;  // [rip+disp32]
                int disp = BitConverter.ToInt32(code, i + 3);
                long insAddr = moduleBase + i;
                long target = insAddr + 7 + disp;
                if (target < moduleBase || target >= moduleBase + 0x10000000) continue;
                long offset = target - moduleBase;
                if (offsetCounts.ContainsKey(offset))
                    offsetCounts[offset]++;
                else
                    offsetCounts[offset] = 1;
            }

            // 找出被引用>=3次且能通过指针链验证的偏移
            // (17处引用中, STATIC_OFFSET 被引用最多)
            var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, int>>(offsetCounts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var kv in sorted)
            {
                if (kv.Value < 3) break;  // 至少被引用3次
                long off = kv.Key;
                // 验证: 读取指针链第一步
                long p1 = ReadInt64(hProcess, moduleBase + off);
                if (p1 == 0 || p1 < 0x10000) continue;
                if (p1 >= moduleBase && p1 < moduleBase + 0x10000000) continue;
                // 验证: 读取 P1+0x58
                long p2 = ReadInt64(hProcess, p1 + PTR_STEP1_OFFSET);
                if (p2 == 0 || p2 < 0x10000) continue;
                // 验证: 读取 P2+0x00
                long obj = ReadInt64(hProcess, p2 + PTR_STEP2_OFFSET);
                if (obj == 0 || obj < 0x10000 || obj > 0x7FFFFFFFFFFF) continue;
                // 验证: 读取坐标
                double x = ReadDouble(hProcess, obj + COORD_OFFSET_X);
                if (double.IsNaN(x) || Math.Abs(x) > COORD_SANITY_MAX) continue;

                AppendLog(string.Format("[坐标] AOB扫描找到STATIC_OFFSET=0x{0:X} (引用{1}次)", off, kv.Value));
                return off;
            }
            AppendLog("[坐标] AOB扫描未找到有效STATIC_OFFSET");
            return 0;
        }

        // 通过hook捕获的寄存器值读取坐标 (Double) + 相机偏移校正
        bool ReadMemCoord(out double x, out double y, out double z, bool debug = false)
        {
            x = y = z = 0;
            IntPtr hProcess = AcquireCoordHandle();  // 优化项1: 长驻句柄复用
            if (hProcess == IntPtr.Zero) return false;
            try
            {
                long ptr2;
                if (!ResolveCoordBase(hProcess, out ptr2, debug)) return false;
                double rawX, rawY, rawZ;
                // 静态指针链方案: 直接玩家对象, 全Double, 无需相机校正
                if (lastResolveWasPtrChain)
                {
                    rawX = ReadDouble(hProcess, ptr2 + COORD_OFFSET_X);
                    rawY = ReadDouble(hProcess, ptr2 + COORD_OFFSET_Y);
                    rawZ = ReadDouble(hProcess, ptr2 + COORD_OFFSET_Z);
                }
                // Hook后备方案: slot 10+ 是Float玩家对象, 其他是Double相机
                else if (coordHookActiveSlot >= 10)
                {
                    rawX = ReadFloat(hProcess, ptr2 + COORD_OFFSET_X);
                    rawY = ReadFloat(hProcess, ptr2 + COORD_OFFSET_Y);
                    rawZ = ReadFloat(hProcess, ptr2 + COORD_OFFSET_Z);
                }
                else
                {
                    rawX = ReadDouble(hProcess, ptr2 + COORD_OFFSET_X);
                    rawY = ReadDouble(hProcess, ptr2 + COORD_OFFSET_Y);
                    rawZ = ReadDouble(hProcess, ptr2 + COORD_OFFSET_Z);
                    // 相机坐标 + 偏移 = 玩家坐标
                    rawX += CAM_OFFSET_X;
                    rawY += CAM_OFFSET_Y;
                    rawZ += CAM_OFFSET_Z;
                }
                x = rawX; y = rawY; z = rawZ;
                if (debug) AppendLog(string.Format("[坐标] ptr2=0x{0:X} X={1:F1} Y(高)={2:F1} Z={3:F1}", ptr2, x, y, z));
                return true;
            }
            catch (Exception ex) { AppendLog("读取坐标异常: " + ex.Message); return false; }
            // 优化项1: 不在此 CloseHandle, 长驻句柄由 AcquireCoordHandle/OnFormClosing 管理生命周期
        }

        // ===== 传送后按键模拟 (PostMessageW 方案, 对齐 AAA.exe) =====
        // 关键: AAA 用 PostMessageW 给游戏窗口发 WM_KEYDOWN/UP, 可后台无需前台焦点
        // 早期 banyi 用 SendInput 需游戏前台且仍被拉回, 改 PostMessageW 解决
        string tpKeySequence = "20,51";  // VK_SPACE=0x20, VK_Q=0x51
        int tpKeyDelayMs = 50;           // 按键间隔(毫秒)
        IntPtr cachedGameHwnd = IntPtr.Zero;  // 游戏窗口句柄缓存

        // 查找游戏窗口句柄 (yysls.exe 的可见顶层窗口)
        IntPtr FindGameWindow()
        {
            if (cachedGameHwnd != IntPtr.Zero) return cachedGameHwnd;
            IntPtr found = IntPtr.Zero;
            int targetPid = persistentCoordPid;
            if (targetPid == 0)
            {
                try
                {
                    var procs = System.Diagnostics.Process.GetProcessesByName("yysls");
                    if (procs.Length > 0) targetPid = procs[0].Id;
                }
                catch { }
            }
            if (targetPid == 0) return IntPtr.Zero;
            EnumWindows((hWnd, lp) =>
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == (uint)targetPid && IsWindowVisible(hWnd))
                {
                    found = hWnd;
                    return false;  // 找到即停
                }
                return true;
            }, IntPtr.Zero);
            cachedGameHwnd = found;
            return found;
        }

        // PostMessageW 发送单键 按下+抬起
        void PostKeyPress(IntPtr hWnd, ushort vk)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessageW(hWnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            System.Threading.Thread.Sleep(tpKeyDelayMs);
            PostMessageW(hWnd, WM_KEYUP, (IntPtr)vk, (IntPtr)0xC0000000);
        }

        // PostMessageW 发送 Alt+键 (用 WM_SYSKEYDOWN/UP, wParam 为 vk, lParam 含 Alt 标志)
        void PostKeyWithAlt(IntPtr hWnd, ushort vk)
        {
            if (hWnd == IntPtr.Zero) return;
            // lParam bit20(0x20000000) = context code, Alt 按下
            PostMessageW(hWnd, WM_SYSKEYDOWN, (IntPtr)vk, (IntPtr)0x20000001);
            System.Threading.Thread.Sleep(tpKeyDelayMs);
            PostMessageW(hWnd, WM_SYSKEYUP, (IntPtr)vk, (IntPtr)0xE0000001);
        }

        // 传送后触发按键序列 (冻结窗口内执行, PostMessageW 到游戏窗口)
        void FireTeleportKeys()
        {
            if (string.IsNullOrEmpty(tpKeySequence)) return;
            IntPtr hWnd = FindGameWindow();
            if (hWnd == IntPtr.Zero)
            {
                AppendLog("[传送] 未找到游戏窗口, 跳过按键模拟");
                return;
            }
            try
            {
                string[] parts = tpKeySequence.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string p in parts)
                {
                    ushort vk;
                    if (ushort.TryParse(p.Trim(), System.Globalization.NumberStyles.HexNumber, null, out vk))
                    {
                        PostKeyPress(hWnd, vk);
                    }
                }
                AppendLog(string.Format("[传送] PostMessageW 已发送序列: {0} hWnd=0x{1:X}", tpKeySequence, hWnd.ToInt64()));
            }
            catch (Exception ex) { AppendLog("[传送] 按键模拟失败: " + ex.Message); }
        }

        // ===== 微调传送 (对齐 AAA.exe 的 Alt+方向键/空格/C 行为) =====
        // AAA 逆向结论: 微调传送是直接写入坐标内存, 无按键模拟, 无冻结
        // 偏移方向: +X=东, -X=西, +Z=南, -Z=北, +Y=上, -Y=下
        // 步长参考 AAA 实测: 水平 3.0 米, 垂直上 2.0 米, 垂直下 6.0 米
        const double NUDGE_HORIZONTAL = 3.0;
        const double NUDGE_UP = 2.0;
        const double NUDGE_DOWN = 6.0;

        // 微调传送: 读取当前坐标 + 偏移 + 单次写入 (不冻结不按键)
        void NudgeTeleport(double dx, double dy, double dz)
        {
            if (!chkEnableMemory.Checked)
            {
                AppendLog("微调失败: 请先勾选'启用内存功能'");
                return;
            }
            double x, y, z;
            if (!ReadMemCoord(out x, out y, out z))
            {
                AppendLog("微调失败: 无法读取当前坐标 (请先初始化坐标)");
                return;
            }
            double nx = x + dx, ny = y + dy, nz = z + dz;
            if (WriteMemCoord(nx, ny, nz, false))
            {
                AppendLog(string.Format("[微调] ({0:F1},{1:F1},{2:F1}) -> ({3:F1},{4:F1},{5:F1})  dx={6} dy={7} dz={8}",
                    x, y, z, nx, ny, nz, dx, dy, dz));
            }
            else
            {
                AppendLog("[微调] 写入失败");
            }
        }

        // 更新键盘钩子状态: 任一模式开启则安装钩子, 都关闭则卸载
        void UpdateNudgeHookState()
        {
            bool needHook = flyModeEnabled || nudgeModeEnabled;
            if (needHook && nudgeKbHook == IntPtr.Zero)
            {
                using (System.Diagnostics.Process curProc = System.Diagnostics.Process.GetCurrentProcess())
                using (System.Diagnostics.ProcessModule mod = curProc.MainModule)
                {
                    nudgeKbHook = SetWindowsHookEx(WH_KEYBOARD_LL, NudgeKbProc, GetModuleHandle(mod.ModuleName), 0);
                }
                if (nudgeKbHook == IntPtr.Zero)
                {
                    AppendLog("[微调] 键盘钩子安装失败: " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }
            }
            else if (!needHook && nudgeKbHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(nudgeKbHook);
                nudgeKbHook = IntPtr.Zero;
                // 重置按键去抖动状态, 避免下次开启时被卡住
                flyKeyUp = flyKeyDown = true;
                nudgeKeyLeft = nudgeKeyRight = nudgeKeyUp = nudgeKeyDown = true;
            }
        }

        // 低级键盘钩子回调: 在 UI 线程外执行, 不可直接操作控件, 仅触发微调传送
        IntPtr NudgeKbProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vk = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
                uint msg = (uint)wParam.ToInt64();
                bool isDown = (msg == WM_KEYDOWN_LL || msg == WM_SYSKEYDOWN_LL);
                bool isUp = (msg == WM_KEYUP_LL || msg == WM_SYSKEYUP_LL);

                if (flyModeEnabled && (vk == VK_UP || vk == VK_DOWN))
                {
                    // 飞天遁地用无 Alt 的方向键 (WM_KEYDOWN), 与瞬移模式的 Alt+方向键 (WM_SYSKEYDOWN) 互不干扰
                    if (vk == VK_UP && isDown && flyKeyUp)
                    {
                        flyKeyUp = false;
                        BeginInvoke((Action)(() => NudgeTeleport(0, NUDGE_UP, 0)));
                    }
                    else if (vk == VK_DOWN && isDown && flyKeyDown)
                    {
                        flyKeyDown = false;
                        BeginInvoke((Action)(() => NudgeTeleport(0, -NUDGE_DOWN, 0)));
                    }
                    else if (vk == VK_UP && isUp) flyKeyUp = true;
                    else if (vk == VK_DOWN && isUp) flyKeyDown = true;
                }

                if (nudgeModeEnabled)
                {
                    // Alt+方向键 (WM_SYSKEYDOWN/UP), 拦截方向键避免游戏同时响应
                    if (msg == WM_SYSKEYDOWN_LL || msg == WM_SYSKEYUP_LL)
                    {
                        if (vk == VK_UP && isDown && nudgeKeyUp)
                        {
                            nudgeKeyUp = false;
                            BeginInvoke((Action)(() => NudgeTeleport(0, 0, -NUDGE_HORIZONTAL)));
                        }
                        else if (vk == VK_DOWN && isDown && nudgeKeyDown)
                        {
                            nudgeKeyDown = false;
                            BeginInvoke((Action)(() => NudgeTeleport(0, 0, NUDGE_HORIZONTAL)));
                        }
                        else if (vk == VK_LEFT && isDown && nudgeKeyLeft)
                        {
                            nudgeKeyLeft = false;
                            BeginInvoke((Action)(() => NudgeTeleport(-NUDGE_HORIZONTAL, 0, 0)));
                        }
                        else if (vk == VK_RIGHT && isDown && nudgeKeyRight)
                        {
                            nudgeKeyRight = false;
                            BeginInvoke((Action)(() => NudgeTeleport(NUDGE_HORIZONTAL, 0, 0)));
                        }
                        else if (isUp)
                        {
                            if (vk == VK_UP) nudgeKeyUp = true;
                            else if (vk == VK_DOWN) nudgeKeyDown = true;
                            else if (vk == VK_LEFT) nudgeKeyLeft = true;
                            else if (vk == VK_RIGHT) nudgeKeyRight = true;
                        }
                    }
                }
            }
            return CallNextHookEx(nudgeKbHook, nCode, wParam, lParam);
        }

        // 通过hook捕获的寄存器值写入坐标 + 冻结
        // freezeAndKeys=false 时仅写入一次, 不启动冻结定时器也不发按键 (用于微调传送, 对齐 AAA.exe 行为)
        bool WriteMemCoord(double x, double y, double z, bool freezeAndKeys = true)
        {
            if (!chkEnableMemory.Checked)
            {
                AppendLog("传送失败: 请先勾选'启用内存功能'");
                return false;
            }
            IntPtr hProcess = AcquireCoordHandle();  // 优化项1: 长驻句柄复用
            if (hProcess == IntPtr.Zero) return false;
            try
            {
                long ptr2;
                if (!ResolveCoordBase(hProcess, out ptr2, true))
                {
                    AppendLog("传送失败: 无法解析坐标基址");
                    return false;
                }

                // 静态指针链方案: 直接写玩家对象, 全Double, 无需相机校正
                // Hook后备方案: slot 10+ 写Float, 其他写Double(相机坐标=玩家目标-偏移)
                bool ok = true;
                if (lastResolveWasPtrChain)
                {
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_X, x);
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_Y, y);
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_Z, z);
                    AppendLog(string.Format("[传送] 写入玩家坐标: X={0:F1} Y(高)={1:F1} Z={2:F1}", x, y, z));
                }
                else if (coordHookActiveSlot >= 10)
                {
                    ok &= WriteFloat(hProcess, ptr2 + COORD_OFFSET_X, (float)x);
                    ok &= WriteFloat(hProcess, ptr2 + COORD_OFFSET_Y, (float)y);
                    ok &= WriteFloat(hProcess, ptr2 + COORD_OFFSET_Z, (float)z);
                }
                else
                {
                    // 写入相机坐标 = 玩家目标坐标 - 偏移
                    double camX = x - CAM_OFFSET_X;
                    double camY = y - CAM_OFFSET_Y;
                    double camZ = z - CAM_OFFSET_Z;
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_X, camX);
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_Y, camY);
                    ok &= WriteDouble(hProcess, ptr2 + COORD_OFFSET_Z, camZ);
                    AppendLog(string.Format("[传送] 写入相机坐标: X={0:F1} Y={1:F1} Z={2:F1}", camX, camY, camZ));
                }

                if (!freezeAndKeys)
                {
                    // 微调传送: 单次写入即可, 不冻结不按键
                    return ok;
                }

                // 关闭上一次冻结 (优化项1: 句柄长驻, 仅停止定时器, 不 CloseHandle)
                if (coordFreezeTimer != null && coordFreezeTimer.Enabled)
                {
                    coordFreezeTimer.Stop();
                }

                // 启动冻结：短时间反复写入防止引擎覆盖
                // 注意: lockCoordHProcess 复用长驻句柄, CoordFreezeTick 不应 CloseHandle 它
                lockCoordX = x; lockCoordY = y; lockCoordZ = z;
                lockCoordBase = ptr2;
                lockCoordHProcess = hProcess;
                lockCoordSlot = coordHookActiveSlot;
                lockCoordIsPtrChain = lastResolveWasPtrChain;
                coordFreezeCount = 0;
                if (coordFreezeTimer == null)
                {
                    coordFreezeTimer = new System.Windows.Forms.Timer();
                    coordFreezeTimer.Interval = 16;
                    coordFreezeTimer.Tick += CoordFreezeTick;
                }
                coordFreezeTimer.Start();
                // 传送后触发按键序列(空格+Q), 绕过游戏位移回滚机制
                // 在冻结窗口(约1秒)内发送, 此时坐标正被反复写入锁定
                FireTeleportKeys();
                return ok;
            }
            catch (Exception ex) { AppendLog("传送异常: " + ex.Message); return false; }
            // 优化项1: 不在此 CloseHandle, 长驻句柄由 AcquireCoordHandle/OnFormClosing 管理生命周期
        }

        double lockCoordX, lockCoordY, lockCoordZ;
        long lockCoordBase;
        IntPtr lockCoordHProcess;
        int coordFreezeCount;
        int lockCoordSlot; // 记录冻结时的slot,区分Float/Double
        bool lockCoordIsPtrChain; // 冻结时是否用静态指针链方案
        System.Windows.Forms.Timer coordFreezeTimer;

        void CoordFreezeTick(object sender, EventArgs e)
        {
            coordFreezeCount++;
            // 优化项3: 句柄空校验, 防止定时器多触发一次 tick 后写入已关闭/无效句柄
            if (lockCoordHProcess == IntPtr.Zero) return;
            try
            {
                if (lockCoordIsPtrChain)
                {
                    // 静态指针链方案: 直接写玩家对象Double坐标
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_X, lockCoordX);
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Y, lockCoordY);
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Z, lockCoordZ);
                }
                else if (lockCoordSlot >= 10)
                {
                    WriteFloat(lockCoordHProcess, lockCoordBase + COORD_OFFSET_X, (float)lockCoordX);
                    WriteFloat(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Y, (float)lockCoordY);
                    WriteFloat(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Z, (float)lockCoordZ);
                }
                else
                {
                    // 冻结写入相机坐标(玩家目标-偏移)
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_X, lockCoordX - CAM_OFFSET_X);
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Y, lockCoordY - CAM_OFFSET_Y);
                    WriteDouble(lockCoordHProcess, lockCoordBase + COORD_OFFSET_Z, lockCoordZ - CAM_OFFSET_Z);
                }
            }
            catch { }
            if (coordFreezeCount >= 60) // 持续约1秒
            {
                coordFreezeTimer.Stop();
                // 优化项1: 句柄长驻, 不在此 CloseHandle
                // 优化项3: 重置为 Zero, 标记冻结结束, 避免后续 tick 重复写入
                lockCoordHProcess = IntPtr.Zero;
            }
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
            // 自动从运行中的进程检测游戏路径
            if (gameRunning && string.IsNullOrEmpty(gameRootPath))
            {
                try
                {
                    string exePath = procs[0].MainModule.FileName;
                    string binDir = Path.GetDirectoryName(exePath);
                    string rootDir = Path.GetDirectoryName(binDir);
                    if (File.Exists(Path.Combine(binDir, GameExeName)))
                    {
                        gameRootPath = rootDir;
                        gameBinPath = binDir;
                        UpdateCommPaths();
                        AppendLog("自动检测游戏路径: " + gameRootPath);
                        SaveConfig();
                    }
                }
                catch { }
            }
            try
            {
                if (isReady) { status = "已就绪 - 可以使用 GM 命令"; color = Color.Green; }
                else if (gameRunning) { status = "游戏运行中 - 点[注入]"; color = Color.Blue; }
                else if (!gameRunning && gameLaunched) { status = "游戏可能已退出"; color = Color.Red; gameLaunched = false; }
                else if (!gameRunning && string.IsNullOrEmpty(gameRootPath)) { status = "游戏未运行 - 请先设置目录"; color = Color.Gray; }
                else if (!gameRunning) { status = "游戏未运行 - " + gameRootPath; color = Color.Gray; }
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
            bool memEnabled = chkEnableMemory.Checked;
            foreach (Control control in parent.Controls)
            {
                // 初始标签页的按钮始终可点击（注入按钮除外）
                if (parent == tabInit || parent == grpGM && control == tabInit) continue;
                // 内存操作控件受 chkEnableMemory 控制
                bool isMemControl = (control.Tag as string) == "mem";
                bool controlEnabled = enabled;
                if (isMemControl) controlEnabled = memEnabled;
                Button button = control as Button;
                if (button != null && (button.Tag as string) != "nav")
                    button.Enabled = controlEnabled;
                CheckBox chk = control as CheckBox;
                if (chk != null && chk != chkEnableMemory) chk.Enabled = controlEnabled;
                ComboBox combo = control as ComboBox;
                if (combo != null) combo.Enabled = controlEnabled;
                TextBox tb = control as TextBox;
                if (tb != null && !tb.ReadOnly) tb.Enabled = controlEnabled;
                TrackBar trk = control as TrackBar;
                if (trk != null) trk.Enabled = controlEnabled;
                DataGridView dgv = control as DataGridView;
                if (dgv != null) dgv.Enabled = controlEnabled;
                if (control.HasChildren) SetButtonsEnabled(control, enabled);
            }
        }

        void chkEnableMemory_CheckedChanged(object sender, EventArgs e)
        {
            // 刷新所有内存控件状态
            SetGMEnabled(isReady);
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
            string[] paths = new string[] { CmdFile, CmdResultFile, ToolResultFile, UnifiedLogFile, Path.Combine(ToolDir, "ready.txt"), Path.Combine(ToolDir, "trace.txt"), Path.Combine(ToolDir, "connector_log.txt"), Path.Combine(ToolDir, "gm_tool_ui.log"), Path.Combine(ToolDir, "gm_tool_all.log"), Path.Combine(ToolDir, "gm_signal.txt"), Path.Combine(ToolDir, "svc.cfg"), Path.Combine(ToolDir, "coord_ptr.txt") };
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

            // 实时坐标刷新定时器: 每 500ms 读取当前坐标并更新 lblLiveCoord
            liveCoordTimer = new System.Windows.Forms.Timer();
            liveCoordTimer.Interval = 500;
            liveCoordTimer.Tick += (s, e2) =>
            {
                if (!chkEnableMemory.Checked) return;
                double x, y, z;
                if (ReadMemCoord(out x, out y, out z))
                {
                    lblLiveCoord.Text = string.Format("X={0:F1}  Y={1:F1}  Z={2:F1}", x, y, z);
                    lastReadX = x; lastReadY = y; lastReadZ = z;
                }
            };
            liveCoordTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { if (commandResultTimer != null) { commandResultTimer.Stop(); commandResultTimer.Dispose(); commandResultTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose commandResultTimer failed: " + ex.Message); }
            try { if (injectionDiagTimer != null) { injectionDiagTimer.Stop(); injectionDiagTimer.Dispose(); injectionDiagTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose injectionDiagTimer failed: " + ex.Message); }
            try { if (readyPollTimer != null) { readyPollTimer.Stop(); readyPollTimer.Dispose(); readyPollTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose readyPollTimer failed: " + ex.Message); }
            try { if (coordFreezeTimer != null) { coordFreezeTimer.Stop(); coordFreezeTimer.Dispose(); coordFreezeTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose coordFreezeTimer failed: " + ex.Message); }
            try { if (liveCoordTimer != null) { liveCoordTimer.Stop(); liveCoordTimer.Dispose(); liveCoordTimer = null; } } catch (Exception ex) { Debug.WriteLine("Dispose liveCoordTimer failed: " + ex.Message); }
            try { if (nudgeKbHook != IntPtr.Zero) { UnhookWindowsHookEx(nudgeKbHook); nudgeKbHook = IntPtr.Zero; } } catch (Exception ex) { Debug.WriteLine("Unhook nudgeKbHook failed: " + ex.Message); }
            try { if (persistentCoordHProcess != IntPtr.Zero) { CloseHandle(persistentCoordHProcess); persistentCoordHProcess = IntPtr.Zero; persistentCoordPid = 0; } } catch (Exception ex) { Debug.WriteLine("Dispose persistentCoordHProcess failed: " + ex.Message); }
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
