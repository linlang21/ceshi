# -*- coding: utf-8 -*-
import sys
p = r'E:\ceshi\banyi\FridaGMTool.NetFx.cs'
with open(p, 'r', encoding='utf-8-sig', newline='') as f:
    t = f.read()

def rep(old, new, label):
    global t
    c = t.count(old)
    if c != 1:
        print('FAIL [%s] count=%d' % (label, c))
        sys.exit(1)
    t = t.replace(old, new)
    print('OK   [%s]' % label)

# 1) VK_F9 常量
rep('        const ushort VK_F11 = 0x7A;',
    '        const ushort VK_F11 = 0x7A;\n        const ushort VK_F9 = 0x78;          // 朝向字段扫描诊断热键',
    'VK_F9 const')

# 2) hotkeyF9Up 去抖变量
rep('        volatile bool hotkeyF11Up = true, hotkeyLeftUp = true, hotkeyRightUp = true;',
    '        volatile bool hotkeyF11Up = true, hotkeyLeftUp = true, hotkeyRightUp = true;\n        volatile bool hotkeyF9Up = true;',
    'hotkeyF9Up var')

# 3) 钩子卸载时重置去抖
rep('                hotkeyF11Up = hotkeyLeftUp = hotkeyRightUp = true;',
    '                hotkeyF11Up = hotkeyLeftUp = hotkeyRightUp = true;\n                hotkeyF9Up = true;',
    'reset hotkeyF9Up')

# 4) NudgeKbProc 里在 F11 分支后插入 F9 分支 (锚点: VK_LEFT+hotkeyLeftUp 唯一)
rep('                        else if (vk == VK_LEFT && isDown && hotkeyLeftUp)',
    '                        else if (vk == VK_F9 && isDown && hotkeyF9Up)\n'
    '                        {\n'
    '                            hotkeyF9Up = false;\n'
    '                            System.Threading.Tasks.Task.Run(() => { try { ScanYawField(); } catch (Exception ex) { try { BeginInvoke((Action)(() => AppendLog("[朝向扫描] 异常: " + ex.Message))); } catch { } } });\n'
    '                        }\n'
    '                        else if (vk == VK_LEFT && isDown && hotkeyLeftUp)',
    'F9 branch in NudgeKbProc')

# 5) isUp 分支加 F9 复位
rep('                            if (vk == VK_F11) hotkeyF11Up = true;\n'
    '                            else if (vk == VK_LEFT) hotkeyLeftUp = true;',
    '                            if (vk == VK_F11) hotkeyF11Up = true;\n'
    '                            else if (vk == VK_F9) hotkeyF9Up = true;\n'
    '                            else if (vk == VK_LEFT) hotkeyLeftUp = true;',
    'F9 reset in isUp')

# 6) 在 YAW_OFFSET 常量前插入 ScanYawField 函数
scan_fn = (
'        // 朝向字段扫描: F9 触发, 两阶段对比定位真正的 YAW 偏移\n'
'        // 0x358 未经验证且读到随机值, 需通过此功能找到正确偏移后再改 YAW_OFFSET\n'
'        // 用法: 启用内存功能 -> 站定不动按F9记录基准 -> 原地转身约90度再按F9 -> 日志输出角度候选偏移\n'
'        Dictionary<long, float> yawScanBaseline = null;\n'
'        void ScanYawField()\n'
'        {\n'
'            IntPtr hProcess = AcquireCoordHandle();\n'
'            if (hProcess == IntPtr.Zero) { try { BeginInvoke((Action)(() => AppendLog("[朝向扫描] 无法获取进程句柄"))); } catch { } return; }\n'
'            long obj;\n'
'            if (!ResolveCoordBase(hProcess, out obj, false)) { try { BeginInvoke((Action)(() => AppendLog("[朝向扫描] 无法解析对象基址 (先初始化坐标)"))); } catch { } return; }\n'
'            var current = new Dictionary<long, float>();\n'
'            for (long off = 0x300; off <= 0x400; off += 4)\n'
'            {\n'
'                float v = ReadFloat(hProcess, obj + off);\n'
'                if (!float.IsNaN(v) && !float.IsInfinity(v)) current[off] = v;\n'
'            }\n'
'            if (yawScanBaseline == null)\n'
'            {\n'
'                yawScanBaseline = current;\n'
'                try { BeginInvoke((Action)(() => AppendLog(string.Format("[朝向扫描] 已记录基准 ({0} 个字段)。请原地转身约90度后再按F9", current.Count)))); } catch { }\n'
'                return;\n'
'            }\n'
'            try\n'
'            {\n'
'                BeginInvoke((Action)(() =>\n'
'                {\n'
'                    AppendLog("=== 朝向字段对比 (仅显示变化项, 标记角度候选) ===");\n'
'                    int found = 0;\n'
'                    foreach (var kv in current)\n'
'                    {\n'
'                        if (!yawScanBaseline.ContainsKey(kv.Key)) continue;\n'
'                        float oldV = yawScanBaseline[kv.Key];\n'
'                        float diff = kv.Value - oldV;\n'
'                        if (Math.Abs(diff) > 0.1f)\n'
'                        {\n'
'                            bool oldAngle = (oldV >= -Math.PI - 0.5 && oldV <= Math.PI * 2 + 0.5);\n'
'                            bool newAngle = (kv.Value >= -Math.PI - 0.5 && kv.Value <= Math.PI * 2 + 0.5);\n'
'                            string mark = (oldAngle && newAngle) ? "  <== 角度候选" : "";\n'
'                            AppendLog(string.Format("[朝向] +0x{0:X3}  {1:F4} -> {2:F4}  d={3:F4}{4}", kv.Key, oldV, kv.Value, diff, mark));\n'
'                            found++;\n'
'                        }\n'
'                    }\n'
'                    if (found == 0) AppendLog("[朝向扫描] 未检测到变化字段, 请确认已转身");\n'
'                    yawScanBaseline = current;\n'
'                    AppendLog("[朝向扫描] 已更新基准, 可再次转身后按F9继续对比");\n'
'                }));\n'
'            }\n'
'            catch { }\n'
'        }\n'
'\n'
'        const long YAW_OFFSET = 0x358;'
)
rep('        const long YAW_OFFSET = 0x358;', scan_fn, 'ScanYawField func')

with open(p, 'w', encoding='utf-8-sig', newline='') as f:
    f.write(t)
print('DONE: 6 modifications applied')
