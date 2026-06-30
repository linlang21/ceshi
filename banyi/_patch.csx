using System;
using System.IO;
using System.Text;

class P
{
    static void Main()
    {
        string path = @"E:\ceshi\banyi\FridaGMTool.NetFx.cs";
        string text = File.ReadAllText(path, new UTF8Encoding(false));
        bool changed = false;

        // 修复1: flyModeEnabled/nudgeModeEnabled 加 volatile (钩子线程读, UI 线程写)
        string old1 = "        bool flyModeEnabled = false;     // 飞天遁地: 小键盘上下瞬移\n        bool nudgeModeEnabled = false;   // 瞬移: Alt+方向键东南西北瞬移";
        string new1 = "        volatile bool flyModeEnabled = false;     // 飞天遁地: 方向键上下瞬移 (钩子线程读, 需 volatile)\n        volatile bool nudgeModeEnabled = false;   // 瞬移: Alt+方向键东南西北瞬移 (钩子线程读, 需 volatile)";
        if (text.Contains(old1)) { text = text.Replace(old1, new1); changed = true; Console.WriteLine("fix1: volatile fields OK"); }
        else Console.WriteLine("fix1: SKIP (not found)");

        // 修复2: 去抖标志加 volatile
        string old2 = "        bool flyKeyUp = true, flyKeyDown = true;\n        bool nudgeKeyLeft = true, nudgeKeyRight = true, nudgeKeyUp = true, nudgeKeyDown = true;";
        string new2 = "        volatile bool flyKeyUp = true, flyKeyDown = true;\n        volatile bool nudgeKeyLeft = true, nudgeKeyRight = true, nudgeKeyUp = true, nudgeKeyDown = true;";
        if (text.Contains(old2)) { text = text.Replace(old2, new2); changed = true; Console.WriteLine("fix2: volatile debounce OK"); }
        else Console.WriteLine("fix2: SKIP (not found)");

        // 修复3: liveCoordTimer 改用后台线程读坐标, 不阻塞 UI 消息泵 (否则 WH_KEYBOARD_LL 钩子超时被系统断开)
        string old3 = "            liveCoordTimer.Tick += (s, e2) =>\n            {\n                if (!chkEnableMemory.Checked) return;\n                double x, y, z;\n                if (ReadMemCoord(out x, out y, out z))\n                {\n                    lblLiveCoord.Text = string.Format(\"X={0:F1}  Y={1:F1}  Z={2:F1}\", x, y, z);\n                    lastReadX = x; lastReadY = y; lastReadZ = z;\n                }\n            };";
        string new3 = "            liveCoordTimer.Tick += (s, e2) =>\n            {\n                if (!memoryEnabledCache) return;\n                // 后台线程读坐标, 避免 RPM 阻塞 UI 消息泵导致 WH_KEYBOARD_LL 钩子超时被系统断开\n                System.Threading.Tasks.Task.Run(() =>\n                {\n                    double x, y, z;\n                    if (ReadMemCoord(out x, out y, out z))\n                    {\n                        lastReadX = x; lastReadY = y; lastReadZ = z;\n                        try { BeginInvoke((Action)(() => { lblLiveCoord.Text = string.Format(\"X={0:F1}  Y={1:F1}  Z={2:F1}\", x, y, z); })); } catch { }\n                    }\n                });\n            };";
        if (text.Contains(old3)) { text = text.Replace(old3, new3); changed = true; Console.WriteLine("fix3: liveCoordTimer background OK"); }
        else Console.WriteLine("fix3: SKIP (not found)");

        if (changed)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Console.WriteLine("ALL DONE");
        }
        else Console.WriteLine("NO CHANGES");
    }
}
