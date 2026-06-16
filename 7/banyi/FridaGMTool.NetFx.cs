using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace FridaGMTool
{
    class GMForm : Form
    {
        // Paths
        static string WorkDir = @"E:\ceshi\7\banyi";
        static string ToolDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static string OriginalDir = @"E:\ceshi\7";
        static string OriginalInjector = Path.Combine(OriginalDir, "右键管理员运行.exe");
        static string OriginalFridaGadget = Path.Combine(OriginalDir, "frida-gadget.dll");
        static string OriginalDinput8 = Path.Combine(OriginalDir, "dinput8.dll");
        static string PortableToolFallbackDir = @"E:\ceshi\7\新建文件夹";
        static string PortableInjectorName = "Extreme Injector v3.exe";
        static string PayloadJs = Path.Combine(WorkDir, "gm_payload.js");
        static string TestLuaMenuScript = @"E:\ceshi\7\Test.lua-main\test.lua";
        static string ConfigFile = Path.Combine(ToolDir, "config.txt");
        static string BuffConfigFile = Path.Combine(ToolDir, "buff_config.txt");
        static readonly int[] OutfitIds = new int[] { 1570003, 400148, 1470000, 30335, 310218, 310219, 310221, 30524, 310224, 30384, 30385, 30386, 30387, 30388, 30389, 30390, 30391, 30392, 30393, 30394, 30395, 30396, 30397, 30398, 50103, 50104, 80000, 109623, 1007004, 1030000, 1230011, 1230012, 1240000, 1240001, 1240002, 1240003, 1240004, 1460000, 1460001, 1460002, 1460003, 1460004, 1460005, 1460006, 1460007, 1460008, 1460009, 1460010, 1460021, 1460024, 1460025, 1460026, 1460027, 1460028, 1460043, 1460043, 1460045, 1460046, 1460047, 1700000, 1760001, 1330001, 1450010, 1450011, 1420001, 1310000, 1250016, 1250002, 1010007, 1100000, 1170007, 1471009, 1740002, 1475007, 1820000, 1920001, 1660020, 1660021, 1500120, 1400000, 1400004, 1100000, 1770000, 1500170, 1540010, 50014, 1290000, 1290001, 1410050, 1410051, 1360005, 1560000, 1750000, 1050001, 1050013, 1040006, 1040008, 1020013, 1020014, 1020015, 1020017, 1020018, 1010000, 1070032, 1070052, 1080003, 1080015, 1080021, 1080054, 1080067, 1080073, 1080074, 910324, 910325, 910328, 910329, 910330, 910332, 910446, 960001, 440020, 440093, 450009, 310211 };
        static readonly string[] OutfitNames = new string[] {
            "变成猪头", "瞬间秒杀一切", "江叔", "发光特效", "变小", "变小近距", "变小可操作", "变大", "滑冰", "穿心者", "一刀", "天鹰", "沉睡道士", "木鹰", "郑厄", "地煞", "龙王", "虚王", "十七", "蛇医", "河主", "十七 (P2)", "道君", "傀儡师", "讲经人 (男)", "讲经人 (女)", "安西军", "全身发光", "刺客", "漂流者套装", "少冬瓜", "变身 L", "发疯 - 狗", "发疯 - 驴", "发疯 - 鹿", "发疯 - 熊", "发疯 - 鸟", "人间仙", "无相", "花瓣挽歌", "春回大地", "红幕", "平静生活", "换装 6", "换装 7", "沧海威严", "九尾传承", "雅乐风华", "笔", "大鹅", "公鸡", "土拨鼠", "驴道人的驴", "冲天炮", "陨石", "咸鱼", "独眼老鼠", "爆炸桶", "踢鞠小人", "浴衣", "大宋提刑官", "少年猪脚", "高冬瓜人", "矮冬瓜人", "绿衣公公", "衣服女", "火眼金睛-特效", "移动麻布袋", "战斗麻布袋", "美女", "白衣小哥", "兵", "老虎", "苹果精", "玫瑰花快速", "赵二", "超级战斗鸡", "超级战斗鹅", "巨子青", "无头鬼新娘", "有头鬼新娘", "女npc", "小蓝猫", "大猫猫", "大鹅", "机械蚂蚁", "好看女npc", "好看女npc2", "可以取消的狼王", "可以取消的鹰", "韩通", "韩守谅", "mini苹果", "马儿", "台柱子", "红色短裤", "黄色短裤", "白发", "面具", "白发面具", "盾牌", "方天画戟", "鹿角", "超好看特效", "超好看特效2", "武器带水特效", "角面具", "萤火虫特效", "红叶特效", "欲火焚身特效", "白鹤特效", "水汽特效", "地狱火特效", "地狱火白特效", "地狱武器附火", "头顶花瓣", "第一人称", "花瓣墙特效", "光圈特效", "帽子小哥", "闪电特效", "闪电特效2", "脚底白云特效", "不良人"
        };
        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const long GLOBAL_BASE_OFFSET = 0x07C04698;

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr dwSize, out IntPtr lpNumberOfBytesWritten);

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
        static string GameExeName = "yysls.exe";

        Label lblStatus;
        Label lblGamePath;
        Panel grpGM;
        Panel tabGM;
        Button btnBrowse;
        Button btnCopyFiles, btnClearFiles, btnStartGame, btnInjectFrida;
        Button btnRefresh;
        Button btnGod, btnStamina, btnInvis;
        Button btnAtkBuff, btnDefBuff;
        Button btnCancelInvis;
        Button btnYyAutoLoot, btnYyNpcDumb, btnYyRecover, btnYySuperDodge, btnYyAutoBuff, btnYyLoadScript, btnCnMenuLoad;
        Button btnLoopBuff, btnLoopLoot, btnLoopRecover, btnLoopDefense;
        Button btnGatherBuff, btnAuxBuff, btnUnknownBuff;
        Button btnCombatSkill, btnScrollBuff, btnAttrBuff;
        Button btnAutoChest, btnRhythmGame, btnChessWin, btnGmTrainPanel, btnTestLuaMenu, btnApplyOutfit, btnOpenOutfitPicker;
        Button btnStaminaDive, btnStaminaEmpty, btnStaminaResetAll, btnDisableLogs, btnAutoLootPlus, btnPitchPot, btnApplyAtkMul, btnResetAtkMul, btnApplyDialogSpeed, btnResetDialogSpeed;
        ComboBox cmbAtkMul, cmbDialogSpeed, cmbOutfit;
        TextBox txtLog = null;

        string gameRootPath = "";
        string gameBinPath = "";
        bool isReady = false;
        bool gameLaunched = false;
        bool commandPending = false;

        string CmdFile = "";
        string FridaLogFile = "";
        string HackLogFile = "";
        string CmdResultFile = "";
        string ToolResultFile = Path.Combine(ToolDir, "gm_tool_result.txt");
        string ToolResultCompatFile = Path.Combine(WorkDir, "gm_tool_result.txt");
        string UnifiedLogFile = Path.Combine(ToolDir, "gm_tool.log");

        public GMForm()
        {
            Text = "燕云十六声 辅助工具 v13.0";
            Size = new Size(600, 540);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            int y = 10;

            // === 操作区 ===
            var grpAction = new Panel { Location = new Point(10, y), Size = new Size(560, 105), BackColor = Color.White };
            btnBrowse = new Button { Text = "选择目录", Location = new Point(10, 10), Size = new Size(105, 30), BackColor = Color.White };
            btnBrowse.Click += BtnBrowse_Click;
            grpAction.Controls.Add(btnBrowse);
            btnCopyFiles = new Button { Text = "复制文件", Location = new Point(120, 10), Size = new Size(105, 30), BackColor = Color.LightGreen };
            btnCopyFiles.Click += BtnCopyFiles_Click_B;
            grpAction.Controls.Add(btnCopyFiles);
            btnClearFiles = new Button { Text = "清理文件", Location = new Point(230, 10), Size = new Size(105, 30), BackColor = Color.White };
            btnClearFiles.Click += BtnClearFiles_Click_B;
            grpAction.Controls.Add(btnClearFiles);
            lblGamePath = new Label { Text = "未选择目录", Location = new Point(345, 16), Size = new Size(200, 18), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8) };
            grpAction.Controls.Add(lblGamePath);

            btnStartGame = new Button { Text = "1.启动游戏", Location = new Point(10, 48), Size = new Size(105, 30), BackColor = Color.LightYellow };
            btnStartGame.Click += BtnStartGame_Click;
            grpAction.Controls.Add(btnStartGame);
            btnInjectFrida = new Button { Text = "2.注入Frida", Location = new Point(120, 48), Size = new Size(105, 30), BackColor = Color.LightPink };
            btnInjectFrida.Click += BtnInjectFrida_Click_B;
            grpAction.Controls.Add(btnInjectFrida);

            var lblHint = new Label { Text = "方案B: 复制文件 → 启动游戏 → 注入Frida", Location = new Point(230, 54), Size = new Size(310, 18), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei", 8) };
            grpAction.Controls.Add(lblHint);
            btnRefresh = new Button { Text = "刷新状态", Location = new Point(10, 80), Size = new Size(105, 22) };
            btnRefresh.Click += (s, e) => CheckState();
            grpAction.Controls.Add(btnRefresh);
            var btnQuickOpenLog = new Button { Text = "打开日志", Location = new Point(120, 80), Size = new Size(105, 22) };
            btnQuickOpenLog.Click += (s, e) => {
                string logToOpen = BuildUnifiedLogSnapshot();
                if (!string.IsNullOrEmpty(logToOpen) && File.Exists(logToOpen)) Process.Start("notepad.exe", logToOpen);
                else MessageBox.Show("日志文件不存在");
            };
            grpAction.Controls.Add(btnQuickOpenLog);
            var btnQuickOpenLogDir = new Button { Text = "打开目录", Location = new Point(230, 80), Size = new Size(105, 22) };
            btnQuickOpenLogDir.Click += (s, e) => { if (Directory.Exists(ToolDir)) Process.Start("explorer.exe", ToolDir); };
            grpAction.Controls.Add(btnQuickOpenLogDir);
            var btnQuickClearLog = new Button { Text = "清除日志", Location = new Point(340, 80), Size = new Size(105, 22) };
            btnQuickClearLog.Click += (s, e) => {
                ClearManagedFiles();
                isReady = false;
                UpdateStatus();
            };
            grpAction.Controls.Add(btnQuickClearLog);
            Controls.Add(grpAction);
            y += 115;

            // === 状态 ===
            lblStatus = new Label { Text = "状态: 请先选择游戏目录", Location = new Point(10, y), Size = new Size(560, 22), ForeColor = Color.Blue, Font = new Font("Microsoft YaHei", 9, FontStyle.Bold) };
            Controls.Add(lblStatus);
            y += 30;

            // === GM 命令 ===
            grpGM = new Panel { Location = new Point(10, y), Size = new Size(560, 288), BackColor = Color.White };
            int gy = 12;
            const int rowStep = 32;
            const int sectionGap = 8;
            Action<string> addSection = (title) => {
                if (grpGM.Controls.Count > 0) gy += rowStep + sectionGap;
                var lbl = new Label { Text = "── " + title + " ──", Location = new Point(10, gy), Size = new Size(540, 18), ForeColor = Color.FromArgb(80, 80, 80), Font = new Font("Microsoft YaHei", 8, FontStyle.Bold) };
                grpGM.Controls.Add(lbl);
                gy += 21;
            };

            addSection("战斗增强");
            btnGod = new Button { Text = "无敌", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnGod.Click += (s, e) => SendExperimentCommand("无敌", BuildCombatExperimentLua("god"));
            grpGM.Controls.Add(btnGod);
            btnStamina = new Button { Text = "锁体力消耗", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnStamina.Click += (s, e) => SendExperimentCommand("锁体力消耗", BuildCombatExperimentLua("stamina_lock"));
            grpGM.Controls.Add(btnStamina);
            btnStaminaDive = new Button { Text = "无限潜水资源", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnStaminaDive.Click += (s, e) => SendExperimentCommand("无限潜水资源", BuildCombatExperimentLua("stamina_dive"));
            grpGM.Controls.Add(btnStaminaDive);
            btnInvis = new Button { Text = "隐身", Location = new Point(400, gy), Size = new Size(125, 30) };
            btnInvis.Click += (s, e) => SendExperimentCommand("隐身", BuildCombatExperimentLua("invis"));
            grpGM.Controls.Add(btnInvis);

            gy += rowStep;
            btnCancelInvis = new Button { Text = "取消隐身", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnCancelInvis.Click += (s, e) => SendExperimentCommand("取消隐身", BuildCombatExperimentLua("invis_off"));
            grpGM.Controls.Add(btnCancelInvis);
            btnStaminaEmpty = new Button { Text = "清空战斗资源", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnStaminaEmpty.Click += (s, e) => SendExperimentCommand("清空战斗资源", BuildCombatExperimentLua("stamina_empty"));
            grpGM.Controls.Add(btnStaminaEmpty);
            btnStaminaResetAll = new Button { Text = "恢复体力设置", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnStaminaResetAll.Click += (s, e) => SendExperimentCommand("恢复体力设置", BuildCombatExperimentLua("stamina_reset_all"));
            grpGM.Controls.Add(btnStaminaResetAll);

            gy += rowStep;
            btnYyNpcDumb = new Button { Text = "NPC变笨", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnYyNpcDumb.Click += (s, e) => SendExperimentCommand("NPC变笨", BuildYyLaoLiuLua("yy_npcdumb"));
            grpGM.Controls.Add(btnYyNpcDumb);
            btnYySuperDodge = new Button { Text = "超级闪避", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnYySuperDodge.Click += (s, e) => SendExperimentCommand("超级闪避", BuildYyLaoLiuLua("yy_superdodge"));
            grpGM.Controls.Add(btnYySuperDodge);

            addSection("Buff 施加 (一次性)");
            btnAtkBuff = new Button { Text = "攻击Buff", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnAtkBuff.Click += (s, e) => SendExperimentCommand("攻击Buff整合", BuildCombatExperimentLua("atkbuff_combo"));
            grpGM.Controls.Add(btnAtkBuff);
            btnDefBuff = new Button { Text = "防御Buff", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnDefBuff.Click += (s, e) => SendExperimentCommand("防御Buff", BuildCombatExperimentLua("defbuff"));
            grpGM.Controls.Add(btnDefBuff);

            gy += rowStep;
            btnGatherBuff = new Button { Text = "采集Buff", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnGatherBuff.Click += (s, e) => SendExperimentCommand("采集Buff", BuildLoopFeatureLua("gather_buff"));
            grpGM.Controls.Add(btnGatherBuff);
            btnYyAutoBuff = new Button { Text = "自动Buff", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnYyAutoBuff.Click += (s, e) => SendExperimentCommand("自动Buff", BuildLoopFeatureLua("auto_buff"));
            grpGM.Controls.Add(btnYyAutoBuff);

            gy += rowStep;
            btnCombatSkill = new Button { Text = "战斗技巧", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnCombatSkill.Click += (s, e) => SendExperimentCommand("战斗技巧Buff", BuildLoopFeatureLua("combat_skill"));
            grpGM.Controls.Add(btnCombatSkill);
            btnScrollBuff = new Button { Text = "卷轴心法", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnScrollBuff.Click += (s, e) => SendExperimentCommand("卷轴心法", BuildLoopFeatureLua("scroll_buff"));
            grpGM.Controls.Add(btnScrollBuff);

            gy += rowStep;
            btnAttrBuff = new Button { Text = "属性Buff", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnAttrBuff.Click += (s, e) => SendExperimentCommand("属性Buff", BuildLoopFeatureLua("attr_buff"));
            grpGM.Controls.Add(btnAttrBuff);
            btnAuxBuff = new Button { Text = "辅助Buff", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnAuxBuff.Click += (s, e) => SendExperimentCommand("辅助Buff", BuildLoopFeatureLua("aux_buff"));
            grpGM.Controls.Add(btnAuxBuff);

            gy += rowStep;
            btnUnknownBuff = new Button { Text = "未知Buff", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnUnknownBuff.Click += (s, e) => SendExperimentCommand("未知Buff", BuildLoopFeatureLua("unknown_buff"));
            grpGM.Controls.Add(btnUnknownBuff);
            var btnRemoveAllBuffs = new Button { Text = "移除全部Buff", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnRemoveAllBuffs.Click += (s, e) => SendExperimentCommand("移除全部Buff", BuildLoopFeatureLua("remove_all_buffs"));
            grpGM.Controls.Add(btnRemoveAllBuffs);
            btnDisableLogs = new Button { Text = "禁用日志/检测", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnDisableLogs.Click += (s, e) => SendExperimentCommand("禁用日志/检测", BuildYyLaoLiuLua("yy_disablelogs"));
            grpGM.Controls.Add(btnDisableLogs);
            var btnYyRemoveBuff = new Button { Text = "老六移除Buff", Location = new Point(400, gy), Size = new Size(125, 30) };
            btnYyRemoveBuff.Click += (s, e) => SendExperimentCommand("老六移除Buff", BuildYyLaoLiuBuffToolLua("yy_remove_buffs"));
            grpGM.Controls.Add(btnYyRemoveBuff);

            addSection("循环功能 (再点停止)");
            btnLoopBuff = new Button { Text = "循环强力Buff", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnLoopBuff.Click += (s, e) => SendExperimentCommand("循环强力Buff", BuildLoopFeatureLua("loop_buff"));
            grpGM.Controls.Add(btnLoopBuff);
            btnLoopDefense = new Button { Text = "循环防御Buff", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnLoopDefense.Click += (s, e) => SendExperimentCommand("循环防御Buff", BuildLoopFeatureLua("loop_defense"));
            grpGM.Controls.Add(btnLoopDefense);
            btnLoopLoot = new Button { Text = "循环自动拾取", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnLoopLoot.Click += (s, e) => SendExperimentCommand("循环自动拾取", BuildLoopFeatureLua("loop_loot"));
            grpGM.Controls.Add(btnLoopLoot);
            btnLoopRecover = new Button { Text = "循环自动恢复", Location = new Point(400, gy), Size = new Size(125, 30) };
            btnLoopRecover.Click += (s, e) => SendExperimentCommand("循环自动恢复", BuildLoopFeatureLua("loop_recover"));
            grpGM.Controls.Add(btnLoopRecover);

            addSection("游戏辅助");
            btnYyAutoLoot = new Button { Text = "自动拾取", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnYyAutoLoot.Click += (s, e) => SendExperimentCommand("自动拾取", BuildYyLaoLiuLua("yy_autoloot"));
            grpGM.Controls.Add(btnYyAutoLoot);
            btnAutoLootPlus = new Button { Text = "增强拾取", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnAutoLootPlus.Click += (s, e) => SendExperimentCommand("增强拾取", BuildGameFeatureLua("auto_loot_plus"));
            grpGM.Controls.Add(btnAutoLootPlus);
            btnAutoChest = new Button { Text = "自动开箱收集", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnAutoChest.Click += (s, e) => SendExperimentCommand("自动开箱收集", BuildGameFeatureLua("auto_chest"));
            grpGM.Controls.Add(btnAutoChest);
            btnYyRecover = new Button { Text = "一键恢复", Location = new Point(400, gy), Size = new Size(125, 30) };
            btnYyRecover.Click += (s, e) => SendExperimentCommand("一键恢复", BuildYyLaoLiuLua("yy_recover"));
            grpGM.Controls.Add(btnYyRecover);

            gy += rowStep;
            btnRhythmGame = new Button { Text = "NPC节奏游戏", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnRhythmGame.Click += (s, e) => SendExperimentCommand("NPC节奏游戏", BuildGameFeatureLua("rhythm_game"));
            grpGM.Controls.Add(btnRhythmGame);
            btnChessWin = new Button { Text = "象棋秒赢", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnChessWin.Click += (s, e) => SendExperimentCommand("象棋秒赢", BuildGameFeatureLua("chess_win"));
            grpGM.Controls.Add(btnChessWin);
            btnPitchPot = new Button { Text = "投壶圈变大", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnPitchPot.Click += (s, e) => SendExperimentCommand("投壶圈变大", BuildGameFeatureLua("pitch_pot_easy"));
            grpGM.Controls.Add(btnPitchPot);

            gy += rowStep;
            btnGmTrainPanel = new Button { Text = "GM训练面板", Location = new Point(10, gy), Size = new Size(125, 30) };
            btnGmTrainPanel.Click += (s, e) => SendExperimentCommand("GM训练面板", BuildGameFeatureLua("gm_train_panel"));
            grpGM.Controls.Add(btnGmTrainPanel);
            btnTestLuaMenu = new Button { Text = "打开Test菜单", Location = new Point(140, gy), Size = new Size(125, 30) };
            btnTestLuaMenu.Click += (s, e) => SendExperimentCommand("打开Test菜单", BuildTestLuaMenuLua());
            grpGM.Controls.Add(btnTestLuaMenu);
            btnYyLoadScript = new Button { Text = "加载老六脚本", Location = new Point(270, gy), Size = new Size(125, 30) };
            btnYyLoadScript.Click += (s, e) => SendExperimentCommand("加载老六脚本", BuildYyLaoLiuLua("yy_load_script"));
            grpGM.Controls.Add(btnYyLoadScript);
            btnCnMenuLoad = new Button { Text = "加载国服菜单", Location = new Point(400, gy), Size = new Size(125, 30) };
            btnCnMenuLoad.Click += (s, e) => SendExperimentCommand("加载国服菜单", BuildChinaMenuLua("cn_menu_load"));
            grpGM.Controls.Add(btnCnMenuLoad);

            gy += rowStep;
            grpGM.Controls.Add(new Label { Text = "衣服换皮", Location = new Point(10, gy + 6), Size = new Size(80, 20) });
            cmbOutfit = new ComboBox { Location = new Point(95, gy + 3), Size = new Size(185, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            FillOutfitCombo();
            grpGM.Controls.Add(cmbOutfit);
            btnApplyOutfit = new Button { Text = "应用换皮", Location = new Point(290, gy), Size = new Size(105, 30) };
            btnApplyOutfit.Click += (s, e) => ApplyOutfitSelection();
            grpGM.Controls.Add(btnApplyOutfit);
            btnOpenOutfitPicker = new Button { Text = "换皮菜单", Location = new Point(405, gy), Size = new Size(105, 30) };
            btnOpenOutfitPicker.Click += (s, e) => SendExperimentCommand("打开换皮菜单", BuildOutfitPickerLua());
            grpGM.Controls.Add(btnOpenOutfitPicker);

            addSection("速度 / 倍率");
            grpGM.Controls.Add(new Label { Text = "攻击倍率", Location = new Point(10, gy + 6), Size = new Size(80, 20) });
            cmbAtkMul = new ComboBox { Location = new Point(95, gy + 3), Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAtkMul.Items.AddRange(new object[] { "x2", "x4", "x8" });
            cmbAtkMul.SelectedIndex = 0;
            grpGM.Controls.Add(cmbAtkMul);
            btnApplyAtkMul = new Button { Text = "应用倍率", Location = new Point(230, gy), Size = new Size(115, 30) };
            btnApplyAtkMul.Click += (s, e) => ApplyAttackMultiplierSelection();
            grpGM.Controls.Add(btnApplyAtkMul);
            btnResetAtkMul = new Button { Text = "还原倍率", Location = new Point(360, gy), Size = new Size(115, 30) };
            btnResetAtkMul.Click += (s, e) => SendExperimentCommand("还原攻击倍率", BuildLoopFeatureLua("atk_mul_reset"));
            grpGM.Controls.Add(btnResetAtkMul);

            gy += rowStep;
            grpGM.Controls.Add(new Label { Text = "剧情速度", Location = new Point(10, gy + 6), Size = new Size(80, 20) });
            cmbDialogSpeed = new ComboBox { Location = new Point(95, gy + 3), Size = new Size(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDialogSpeed.Items.AddRange(new object[] { "x20", "x80" });
            cmbDialogSpeed.SelectedIndex = 0;
            grpGM.Controls.Add(cmbDialogSpeed);
            btnApplyDialogSpeed = new Button { Text = "应用速度", Location = new Point(230, gy), Size = new Size(115, 30) };
            btnApplyDialogSpeed.Click += (s, e) => ApplyDialogSpeedSelection();
            grpGM.Controls.Add(btnApplyDialogSpeed);
            btnResetDialogSpeed = new Button { Text = "还原剧情速度", Location = new Point(360, gy), Size = new Size(115, 30) };
            btnResetDialogSpeed.Click += (s, e) => SendExperimentCommand("还原剧情速度", BuildLoopFeatureLua("dialog_speed_reset"));
            grpGM.Controls.Add(btnResetDialogSpeed);

            grpGM.Controls.Clear();
            grpGM.Controls.Add(new Label { Text = "GM 功能", Location = new Point(10, 5), Size = new Size(120, 18), Font = new Font("Microsoft YaHei UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(45, 55, 72) });
            tabGM = new Panel { Location = new Point(10, 28), Size = new Size(540, 250), BackColor = Color.White };
            var tabNav = new Panel { Location = new Point(0, 0), Size = new Size(540, 38), BackColor = Color.White };
            var btnTabBattle = new Button { Text = "战斗增强", Location = new Point(0, 0), Size = new Size(126, 32), Tag = "nav" };
            var btnTabBuff = new Button { Text = "Buff / 循环", Location = new Point(134, 0), Size = new Size(126, 32), Tag = "nav" };
            var btnTabAssist = new Button { Text = "游戏辅助", Location = new Point(268, 0), Size = new Size(126, 32), Tag = "nav" };
            var btnTabTools = new Button { Text = "换皮 / 速度", Location = new Point(402, 0), Size = new Size(126, 32), Tag = "nav" };
            tabNav.Controls.Add(btnTabBattle);
            tabNav.Controls.Add(btnTabBuff);
            tabNav.Controls.Add(btnTabAssist);
            tabNav.Controls.Add(btnTabTools);
            tabGM.Controls.Add(tabNav);

            var tabBattle = new Panel { Location = new Point(0, 42), Size = new Size(540, 208), AutoScroll = true, BackColor = Color.White };
            var tabBuff = new Panel { Location = new Point(0, 42), Size = new Size(540, 208), AutoScroll = true, BackColor = Color.White };
            var tabAssist = new Panel { Location = new Point(0, 42), Size = new Size(540, 208), AutoScroll = true, BackColor = Color.White };
            var tabTools = new Panel { Location = new Point(0, 42), Size = new Size(540, 208), AutoScroll = true, BackColor = Color.White };
            tabGM.Controls.Add(tabBattle);
            tabGM.Controls.Add(tabBuff);
            tabGM.Controls.Add(tabAssist);
            tabGM.Controls.Add(tabTools);

            Button[] tabButtons = new Button[] { btnTabBattle, btnTabBuff, btnTabAssist, btnTabTools };
            Panel[] tabPages = new Panel[] { tabBattle, tabBuff, tabAssist, tabTools };
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
            btnTabBattle.Click += (s, e) => activateTab(tabBattle, btnTabBattle);
            btnTabBuff.Click += (s, e) => activateTab(tabBuff, btnTabBuff);
            btnTabAssist.Click += (s, e) => activateTab(tabAssist, btnTabAssist);
            btnTabTools.Click += (s, e) => activateTab(tabTools, btnTabTools);

            int[] yBattle = new int[] { 14 };
            int[] yBuff = new int[] { 14 };
            int[] yAssist = new int[] { 14 };
            int[] yTools = new int[] { 14 };
            Func<int, int, Point> pos = (col, yy) => new Point(10 + col * 130, yy);
            Action<Control, string, int[]> addTabSection = (parent, title, yref) => {
                if (parent.Controls.Count > 0) yref[0] += rowStep + sectionGap;
                parent.Controls.Add(new Label { Text = "── " + title + " ──", Location = new Point(10, yref[0]), Size = new Size(500, 18), ForeColor = Color.FromArgb(80, 80, 80), Font = new Font("Microsoft YaHei", 8, FontStyle.Bold) });
                yref[0] += 24;
            };
            Action<Control, Control, int, int[]> place = (parent, control, col, yref) => {
                control.Location = pos(col, yref[0]);
                parent.Controls.Add(control);
            };

            addTabSection(tabBattle, "战斗增强", yBattle);
            place(tabBattle, btnGod, 0, yBattle);
            place(tabBattle, btnStamina, 1, yBattle);
            place(tabBattle, btnStaminaDive, 2, yBattle);
            place(tabBattle, btnInvis, 3, yBattle);
            yBattle[0] += rowStep;
            place(tabBattle, btnCancelInvis, 0, yBattle);
            place(tabBattle, btnStaminaEmpty, 1, yBattle);
            place(tabBattle, btnStaminaResetAll, 2, yBattle);
            yBattle[0] += rowStep;
            place(tabBattle, btnYyNpcDumb, 0, yBattle);
            place(tabBattle, btnYySuperDodge, 1, yBattle);

            addTabSection(tabBuff, "Buff 施加 (一次性)", yBuff);
            place(tabBuff, btnAtkBuff, 0, yBuff);
            place(tabBuff, btnDefBuff, 1, yBuff);
            place(tabBuff, btnGatherBuff, 2, yBuff);
            place(tabBuff, btnYyAutoBuff, 3, yBuff);
            yBuff[0] += rowStep;
            place(tabBuff, btnCombatSkill, 0, yBuff);
            place(tabBuff, btnScrollBuff, 1, yBuff);
            place(tabBuff, btnAttrBuff, 2, yBuff);
            place(tabBuff, btnAuxBuff, 3, yBuff);
            yBuff[0] += rowStep;
            place(tabBuff, btnUnknownBuff, 0, yBuff);
            place(tabBuff, btnRemoveAllBuffs, 1, yBuff);
            place(tabBuff, btnDisableLogs, 2, yBuff);
            place(tabBuff, btnYyRemoveBuff, 3, yBuff);

            addTabSection(tabBuff, "循环功能 (再点停止)", yBuff);
            place(tabBuff, btnLoopBuff, 0, yBuff);
            place(tabBuff, btnLoopDefense, 1, yBuff);
            place(tabBuff, btnLoopLoot, 2, yBuff);
            place(tabBuff, btnLoopRecover, 3, yBuff);

            addTabSection(tabAssist, "游戏辅助", yAssist);
            place(tabAssist, btnYyAutoLoot, 0, yAssist);
            place(tabAssist, btnAutoLootPlus, 1, yAssist);
            place(tabAssist, btnAutoChest, 2, yAssist);
            place(tabAssist, btnYyRecover, 3, yAssist);
            yAssist[0] += rowStep;
            place(tabAssist, btnRhythmGame, 0, yAssist);
            place(tabAssist, btnChessWin, 1, yAssist);
            place(tabAssist, btnPitchPot, 2, yAssist);
            yAssist[0] += rowStep;
            place(tabAssist, btnGmTrainPanel, 0, yAssist);
            place(tabAssist, btnTestLuaMenu, 1, yAssist);
            place(tabAssist, btnYyLoadScript, 2, yAssist);
            place(tabAssist, btnCnMenuLoad, 3, yAssist);

            addTabSection(tabTools, "衣服换皮", yTools);
            tabTools.Controls.Add(new Label { Text = "衣服换皮", Location = new Point(10, yTools[0] + 6), Size = new Size(80, 20) });
            cmbOutfit.Location = new Point(95, yTools[0] + 3);
            cmbOutfit.Size = new Size(185, 24);
            tabTools.Controls.Add(cmbOutfit);
            btnApplyOutfit.Location = new Point(290, yTools[0]);
            tabTools.Controls.Add(btnApplyOutfit);
            btnOpenOutfitPicker.Location = new Point(405, yTools[0]);
            tabTools.Controls.Add(btnOpenOutfitPicker);

            addTabSection(tabTools, "速度 / 倍率", yTools);
            tabTools.Controls.Add(new Label { Text = "攻击倍率", Location = new Point(10, yTools[0] + 6), Size = new Size(80, 20) });
            cmbAtkMul.Location = new Point(95, yTools[0] + 3);
            tabTools.Controls.Add(cmbAtkMul);
            btnApplyAtkMul.Location = new Point(230, yTools[0]);
            tabTools.Controls.Add(btnApplyAtkMul);
            btnResetAtkMul.Location = new Point(360, yTools[0]);
            tabTools.Controls.Add(btnResetAtkMul);
            yTools[0] += rowStep;
            tabTools.Controls.Add(new Label { Text = "剧情速度", Location = new Point(10, yTools[0] + 6), Size = new Size(80, 20) });
            cmbDialogSpeed.Location = new Point(95, yTools[0] + 3);
            tabTools.Controls.Add(cmbDialogSpeed);
            btnApplyDialogSpeed.Location = new Point(230, yTools[0]);
            tabTools.Controls.Add(btnApplyDialogSpeed);
            btnResetDialogSpeed.Location = new Point(360, yTools[0]);
            tabTools.Controls.Add(btnResetDialogSpeed);

            grpGM.Controls.Add(tabGM);
            Controls.Add(grpGM);
            y += 298;
            ClientSize = new Size(580, grpGM.Bottom + 10);
            UpdateCommPaths();
            EnsureBuffConfigFile();
            LoadConfig();
            ApplyCleanStyle();
            activateTab(tabBattle, btnTabBattle);
            SetGMEnabled(false);
            CheckState();
        }

        void BtnBrowse_Click(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog { Description = "选择游戏主目录（包含 yysls_fast 文件夹的目录）" };
            if (!string.IsNullOrEmpty(gameRootPath) && Directory.Exists(gameRootPath)) dlg.SelectedPath = gameRootPath;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string binPath = Path.Combine(dlg.SelectedPath, GameSubPath);
                string exePath = Path.Combine(binPath, GameExeName);
                if (File.Exists(exePath))
                {
                    gameRootPath = dlg.SelectedPath;
                    gameBinPath = binPath;
                    UpdatePathLabel();
                    UpdateCommPaths();
                    SaveConfig();
                    UpdateStatus();
                }
                else MessageBox.Show("未找到游戏客户端:\n" + exePath, "路径错误");
            }
        }

        void UpdateCommPaths()
        {
            string commRoot = !string.IsNullOrEmpty(gameBinPath) ? gameBinPath : ToolDir;
            CmdFile = Path.Combine(commRoot, "gm_cmd.txt");
            CmdResultFile = Path.Combine(commRoot, "gm_cmd_result.txt");
            ToolResultFile = Path.Combine(commRoot, "gm_tool_result.txt");
            ToolResultCompatFile = Path.Combine(ToolDir, "gm_tool_result.txt");
            UnifiedLogFile = Path.Combine(commRoot, "gm_tool.log");
            FridaLogFile = UnifiedLogFile;
            HackLogFile = UnifiedLogFile;
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

        string BuildFridaGadgetConfig()
        {
            return "{\r\n  \"interaction\": {\r\n    \"type\": \"script\",\r\n    \"path\": \"gm_payload.js\"\r\n  }\r\n}\r\n";
        }

        void WriteAutoLoadFilesToGameDir()
        {
            string gadgetSource = Path.Combine(ToolDir, "frida-gadget.dll");
            if (!File.Exists(gadgetSource)) gadgetSource = ResolvePortableToolPath("frida-gadget.dll", PortableToolFallbackDir, OriginalFridaGadget);
            if (!File.Exists(gadgetSource)) throw new FileNotFoundException("frida-gadget.dll 未找到", gadgetSource);

            string payloadSource = Path.Combine(ToolDir, "gm_payload.js");
            if (!File.Exists(payloadSource)) payloadSource = PayloadJs;
            if (!File.Exists(payloadSource)) throw new FileNotFoundException("gm_payload.js 未找到", payloadSource);
            string payloadText = File.ReadAllText(payloadSource, Encoding.UTF8);

            string targetPayloadPath = Path.Combine(gameBinPath, "gm_payload.js");
            string configPath = Path.Combine(gameBinPath, "frida-gadget.config");
            string gadgetTarget = Path.Combine(gameBinPath, "frida-gadget.dll");

            File.Copy(gadgetSource, gadgetTarget, true);
            File.WriteAllText(targetPayloadPath, payloadText, new UTF8Encoding(false));
            File.WriteAllText(configPath, BuildFridaGadgetConfig(), new UTF8Encoding(false));
        }

        void UpdatePathLabel()
        {
            if (lblGamePath == null) return;
            if (string.IsNullOrEmpty(gameRootPath))
            {
                lblGamePath.Text = "未选择目录";
                return;
            }
            string name = Path.GetFileName(gameRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name)) name = gameRootPath;
            lblGamePath.Text = "当前: " + name;
        }

        void BtnCopyFiles_Click_B(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(gameBinPath)) { MessageBox.Show("请先选择游戏目录！"); return; }
            try
            {
                AppendLog("复制方案B文件到游戏目录...");
                string dinput8Source = Path.Combine(ToolDir, "dinput8.dll");
                if (!File.Exists(dinput8Source)) dinput8Source = OriginalDinput8;
                File.Copy(dinput8Source, Path.Combine(gameBinPath, "dinput8.dll"), true);
                AppendLog("  dinput8.dll 已复制");
                WriteAutoLoadFilesToGameDir();
                AppendLog("  frida-gadget.dll 已复制");
                AppendLog("  gm_payload.js 已写入");
                AppendLog("  frida-gadget.config 已写入");
                UpdateCommPaths();
                try { if (File.Exists(CmdFile)) File.Delete(CmdFile); } catch { }
                try { if (File.Exists(CmdResultFile)) File.Delete(CmdResultFile); } catch { }
                try { if (File.Exists(ToolResultFile)) File.Delete(ToolResultFile); } catch { }
                try { if (File.Exists(UnifiedLogFile)) File.Delete(UnifiedLogFile); } catch { }
                AppendLog("复制完成！方案B自动加载文件已提前写入游戏目录。");
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("复制失败: " + ex.Message);
                AppendLog("ERROR: " + ex.Message);
            }
        }

        void BtnClearFiles_Click_B(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(gameBinPath)) { MessageBox.Show("请先选择游戏目录！"); return; }
            try
            {
                AppendLog("清理复制到游戏目录的文件...");
                int removed = 0;
                foreach (string name in new[] { "dinput8.dll", "frida-gadget.dll", "gm_payload.js", "frida-gadget.config", "gm_cmd.txt", "frida_ready.txt", "frida_log.txt", "gm_cmd_result.txt", "hack_log.txt", "gm_tool_result.txt", "gm_tool.log", "connector_log.txt", "gm_tool_ui.log", "gm_tool_all.log" })
                {
                    string path = Path.Combine(gameBinPath, name);
                    try
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            removed++;
                            AppendLog("  已删除: " + path);
                        }
                    }
                    catch { }
                }

                ClearManagedFiles();
                UpdateCommPaths();
                isReady = false;
                AppendLog("清理完成，共删除 " + removed + " 个游戏目录文件；工具目录临时文件已清理。");
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("清理失败: " + ex.Message);
                AppendLog("ERROR: " + ex.Message);
            }
        }

        void BtnInjectFrida_Click_B(object sender, EventArgs e)
        {
            string injectorPath = ResolvePortableToolPath(PortableInjectorName, PortableToolFallbackDir, OriginalInjector);
            string gadgetPath = Path.Combine(gameBinPath, "frida-gadget.dll");
            string payloadPath = Path.Combine(gameBinPath, "gm_payload.js");
            string gadgetConfigPath = Path.Combine(gameBinPath, "frida-gadget.config");
            if (!File.Exists(injectorPath)) { MessageBox.Show("注入器不存在:\n" + injectorPath); return; }
            if (!File.Exists(gadgetPath)) { MessageBox.Show("frida-gadget.dll 不存在:\n" + gadgetPath); return; }
            if (!File.Exists(payloadPath) || !File.Exists(gadgetConfigPath))
            {
                MessageBox.Show("方案B文件不完整，请先点\"复制文件\"。\n\n缺少文件目录:\n" + gameBinPath);
                return;
            }
            var procs = Process.GetProcessesByName("yysls");
            if (procs.Length == 0) { MessageBox.Show("游戏未运行！请先启动游戏。"); return; }

            try
            {
                AppendLog("运行注入器: " + injectorPath);
                AppendLog("方案B请注入游戏目录中的: " + gadgetPath);
                AppendLog("注入前请确保游戏目录内已存在 dinput8.dll、frida-gadget.dll、gm_payload.js、frida-gadget.config。");
                Process.Start(new ProcessStartInfo
                {
                    FileName = injectorPath,
                    WorkingDirectory = Path.GetDirectoryName(injectorPath),
                    UseShellExecute = true,
                    Verb = "runas"
                });
                AppendLog("注入器已启动！注入后请查看游戏目录 gm_tool.log 是否出现 Ready 和 CAPTURED L。");
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("注入器启动失败: " + ex.Message);
                AppendLog("ERROR: " + ex.Message);
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
# 移除Buff按钮
REMOVE = {108010, 380013, 70063, 70141}
#
# 攻击Buff按钮
ATTACK = {102405, 102406, 102455, 102456, 109003, 109009, 109021, 109901, 109903, 109905, 109908, 109909, 109910, 109911, 109912, 109926, 102423, 102425, 109501, 109503, 109505, 109507, 109509, 109511, 70003, 70004, 109512}
#
# 防御Buff按钮
DEFENSE = {30372, 30310, 70184, 200071, 200059, 200083, 200099, 200086, 30366, 30303, 30333, 30334, 30376, 30379, 30406, 102707, 70005, 102408, 102458, 102703, 102704, 200031}
#
# 循环强力Buff按钮
LOOP_STRONG = {1053027, 1053026, 109927, 200005, 70141, 70063, 109506, 30302, 30314, 107031, 1053070, 102404, 102454, 109914, 109917, 109923, 109515, 10532, 200035, 200036, 102400, 102401, 102402, 102450, 102451, 102452, 109014, 109015, 109016, 109920, 109921, 109922, 102407, 102457, 102408, 102458}
#
# 采集/拾取相关Buff
GATHER = {104002, 104003, 104021, 104027, 104004, 104005, 104006, 104007, 104015, 104016, 104033, 104039, 104045, 104051, 104013, 104014, 104017, 104022, 104023, 104024, 104028, 104029, 104030, 104034, 104035, 104040, 104041, 104046, 104047, 104018, 104019, 104020, 104025, 104026, 104031, 104032, 104036, 104037, 104038, 104042, 104043, 104044, 104048, 104049, 104050}
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
            SendExperimentCommand(label, BuildLoopFeatureLua(mode));
        }

        void ApplyDialogSpeedSelection()
        {
            string selected = cmbDialogSpeed != null && cmbDialogSpeed.SelectedItem != null ? cmbDialogSpeed.SelectedItem.ToString() : "x20";
            string mode = selected == "x80" ? "dialog_speed_80" : "dialog_speed_20";
            string label = selected == "x80" ? "剧情速度x80" : "剧情速度x20";
            SendExperimentCommand(label, BuildLoopFeatureLua(mode));
        }

        void FillOutfitCombo()
        {
            if (cmbOutfit == null) return;
            cmbOutfit.Items.Clear();
            for (int i = 0; i < OutfitIds.Length && i < OutfitNames.Length; i++)
            {
                cmbOutfit.Items.Add(OutfitIds[i].ToString() + " - " + OutfitNames[i]);
            }
            if (cmbOutfit.Items.Count > 0) cmbOutfit.SelectedIndex = 0;
        }

        void ApplyOutfitSelection()
        {
            int index = cmbOutfit != null ? cmbOutfit.SelectedIndex : -1;
            if (index < 0 || index >= OutfitIds.Length || index >= OutfitNames.Length)
            {
                MessageBox.Show("请先选择换皮选项");
                return;
            }
            string label = "衣服换皮 < " + OutfitNames[index] + " >";
            SendExperimentCommand(label, BuildOutfitApplyLua(OutfitIds[index], OutfitNames[index]));
        }

        string ResolvePortableToolPath(string fileName, string fallbackDir, string legacyPath)
        {
            string portablePath = Path.Combine(Application.StartupPath, fileName);
            if (File.Exists(portablePath)) return portablePath;
            string fallbackPath = Path.Combine(fallbackDir, fileName);
            if (File.Exists(fallbackPath)) return fallbackPath;
            if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath)) return legacyPath;
            return portablePath;
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
                AppendLog("游戏已启动！到角色选择界面后点[2.注入Frida]");
                UpdateStatus();
            }
            catch (Exception ex) { MessageBox.Show("启动失败: " + ex.Message); AppendLog("ERROR: " + ex.Message); }
        }

        void SendCommand(string luaCode)
        {
            if (!isReady) { MessageBox.Show("请先等待就绪！"); return; }
            try
            {
                File.WriteAllText(CmdFile, luaCode);
                AppendLog("发送: " + luaCode.Substring(0, Math.Min(60, luaCode.Length)) + (luaCode.Length > 60 ? "..." : ""));
            }
            catch (Exception ex) { MessageBox.Show("发送失败: " + ex.Message); }
        }

        void SendExperimentCommand(string name, string luaCode)
        {
            if (!isReady) { MessageBox.Show("请先等待 Lua 就绪"); return; }
            if (commandPending) { AppendLog("上一条命令还在执行，请稍等: " + name); return; }
            commandPending = true;
            try { if (File.Exists(ToolResultFile)) File.Delete(ToolResultFile); } catch { }
            try { if (File.Exists(ToolResultCompatFile)) File.Delete(ToolResultCompatFile); } catch { }
            SendCommand(luaCode);
            AppendLog("实验功能已发送: " + name);
            var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            int ticks = 0;
            timer.Tick += (s, e) => {
                ticks++;
                ReadCommandResult(name);
                if (ticks >= 5 || File.Exists(ToolResultFile) || File.Exists(ToolResultCompatFile))
                {
                    commandPending = false;
                    timer.Stop();
                }
            };
            timer.Start();
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
                string resultPath = File.Exists(ToolResultFile) ? ToolResultFile : (File.Exists(ToolResultCompatFile) ? ToolResultCompatFile : "");
                if (!string.IsNullOrEmpty(resultPath))
                {
                    var lines = File.ReadAllLines(resultPath);
                    int start = Math.Max(0, lines.Length - 16);
                    AppendLog(name + " 返回结果:");
                    for (int i = start; i < lines.Length; i++) AppendLog("  " + lines[i]);
                }
            }
            catch (Exception ex) { AppendLog(name + " 读取结果失败: " + ex.Message); }
        }

        string LuaString(string text) { return "[[" + text.Replace("]]", "] ]") + "]]"; }

        string BuildLuaEnvelope(string action, string body)
        {
            string resultPath = ToolResultFile.Replace("\\", "/");
            string compatResultPath = ToolResultCompatFile.Replace("\\", "/");
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
  local __ok2, __err2 = __write_result(" + LuaString(compatResultPath) + @")
  if not __ok2 then
    print('[GM_Tool] result write failed', __err, __err2)
  end
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

        string BuildChinaMenuLua(string mode)
        {
            string body = @"
local path = 'E:/ceshi/7/Where Winds Meet/Scripts/Test.lua'
local function toggle_cn_menu()
  if type(_G.ToggleGMMenu) == 'function' then return _G.ToggleGMMenu() end
  if _G.GM_MENU and type(_G.GM_MENU.isVisible) == 'function' and type(_G.GM_MENU.setVisible) == 'function' then
    return _G.GM_MENU:setVisible(not _G.GM_MENU:isVisible())
  end
  error('国服菜单未加载或缺少 ToggleGMMenu/GM_MENU')
end
local function load_cn_menu()
  if _G.GM_MENU then
    __add('cn_menu_load', 'already_loaded', _G.GM_MENU)
    return toggle_cn_menu()
  end
  __try('cn_menu_loadfile', function()
    local f, err = loadfile(path)
    if not f then error('loadfile failed: ' .. tostring(err)) end
    local ok, runErr = pcall(f)
    if not ok then error('pcall failed: ' .. tostring(runErr)) end
    __add('cn_menu_loaded', type(_G.GM_MENU), _G.GM_MENU)
  end)
end
__add('cn_menu_path', path)
";
            if (mode == "cn_menu_load") body += "\nload_cn_menu()\n";
            return BuildLuaEnvelope("china_menu_" + mode, body);
        }

        string BuildTestLuaMenuLua()
        {
            string path = TestLuaMenuScript.Replace("\\", "/");
            string body = @"
local path = '" + path + @"'
__add('test_menu_path', path)
__try('test_menu_loadfile', function()
  local f, err = loadfile(path)
  if not f then error('loadfile failed: ' .. tostring(err)) end
  local ok, runErr = pcall(f)
  if not ok then error('pcall failed: ' .. tostring(runErr)) end
  __add('test_menu_loaded', type(_G.GM_MENU), _G.GM_MENU)
end)
";
            return BuildLuaEnvelope("test_lua_menu_open", body);
        }

        // NOTE: BuildLoopFeatureLua, BuildGameFeatureLua, BuildOutfitListLua, BuildOutfitApplyLua,
        // BuildOutfitPickerLua, BuildYyLaoLiuBuffToolLua, BuildYyLaoLiuLua, BuildCombatExperimentLua
        // are identical to the original - kept as-is below.

        string BuildLoopFeatureLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
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
  local ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
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
local function apply_strong_buffs()
  for _, id in ipairs(read_named_buff_list('LOOP_STRONG', {1053027,1053026,109927,200005,70141,70063,109506,30302,30314,107031,1053070,102404,102454,109914,109917,109923,109515,10532,200035,200036,102400,102401,102402,102450,102451,102452,109014,109015,109016,109920,109921,109922,102407,102457,102408,102458})) do
    __try('loop.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_defense_buffs()
  for _, id in ipairs(read_named_buff_list('DEFENSE', {30372,30310,70184,200071,200059,200083,200099,200086,30366,30303,30333,30334,30376,30379,30406,102707,70005,102408,102458,200031})) do
    __try('loop.def_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_auto_buffs()
  for _, id in ipairs(read_named_buff_list('AUTO', {30302,30314,107031,1053070,102400,102401,102402,102404,102450,102451,102452,109014,109015,109016,109920,109921,109922,109923})) do
    __try('auto.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_gather_buffs()
  for _, id in ipairs(read_named_buff_list('GATHER', {104002,104003,104021,104027,104004,104005,104006,104007,104015,104016,104033,104039,104045,104051,104013,104014,104017,104022,104023,104024,104028,104029,104030,104034,104035,104040,104041,104046,104047,104018,104019,104020,104025,104026,104031,104032,104036,104037,104038,104042,104043,104044,104048,104049,104050})) do
    __try('gather.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_aux_buffs()
  for _, id in ipairs(read_named_buff_list('AUX', {30005,70110,70025,102701,102702,102703,102704,109602,109603})) do
    __try('aux.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_unknown_buffs()
  for _, id in ipairs(read_named_buff_list('UNKNOWN', {200102,107201,107202,107203,107204,70182,70183,70186,70187,200095,1053027,109927,200005,109506,70141,70063})) do
    __try('unknown.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_combat_skill_buffs()
  for _, id in ipairs(read_named_buff_list('COMBAT_SKILL', {102502,102503,102505,102508,102705,102706,103007,103008,102605,102606,102607})) do
    __try('combat_skill.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_scroll_buffs()
  for _, id in ipairs(read_named_buff_list('SCROLL', {109604,109605,109606,109607,109608,109609})) do
    __try('scroll.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function apply_attr_buffs()
  for _, id in ipairs(read_named_buff_list('ATTR', {104009,104010,104011,104012,104001,104004})) do
    __try('attr.add_buff_' .. tostring(id), function() return add_buff(id) end)
  end
end
local function remove_named_buffs(tag, label, fallback)
  local removed = 0
  for _, id in ipairs(read_named_buff_list(label, fallback)) do
    if remove_buff_repeat(tag, id, 6) > 0 then removed = removed + 1 end
  end
  __add(tag, 'removed', removed)
end
local function run_loot_once()
  if type(_G.RunAutoLoot) == 'function' then return _G.RunAutoLoot() end
  local mp = player()
  if not mp then error('main_player missing') end
  __try('loot.collect_nearby_collections', function() if mp.ride_skill_collect_nearby_collections then return mp:ride_skill_collect_nearby_collections(5000) else error('missing') end end)
  __try('loot.kill_reward', function() if mp.ride_skill_find_nearest_kill_reward then local r = mp:ride_skill_find_nearest_kill_reward(5000); if r and mp.ride_skill_get_kill_reward then return mp:ride_skill_get_kill_reward(r) end else error('missing') end end)
end
local function run_recover_once()
  __try('recover.buff_70141', function() return add_buff(70141) end)
  __try('recover.buff_70063', function() return add_buff(70063) end)
  __try('recover.buff_102410', function() return add_buff(102410) end)
  __try('recover.buff_102460', function() return add_buff(102460) end)
end
local function stop_loop(name)
  local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene() or nil
  local key = '__GM_TOOL_' .. name .. '_ACTION'
  if scene and _G[key] then scene:stopAction(_G[key]) end
  _G[key] = nil
end
local function disable_loop(name)
  stop_loop(name)
  _G['__GM_TOOL_' .. name .. '_ACTIVE'] = false
  _G['__GM_TOOL_' .. name .. '_GEN'] = (_G['__GM_TOOL_' .. name .. '_GEN'] or 0) + 1
  __add(name .. '_loop', 'disabled')
end
local function toggle_loop(name, delaySec, func)
  local activeKey = '__GM_TOOL_' .. name .. '_ACTIVE'
  local actionKey = '__GM_TOOL_' .. name .. '_ACTION'
  local genKey = '__GM_TOOL_' .. name .. '_GEN'
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
local function remove_all_buff_sets()
  disable_loop('LOOP_BUFF')
  disable_loop('LOOP_DEFENSE')
  clear_all_local_buffs('remove_all.preclear')
  local entries = {
    { 'AUTO', {30302,30314,107031,1053070,102400,102401,102402,102404,102450,102451,102452,109014,109015,109016,109920,109921,109922,109923} },
    { 'REMOVE', {108010,380013,70063,70141} },
    { 'ATTACK', {102405,102406,102455,102456,109003,109009,109021,109901,109903,109905,109908,109909,109910,109911,109912,109926,102423,102425,109501,109503,109505,109507,109509,109511,70003,70004,109512} },
    { 'DEFENSE', {30372,30310,70184,200071,200059,200083,200099,200086,30366,30303,30333,30334,30376,30379,30406,102707,70005,102408,102458,102703,102704,200031} },
    { 'LOOP_STRONG', {1053027,1053026,109927,200005,70141,70063,109506,30302,30314,107031,1053070,102404,102454,109914,109917,109923,109515,10532,200035,200036,102400,102401,102402,102450,102451,102452,109014,109015,109016,109920,109921,109922,102407,102457,102408,102458} },
    { 'GATHER', {104002,104003,104021,104027,104004,104005,104006,104007,104015,104016,104033,104039,104045,104051,104013,104014,104017,104022,104023,104024,104028,104029,104030,104034,104035,104040,104041,104046,104047,104018,104019,104020,104025,104026,104031,104032,104036,104037,104038,104042,104043,104044,104048,104049,104050} },
    { 'AUX', {30005,70110,70025,102701,102702,109602,109603} },
    { 'UNKNOWN', {200102,107201,107202,107203,107204,70182,70183,70186,70187,200095,1053027,109927,200005,109506,70141,70063} },
    { 'COMBAT_SKILL', {102502,102503,102505,102508,102705,102706,103007,103008,102605,102606,102607} },
    { 'SCROLL', {109604,109605,109606,109607,109608,109609} },
    { 'ATTR', {104009,104010,104011,104012,104001} }
  }
  local seen = {}
  local ids = {}
  local removed = 0
  for _, entry in ipairs(entries) do
    for _, id in ipairs(read_named_buff_list(entry[1], entry[2])) do
      if not seen[id] then seen[id] = true; table.insert(ids, id) end
    end
  end
  for pass = 1, 2 do
    for _, id in ipairs(ids) do
      remove_buff_repeat('remove_all.pass' .. tostring(pass), id, 6)
      if pass == 1 then removed = removed + 1 end
    end
  end
  for _, id in ipairs({102404,102454,102401,102451,102402,102452,109014,109015,109016,109920,109921,109922,109923,102407,102457,102408,102458}) do
    remove_buff_repeat('remove_all.stubborn', id, 20)
  end
  clear_all_local_buffs('remove_all.postclear')
  __add('remove_all_buffs', 'removed', removed)
end
_G.LOOP_BUFF_ACTIVE = _G.__GM_TOOL_LOOP_BUFF_ACTIVE
_G.LOOP_LOOT_ACTIVE = _G.__GM_TOOL_LOOP_LOOT_ACTIVE
_G.LOOP_RECOVER_ACTIVE = _G.__GM_TOOL_LOOP_RECOVER_ACTIVE
";
            if (mode == "loop_buff") body += "\ntoggle_loop('LOOP_BUFF', 3.0, apply_strong_buffs)\n";
            else if (mode == "loop_loot") body += "\ntoggle_loop('LOOP_LOOT', 10.0, run_loot_once)\n";
            else if (mode == "loop_recover") body += "\ntoggle_loop('LOOP_RECOVER', 5.0, run_recover_once)\n";
            else if (mode == "loop_defense") body += "\ntoggle_loop('LOOP_DEFENSE', 5.0, apply_defense_buffs)\n";
            else if (mode == "auto_buff") body += "\napply_auto_buffs()\n__add('auto_buff', 'applied')\n";
            else if (mode == "gather_buff") body += "\napply_gather_buffs()\n__add('gather_buff', 'applied_45_ids')\n";
            else if (mode == "aux_buff") body += "\napply_aux_buffs()\n__add('aux_buff', 'applied_9_ids')\n";
            else if (mode == "unknown_buff") body += "\napply_unknown_buffs()\n__add('unknown_buff', 'applied_16_ids')\n";
            else if (mode == "remove_all_buffs") body += "\nremove_all_buff_sets()\n";
            else if (mode == "atk_mul_2") body += "\n__try('atk_mul.add_1053017', function() return add_buff(1053017) end)\n__add('atk_mul', 'x2_applied')\n";
            else if (mode == "atk_mul_4") body += "\n__try('atk_mul.add_1053018', function() return add_buff(1053018) end)\n__add('atk_mul', 'x4_applied')\n";
            else if (mode == "atk_mul_8") body += "\n__try('atk_mul.add_1053019', function() return add_buff(1053019) end)\n__add('atk_mul', 'x8_applied')\n";
            else if (mode == "atk_mul_reset") body += "\nfor _, id in ipairs({1053017,1053018,1053019}) do __try('atk_mul.remove_' .. tostring(id), function() return remove_buff(id) end) end\n__add('atk_mul', 'reset_applied')\n";
            else if (mode == "combat_skill") body += "\napply_combat_skill_buffs()\n__add('combat_skill', 'applied_11_ids')\n";
            else if (mode == "scroll_buff") body += "\napply_scroll_buffs()\n__add('scroll_buff', 'applied_6_ids')\n";
            else if (mode == "attr_buff") body += "\napply_attr_buffs()\n__add('attr_buff', 'applied_6_ids')\n";
            else if (mode == "dialog_speed_20") body += "\nset_dialog_speed(20.0)\n";
            else if (mode == "dialog_speed_80") body += "\nset_dialog_speed(80.0)\n";
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
  if _G.__GM_TOOL_RHYTHM_ACTIVE then
    _G.__GM_TOOL_RHYTHM_ACTIVE = false
    local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene()
    if scene and _G.__GM_TOOL_RHYTHM_ACTION then scene:stopAction(_G.__GM_TOOL_RHYTHM_ACTION) end
    _G.__GM_TOOL_RHYTHM_ACTION = nil
    __add('rhythm_game', 'stopped')
    return
  end
  _G.__GM_TOOL_RHYTHM_ACTIVE = true
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
  _G.__GM_TOOL_RHYTHM_ACTION = action
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
            else if (mode == "gm_train_panel")
            {
                body += @"
local function open_gm_train_panel()
  __try('gm.open_combat_train', function()
    if type(_G.gm_combat) == 'table' and _G.gm_combat.open then _G.gm_combat:open()
    elseif type(_G.gm_combat) == 'table' and _G.gm_combat.show then _G.gm_combat:show() end
  end)
  __try('gm.open_train_ui', function()
    if type(_G.gm_decorator) == 'table' then
      if _G.gm_decorator.gm_open_combat_train then _G.gm_decorator:gm_open_combat_train() end
    end
  end)
  __try('gm.open_debug_panel', function()
    local scene = cc and cc.Director and cc.Director:getInstance():getRunningScene()
    if scene then
      if type(_G.gm_show_panel) == 'function' then _G.gm_show_panel() end
      if type(_G.show_debug_menu) == 'function' then _G.show_debug_menu() end
    end
  end)
  __add('gm_train_panel', 'executed')
end
open_gm_train_panel()
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
  if _G.__GM_TOOL_OUTFIT_PICKER then
    pcall(function() _G.__GM_TOOL_OUTFIT_PICKER:removeFromParent() end)
    _G.__GM_TOOL_OUTFIT_PICKER = nil
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
  local page = tonumber(_G.__GM_TOOL_OUTFIT_PAGE) or 1
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
        _G.__GM_TOOL_OUTFIT_SELECTED_INDEX = i
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
    if page > 1 then page = page - 1; _G.__GM_TOOL_OUTFIT_PAGE = page; pageText:setString(tostring(page) .. '/' .. tostring(totalPages)); render() end
  end)
  layer:addChild(prevBtn)
  local nextBtn = ccui.Button:create()
  nextBtn:setTitleText('下一页 >>')
  nextBtn:setTitleFontSize(24)
  nextBtn:setPosition(cc.p(width * 0.76, 35))
  nextBtn:addClickEventListener(function()
    if page < totalPages then page = page + 1; _G.__GM_TOOL_OUTFIT_PAGE = page; pageText:setString(tostring(page) .. '/' .. tostring(totalPages)); render() end
  end)
  layer:addChild(nextBtn)
  pageText:setString(tostring(page) .. '/' .. tostring(totalPages))
  render()
  scene:addChild(layer, 10001)
  _G.__GM_TOOL_OUTFIT_PICKER = layer
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
            if (mode == "yy_remove_buffs") body += "\n_G.RunRemoveBuffs()\n";
            return BuildLuaEnvelope("yylaoliu_buff_tool_" + mode, body);
        }

        string BuildYyLaoLiuLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
local player = type(G) == 'table' and G.main_player or nil
__add('player', type(player), player)
local BUFF_CONFIG_PATH = '" + buffConfigPath + @"'
local function LoadOriginalYyLaoLiuScript()
  if _G.__GM_TOOL_YY_LOADED then
    __add('LoadOriginalYyLaoLiuScript', 'already_loaded')
  else
  __try('LoadOriginalYyLaoLiuScript', function()
    local path = 'E:/ceshi/7/banyi/yylaoliu_decoded/stealth_all_base64_decoded.lua'
    local f, err = io.open(path, 'r')
    if not f then error('open original script failed: ' .. tostring(err)) end
    local code = f:read('*a')
    f:close()
    __add('stealth_all_base64_decoded.lua', 'bytes', tostring(#code))
    local fn, loadErr = load(code, '@stealth_all_base64_decoded.lua')
    if not fn then error('load original script failed: ' .. tostring(loadErr)) end
    local ok, runErr = pcall(fn)
    if not ok then error('run original script failed: ' .. tostring(runErr)) end
    _G.__GM_TOOL_YY_LOADED = true
  end)
  end
  __add('original.RunKillNPC', type(_G.RunKillNPC), _G.RunKillNPC)
  __add('original.GM_EnableOneHit', type(_G.GM_EnableOneHit), _G.GM_EnableOneHit)
  __add('original.RunAutoLoot', type(_G.RunAutoLoot), _G.RunAutoLoot)
  __add('original.GM_EnableNPCDUMB', type(_G.GM_EnableNPCDUMB), _G.GM_EnableNPCDUMB)
  __add('original.BUFF_LIST', type(_G.BUFF_LIST), _G.BUFF_LIST)
end
LoadOriginalYyLaoLiuScript()
local function DiagnoseYyLaoLiuFunctions()
  for _, k in ipairs({ 'ToggleAutoLootLoop', 'ToggleAutoBuffLoop', 'ToggleCombatBuffLoop', 'ToggleRecover', 'ToggleDefenseBuffLoop', 'ToggleSuperDodge', 'GM_EnableOneHit', 'GM_DisableOneHit', 'RunKillNPC', 'RunAutoLoot', 'GM_EnableNPCDUMB', 'RunRecover', 'ForceUpdateVisuals' }) do
    __add('yy_func.' .. k, type(_G[k]), _G[k])
  end
end
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
  __try('mp.add_buff_' .. tostring(id), function()
    if player and type(player.add_buff) == 'function' then player.add_buff(player, id) else error('main_player.add_buff missing') end
  end)
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
  mp_add_buff(70141)
  mp_add_buff(70063)
end
function RunAutoLoot()
  __try('ride_collect', function() if player and player.ride_skill_collect_nearby_collections then player:ride_skill_collect_nearby_collections(5000) else error('missing') end end)
  __try('kill_reward', function() if player and player.ride_skill_find_nearest_kill_reward then local r = player:ride_skill_find_nearest_kill_reward(5000); if r and player.ride_skill_get_kill_reward then player:ride_skill_get_kill_reward(r) end else error('missing') end end)
  __try('drop_manager', function() local dm = rawget(_G, 'DropManager'); if dm and dm.get_nearby_drop_entities then local drops = dm.get_nearby_drop_entities(5000) or {}; for _, eid in ipairs(drops) do pcall(function() player:pick_drop_item(eid) end); pcall(function() player:pick_reward_item(eid) end) end else error('missing') end end)
end
function RunNpcDumb()
  mp_add_buff(380013)
  __try('toggle_npc_ai', function() if type(toggle_npc_ai) == 'function' then toggle_npc_ai() else error('missing') end end)
end
function RunSuperDodge()
  mp_add_buff(102703)
  mp_add_buff(102704)
end
function RunWeaponGuise()
  __try('weapon_guise_panel', function()
    local gm = require('hexm.client.debug.imgui_panels.weapon_guise_panel')
    if gm and gm.WeaponGuisePanel then gm.WeaponGuisePanel(true) else error('missing') end
  end)
  __try('weapon_guise_shortcut', function()
    local dec = try_import('hexm.client.debug.gm.gm_decorator')
    local cmds = dec and dec.gm_command_short_cuts and dec.gm_command_short_cuts.game
    if cmds and cmds['$weapon_guise'] then cmds['$weapon_guise'](1) else error('missing') end
  end)
end
function RunAutoBuff()
  for _, id in ipairs({30302,30314,107031,1053070,102400,102401,102402,102404,102405,102406,102423,102425,102450,102451,102452,102454,102455,102456,109003,109009,109014,109015,109016,109021,109501,109503,109505,109507,109509,109511,109515,109901,109903,109905,109908,109909,109910,109911,109912,109914,109917,109920,109921,109922,109923,109926}) do mp_add_buff(id) end
end
function RunRemoveBuffs()
  local removed = 0
  for _, id in ipairs({108010,380013,70063,70141}) do
    if mp_remove_buff(id) then removed = removed + 1 end
  end
  __add('RunRemoveBuffs.removed', removed)
end
function LogCurrentPosition()
  __try('log_position', function()
    local pos = player and player.get_position and player:get_position() or player.position or player.pos
    __add('position', type(pos), pos and pos.x, pos and pos.y, pos and pos.z)
  end)
end
function RunTeleportToCustom()
  __add('custom_tp', 'needs coordinates from yylaoliucn custom file; not fully wired')
end
function RunTeleportRandom()
  __try('random_tp', function()
    local skip = try_import('hexm.client.ui.windows.gm.gm_skip.skip_action') or try_import('hexm.client.ui.windows.gm.gm_skip.skip_misc')
    if skip and skip.gm_skip_flow_imp then skip.gm_skip_flow_imp(math.random(1, 999999)) else error('missing') end
  end)
end
";
            if (mode == "yy_load_script") body += "\nDiagnoseYyLaoLiuFunctions()\n";
            else if (mode == "yy_autoloot") body += "\n__try('original.ToggleAutoLootLoop.call', function() if type(_G.ToggleAutoLootLoop) == 'function' then return _G.ToggleAutoLootLoop() elseif type(_G.RunAutoLoot) == 'function' then return _G.RunAutoLoot() else return RunAutoLoot() end end)\n";
            else if (mode == "yy_npcdumb") body += "\n__try('original.GM_EnableNPCDUMB.call', function() if type(_G.GM_EnableNPCDUMB) == 'function' then return _G.GM_EnableNPCDUMB() else return RunNpcDumb() end end)\n";
            else if (mode == "yy_recover") body += "\n__try('original.ToggleRecover.call', function() if type(_G.ToggleRecover) == 'function' then return _G.ToggleRecover() elseif type(_G.RunRecover) == 'function' then return _G.RunRecover() else return RunRecover() end end)\n";
            else if (mode == "yy_superdodge") body += "\n__try('original.ToggleSuperDodge.call', function() if type(_G.ToggleSuperDodge) == 'function' then return _G.ToggleSuperDodge() elseif type(_G.RunSuperDodge) == 'function' then return _G.RunSuperDodge() else return RunSuperDodge() end end)\n";
            else if (mode == "yy_weapon") body += "\n__try('original.RunWeaponGuise.call', function() if type(_G.RunWeaponGuise) == 'function' then return _G.RunWeaponGuise() else return RunWeaponGuise() end end)\n";
            else if (mode == "yy_randomtp") body += "\n__try('original.SafeRunTeleportRandom.call', function() if type(_G.SafeRunTeleportRandom) == 'function' then return _G.SafeRunTeleportRandom() else return RunTeleportRandom() end end)\n";
            else if (mode == "yy_logpos") body += "\n__try('original.LogCurrentPosition.call', function() if type(_G.LogCurrentPosition) == 'function' then return _G.LogCurrentPosition() else return LogCurrentPosition() end end)\n";
            else if (mode == "yy_customtp") body += "\n__try('original.RunTeleportToCustom.call', function() if type(_G.RunTeleportToCustom) == 'function' then return _G.RunTeleportToCustom() else return RunTeleportToCustom() end end)\n";
            else if (mode == "yy_removebuffs") body += "\n__try('buff_config.remove_or_original', function() if not apply_config_remove_buffs() then if type(_G.RunRemoveBuffs) == 'function' then return _G.RunRemoveBuffs() else return RunRemoveBuffs() end end end)\n";
            else if (mode == "yy_autobuff") body += "\n__try('buff_config.auto_or_original', function() if not apply_config_auto_buffs() then if type(_G.ToggleAutoBuffLoop) == 'function' then return _G.ToggleAutoBuffLoop() elseif type(_G.ToggleCombatBuffLoop) == 'function' then return _G.ToggleCombatBuffLoop() elseif type(_G.RunAutoBuff) == 'function' then return _G.RunAutoBuff() else return RunAutoBuff() end end end)\n";
            else if (mode == "yy_permbuffs") body += "\n__try('buff_config.permanent_or_original', function() if not apply_config_permanent_buffs() then if type(_G.RunPermanentBuffs) == 'function' then return _G.RunPermanentBuffs() else error('missing') end end end)\n";
            else if (mode == "yy_disablelogs") body += "\n__try('original.DisableLogsAndChecks.call', function() if type(_G.DisableLogsAndChecks) == 'function' then return _G.DisableLogsAndChecks() else error('missing') end end)\n";
            return BuildLuaEnvelope("yylaoliu_" + mode, body);
        }

        string BuildCombatExperimentLua(string mode)
        {
            string buffConfigPath = BuffConfigFile.Replace("\\", "/");
            string body = @"
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
  local ok, mod = pcall(require, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
  if ok and mod then return mod end
  if portable and portable.import then
    ok, mod = pcall(portable.import, 'hexm.client.ui.windows.gm.gm_combat.combat_train_action')
    if ok and mod then return mod end
  end
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
  if type(_G.gm_combat) == 'table' then return _G.gm_combat end
  if type(gm_combat) == 'table' then return gm_combat end
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
  __try('main_player.add_buff_' .. tostring(buffNo), function()
    if player and type(player.add_buff) == 'function' then player.add_buff(player, buffNo) else error('main_player.add_buff missing') end
  end)
end
local function main_remove_buff(buffNo)
  __try('main_player.remove_buff_' .. tostring(buffNo), function()
    if player and type(player.remove_buff) == 'function' then player.remove_buff(player, buffNo)
    elseif player and type(player.rm_buff) == 'function' then player.rm_buff(player, buffNo)
    else error('main_player remove method missing') end
  end)
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
local function set_speed(speed, duration)
  call_class('local_game_speed_duration_num', 'hexm.client.entities.local.player_avatar_members.imp_game_speed', 'PlayerAvatarMember', 'set_speed_by_duration', speed, duration)
  call_class('local_game_speed_duration_table', 'hexm.client.entities.local.player_avatar_members.imp_game_speed', 'PlayerAvatarMember', 'set_speed_by_duration', { speed = speed, duration = duration, type = 1, config_no = 990001 })
  call_class('local_game_speed_push_config_num', 'hexm.client.entities.local.player_avatar_members.imp_game_speed', 'PlayerAvatarMember', 'push_game_speed_config', 990001, speed, duration or 30)
  call_class('local_game_speed_push_config_table', 'hexm.client.entities.local.player_avatar_members.imp_game_speed', 'PlayerAvatarMember', 'push_game_speed_config', { config_no = 990001, speed = speed, duration = duration or 30, type = 1 })
  call_class('local_speed_refresh', 'hexm.client.entities.local.player_avatar_members.imp_speed', 'PlayerAvatarMember', 'speed_refresh')
end
local function get_locked_target()
  local target_id = nil
  __try('target.get_lock_target_id', function()
    if player and type(player.get_lock_target_id) == 'function' then
      target_id = player.get_lock_target_id(player)
      __add('target_id', type(target_id), target_id)
    else
      __add('target_id.missing_method')
    end
  end)
  local target = nil
  __try('target.get_entity', function()
    if target_id and G and G.space and type(G.space.get_entity) == 'function' then
      target = G.space.get_entity(G.space, target_id)
      __add('target', type(target), target)
    else
      __add('target.get_entity.missing', tostring(target_id))
    end
  end)
  return target, target_id
end
local function probe_target_damage_methods()
  local target, target_id = get_locked_target()
  __add('target_probe.result', type(target), target, 'target_id', tostring(target_id))
  if target then
    for _, k in ipairs({ 'force_set_HP', 'attr_set_HP', 'do_direct_damage', 'attr_get_HP', 'get_hp', 'get_HP' }) do
      __add('target_method.' .. k, type(target[k]), target[k])
    end
  end
  return target, target_id
end
local function call_target_class(label, path, className, method, target, ...)
  local args = { ... }
  __try(label .. '.require', function()
    local mod = require(path)
    __add(label .. '.module', type(mod), mod)
    local cls = mod and mod[className]
    __add(label .. '.class', type(cls), cls)
    if cls and type(cls[method]) == 'function' then
      local ret = { cls[method](target, table.unpack(args)) }
      local parts = { label .. '.' .. method, 'ret_count=' .. tostring(#ret) }
      for i = 1, math.min(#ret, 6) do parts[#parts + 1] = tostring(ret[i]) end
      __add(table.unpack(parts))
    else
      __add(label .. '.missing_class_method', method)
    end
  end)
end
local function target_onehit()
  local target, target_id = probe_target_damage_methods()
  if not target then return end
  local fromid = player and player.entity_id or 0
  __try('target.force_set_HP', function()
    if type(target.force_set_HP) == 'function' then target.force_set_HP(target, 0, fromid, 'gm') else error('force_set_HP missing') end
  end)
  __try('target.attr_set_HP', function()
    if type(target.attr_set_HP) == 'function' then target.attr_set_HP(target, 0, fromid, true, false) else error('attr_set_HP missing') end
  end)
  __try('target.do_direct_damage', function()
    if type(target.do_direct_damage) == 'function' then target.do_direct_damage(target, 999999999, fromid, 0, 0, 0, 0) else error('do_direct_damage missing') end
  end)
  call_target_class('class_attr_base_force_hp', 'hexm.common.base.attr_base', 'AttrBase', 'force_set_HP', target, 0, fromid, 'gm')
  call_target_class('class_attr_base_attr_hp', 'hexm.common.base.attr_base', 'AttrBase', 'attr_set_HP', target, 0, fromid, true, false)
  call_target_class('class_btree_attr_force_hp', 'hexm.client.entities.local.btree_ai_members.imp_attr_base_res', 'BtreeAttrBase', 'force_set_HP', target, 0, fromid, 'gm')
  call_target_class('class_btree_attr_attr_hp', 'hexm.client.entities.local.btree_ai_members.imp_attr_base_res', 'BtreeAttrBase', 'attr_set_HP', target, 0, fromid, true, false)
  call_target_class('class_behit_direct_damage', 'hexm.common.combat.behit.behit_base', 'BehitBase', 'do_direct_damage', target, 999999999, fromid, 0, 0, 0, 0)
  call_target_class('class_behit_behit', 'hexm.common.combat.behit.behit_base', 'BehitBase', 'behit', target, { attacker = fromid, damage = 999999999, value = 999999999 })
end
local function dump_npc_member_candidates()
  local target, target_id = get_locked_target()
  __add('npc_member_probe.target', type(target), target, 'target_id', tostring(target_id))
  if not target then return end
  local names = { 'attr', '_attr', 'attr_base', 'behit', '_behit', 'behit_base', 'combat', '_combat', 'members', '_members', 'member_dict', '_member_dict', 'component', 'components', '_components', 'logic', '_logic', 'entity', '_entity' }
  for _, k in ipairs(names) do
    __try('npc_field.' .. k, function()
      local v = target[k]
      __add('npc_field.' .. k, type(v), v)
      if v and type(v) ~= 'number' and type(v) ~= 'string' and type(v) ~= 'boolean' then
        for _, mk in ipairs({ 'force_set_HP', 'attr_set_HP', 'do_direct_damage', 'behit', '_on_damage', 'dead', 'attr_get_HP', 'get_hp' }) do
          local mv = v[mk]
          __add('npc_field_method.' .. k .. '.' .. mk, type(mv), mv)
        end
      end
    end)
  end
  __try('npc_loaded_member_modules', function()
    local count = 0
    for name, mod in pairs(package.loaded) do
      local s = tostring(name)
      if s:find('npc_members') or s:find('btree_ai_members') or s:find('attr_base') or s:find('behit') then
        count = count + 1
        if count <= 80 then __add('loaded_member_module', s, type(mod), mod) end
      end
    end
    __add('loaded_member_module_count', count)
  end)
end
__try('require.buff_invincible', function() local m = require('hexm.common.combat.buff.members.buff_invincible'); __add('buff_invincible', type(m), m) end)
__try('require.buff_misc', function() local m = require('hexm.common.misc.buff_misc'); __add('buff_misc', type(m), m) end)
";
            if (mode == "god") body += @"
add_buff(70063)
add_buff(30372)
add_buff(70005)
";
            else if (mode == "onehit") body += @"
call_gm_action('set_niubility', 1)
";
            else if (mode == "onehit_off") body += @"
call_gm_action('set_niubility', 0)
";
            else if (mode == "target_onehit") body += @"
target_onehit()
";
            else if (mode == "target_probe") body += @"
probe_target_damage_methods()
";
            else if (mode == "npc_member_probe") body += @"
dump_npc_member_candidates()
";
            else if (mode == "stamina") body += @"
call_gm_action('set_lock_res_consume', true)
call_gm_combat('gm_set_sp_calc', 1)
call_gm_combat('gm_lock_res_consume', true)
call_gm_combat('gm_unlimited_dive_resource', true)
call_gm_combat('gm_empty_combat_resource')
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
add_buff(108010)
main_add_buff(108010)
";
            else if (mode == "invis_off") body += @"
call_gm_action('rm_buff', 108010)
remove_buff(108010)
main_remove_buff(108010)
";
            else if (mode == "speed3") body += @"
set_speed(3, 30)
";
            else if (mode == "speed1") body += @"
set_speed(1, 1)
";
            else if (mode == "atkbuff_combo" || mode == "atkbuff_basic" || mode == "atkbuff_physical" || mode == "food_atk" || mode == "atkbuff_all" || mode == "atkbuff") body += @"
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
            IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, proc.Id);
            if (hProcess == IntPtr.Zero) { AppendLog("内存速度失败: 无法打开进程"); return; }
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
            finally { CloseHandle(hProcess); }
        }

        void UpdateStatus()
        {
            SyncReadyState(false);
            string status = "";
            Color color = Color.Blue;
            Process[] procs = Process.GetProcessesByName("yysls");
            bool gameRunning = procs.Length > 0;

            if (string.IsNullOrEmpty(gameRootPath)) { status = "请先选择游戏目录"; color = Color.Gray; }
            else if (!gameRunning && !gameLaunched) { status = "游戏未运行 - 按步骤操作"; color = Color.Gray; }
            else if (!gameRunning && gameLaunched) { status = "游戏可能已退出"; color = Color.Red; gameLaunched = false; }
            else if (isReady) { status = "已就绪 - 可以使用 GM 命令"; color = Color.Green; }
            else if (!string.IsNullOrEmpty(gameBinPath) && File.Exists(Path.Combine(gameBinPath, "frida-gadget.config"))) { status = "方案B文件已就位 - 注入后检查 gm_tool.log"; color = Color.Orange; }
            else { status = "游戏运行中 - 先点[复制文件]再注入Frida"; color = Color.Blue; }

            lblStatus.Text = "状态: " + status;
            lblStatus.ForeColor = color;
            SetGMEnabled(isReady);
        }

        void SetGMEnabled(bool enabled)
        {
            SetButtonsEnabled(grpGM, enabled);
        }

        void ApplyCleanStyle()
        {
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(246, 248, 250);
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
                    button.UseVisualStyleBackColor = false;
                }

                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = Color.FromArgb(31, 41, 55);
                    textBox.BorderStyle = BorderStyle.FixedSingle;
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
                Button button = control as Button;
                if (button != null && (button.Tag as string) != "nav") button.Enabled = enabled;
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
                string gameLogPath = GetGameFilePath("gm_tool.log");
                string activeLogPath = GetPreferredExistingPath(gameLogPath, UnifiedLogFile);
                if (string.IsNullOrEmpty(activeLogPath) || !File.Exists(activeLogPath)) return false;
                string text = File.ReadAllText(activeLogPath);
                if (string.IsNullOrEmpty(text)) return false;
                bool readyLogged =
                    text.Contains("Ready. Command direct executor is polling:") ||
                    text.Contains("Ready file created:");
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
            if (txtLog == null) return;
            try
            {
                string combined = BuildCombinedLogText(12);
                if (!string.IsNullOrEmpty(combined))
                {
                    txtLog.Text = combined;
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                }
            }
            catch { }
        }

        string BuildCombinedLogText(int maxLinesPerFile)
        {
            List<string> sections = new List<string>();
            AddLogSection(sections, "Unified Log", UnifiedLogFile, maxLinesPerFile);
            AddLogSection(sections, "Command Result", CmdResultFile, maxLinesPerFile);
            AddLogSection(sections, "Feature Result", ToolResultFile, maxLinesPerFile);
            return sections.Count > 0 ? string.Join("\r\n\r\n", sections.ToArray()) : "";
        }

        void AddLogSection(List<string> sections, string title, string path, int maxLines)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            int start = Math.Max(0, lines.Length - maxLines);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== " + title + " ===");
            for (int i = start; i < lines.Length; i++) sb.AppendLine(lines[i]);
            sections.Add(sb.ToString().TrimEnd());
        }

        string BuildUnifiedLogSnapshot()
        {
            string gameLogPath = GetGameFilePath("gm_tool.log");
            string activeLogPath = GetPreferredExistingPath(gameLogPath, UnifiedLogFile);
            return File.Exists(activeLogPath) ? activeLogPath : "";
        }

        void ClearManagedFiles()
        {
            string[] paths = new string[] { CmdFile, CmdResultFile, ToolResultFile, ToolResultCompatFile, UnifiedLogFile, Path.Combine(ToolDir, "frida_ready.txt"), Path.Combine(ToolDir, "frida_log.txt"), Path.Combine(ToolDir, "connector_log.txt"), Path.Combine(ToolDir, "gm_tool_ui.log"), Path.Combine(ToolDir, "gm_tool_all.log") };
            foreach (string path in paths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
                }
                catch { }
            }
            UpdateLogView();
        }

        void AppendLog(string msg)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            try { File.AppendAllText(UnifiedLogFile, line + Environment.NewLine, new UTF8Encoding(false)); } catch { }
            if (txtLog != null)
            {
                try
                {
                    if (txtLog.InvokeRequired) txtLog.BeginInvoke(new Action(UpdateLogView));
                    else UpdateLogView();
                }
                catch { }
            }
        }

        void SaveConfig() { try { File.WriteAllText(ConfigFile, gameRootPath); } catch { } }
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
                        if (File.Exists(Path.Combine(binPath, GameExeName)))
                        { gameRootPath = path; gameBinPath = binPath; UpdatePathLabel(); UpdateCommPaths(); }
                    }
                }
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 3000;
            timer.Tick += delegate(object s, EventArgs e2)
            {
                bool wasReady = isReady;
                SyncReadyState(!wasReady);
                if (!wasReady && isReady) UpdateStatus();
                UpdateLogView();
                if (gameLaunched && Process.GetProcessesByName("yysls").Length == 0)
                { gameLaunched = false; isReady = false; UpdateStatus(); }
            };
            timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GMForm());
        }
    }
}