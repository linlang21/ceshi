# 燕云十六声 FridaGM 辅助工具 — 开发文档

> **当前版本：** v38.2（C# .NET Framework 4.x 单文件 EXE）｜payload v38.0
> **最后更新：** 2026-06-30
> **项目总览文档：** `E:\ceshi\项目文档.md`  
> **当前源码目录：** `E:\ceshi\banyi`  
> **当前运行目录：** `E:\ceshi\banyi\FridaGM`

---

## 1. 快速上手（给其他工具 / 协作者）

> 不熟悉本项目的工具或人，先读这一节即可掌握全貌。

### 当前状态
FridaGM 工具核心链路**已全部打通并可用**，当前真实执行链路如下：

1. C# 主程序启动后，读取/保存游戏目录，维护 `config.txt`、`buff_config.txt`。
2. 点击“注入”时，先生成 `svc.cfg`，写入**随机化通信配置**：
   - 随机共享内存名
   - 随机命令文件名
   - 随机结果文件名
   - 随机日志文件名
3. C# 先创建共享内存，再用 `CreateRemoteThread(LoadLibraryA)` 将 `core.dll` 注入 `yysls.exe`。
4. `core.dll` 通过 `core.config` 自动加载 `gm_payload.js`。
5. `gm_payload.js` 先读取 `svc.cfg`，拿到随机化后的通信参数。
6. payload 在 `yysls.exe` 主模块内用 **AOB 特征码扫描**定位 `lua_pcallk` 与 `luaL_loadbufferx`。
7. payload 对 `lua_pcallk` 做**短暂 Hook**，捕获 `lua_State *L` 和线程 ID，随后立即 `Interceptor.detach()`。
8. 就绪后，C# 通过共享内存发送 Lua 命令；若共享内存不可用，则降级为文件轮询。
9. payload 在捕获到的 Lua 线程上执行 `luaL_loadbufferx` + `lua_pcallk`，将结果写回共享内存 / 结果文件。

换句话说，这不是“外部调试器 + 命令转发器”，而是：

**单文件 EXE UI → 内嵌注入 `core.dll` → Gadget 自动加载 `gm_payload.js` → 短暂 Hook 捕获 `lua_State` → 运行时下发 Lua。**

**最近完成（v38.2，2026-06-30，UI 大重构 + 传送按键修复）：**
- **标签页重构**：删除「工具」「Buff」标签页；4 个标签改为「启动」「功能」「传送」「测试」
- **Buff 功能转移**：原 Buff 标签页的攻击/防御/最小/采集/辅助/未知 Buff、移除 Buff、循环 Buff 全部移入「功能」标签页的「Buff 施加」「循环功能」区段（位于「辅助」与「剧情速度」之间）
- **工具内容转移**：原「工具」标签页的体力/资源类、攻击倍率、攻击速度全部移入「测试」标签页
- **窗口尺寸精简**：560×600 → 514×530；左右边距 12→6；按钮宽度 118→110、高度 28→26；行距 34→30；分区间距 8→4
- **置顶按钮**：标签栏末尾添加「置」按钮，默认置顶时绿色框，点击切换 TopMost
- **按钮文字精简**：快速启动→启动、NPC节奏游戏→节奏游戏、终止过场动画→终止动画、关闭安全标志→关闭标志、锁体力消耗→无限体力、无限潜水资源→无限潜水、清空战斗资源→清空战斗、恢复体力设置→恢复体力、还原攻击速度→还原速度、循环强力Buff→强力Buff（所有循环按钮去掉「循环」两字）
- **传送按键修复**：PostKeyPress 改为广播式，同时向游戏窗口+所有可见子窗口发 PostMessageW；用 AttachThreadInput 让目标窗口线程获得键盘焦点；FireTeleportKeys 增加诊断日志输出窗口标题/类名
- **传送时序对齐 AAA 逆向**：WriteMemCoord 改为后台线程 Task.Run，先 FireTeleportKeys()（SPACE↓↑→Q↓↑）再 WriteMemCoordCore()（写内存+冻结），避免 UI 线程阻塞导致键盘钩子超时闪退
- **方向传送基于人物朝向**：新增 TryNudgeDirectional(forward, right, up)，读取 YAW(0x358) 角度后用 sin/cos 转换为世界坐标 dx/dy，Alt+方向键按人物前后左右传送
- **飞天遁地/瞬移热键**：方向键↑飞天/↓遁地、Alt+方向键↑↓←→ 瞬移；全局低级键盘钩子 WH_KEYBOARD_LL，钩子委托用字段保持引用避免 GC 回收崩溃
- **坐标列表优化**：选中项滚动到中间、禁止拖动行高、滚动条细条无背景、双击选中不自动传送
- **传送热键**：F11 传送到选中、左/右箭头切换并传送上一条/下一条（循环）

**最近完成（v38.1）：**
- **传送功能改为静态指针链方案（从 AAA.exe 逆向提取）**：移植 `yysls.exe + 0x083F46D8 → +0x58 → +0x00 → OBJ` 三层指针链，直接获取**玩家对象**（非相机），跳过 slot 切换与相机偏移校正复杂度
- **AOB 扫描自动定位 STATIC_OFFSET**：硬编码偏移失效时，扫描代码段 `mov/lea r64,[rip+disp32]` 指令，找被引用≥3次且通过指针链验证的偏移（17 处 RIP-relative 引用作为稳定特征）
- **修正 Y/Z 偏移命名**：旧代码 `COORD_OFFSET_Y=0x348` / `COORD_OFFSET_Z=0x350` 物理含义反了，已互换为正确命名（0x348=Z/高度，0x350=Y/横向）
- **Hook 后备分支保留**：静态指针链完全失效时仍可使用 `coordHookStoreAddr` 缓存的 rcx
- **冻结定时器三分支**：`CoordFreezeTick` 适配 指针链 / Float(Double对象) / Double(相机校正) 三种模式
- 详细移植记录见 `E:\ceshi\6\AAA_EXTRACT_REPORT.md` 第十章
- 后续优化项见 `E:\ceshi\banyi\下一步优化计划.md`

**最近完成（v38.0）：**
- **坐标读写改为 Frida Interceptor Hook 方案**：废弃失效的静态指针链，改为在 AOB 命中处挂 Interceptor 抓 rbx 基址，通过 `coord_ptr.txt` 回传给 C#
- **坐标数据类型从 Float 改为 Double**：CT 表确认坐标为 Double(8字节)，X=+0x340/Z=+0x348/Y=+0x350
- **传送启用开关**：默认关闭传送功能，需勾选"启用传送"才能使用，防止误操作
- **R/RH 双客户端支持**：记录注入时的目标进程 PID，坐标操作精确定位到被注入的客户端
- **游戏安全说明**：本工具仅适用于半单机模式，线上模式使用内存类功能会被封禁

**历史完成（v37.0）：**
- **内嵌注入**：不再依赖 Extreme Injector，直接用 Windows API（`CreateRemoteThread + LoadLibraryA`）
- **短暂 Hook + Detach**：启动时短暂 Hook 捕获 `lua_State`，成功后立即解除 Hook
- **共享内存双向通信**：优先走共享内存，不可用时降级文件通信
- **去掉 `send()`**：避免 Frida 命名管道检测
- **Frida 字符串替换**：`core.dll` 中 1892 处特征字符串已替换
- **Buff 随机延迟**：所有 Buff 操作间隔 0.5-2 秒随机延迟
- **日志缓冲写入**：3秒批量刷新，减少IO频率

### 接手前先知道什么

如果你是另一个 AI，接手这个项目前先记住下面几点：

1. **权威主源码是** `banyi/FridaGMTool.NetFx.cs`  
   - UI、注入、随机通信配置、就绪检测、Lua 构造器都在这里。
2. **真实 payload 脚本是** `FridaGM/gm_payload.js`  
   - `banyi/gm_payload.js` 与它当前内容一致，但发布/运行时实际使用的是 `FridaGM/` 目录内那份。
3. **运行目录非常重要**  
   - `ToolDir = AppDomain.CurrentDomain.BaseDirectory`，也就是程序实际运行目录。
   - `core.dll`、`core.config`、`gm_payload.js`、`buff_config.txt`、`svc.cfg` 都围绕运行目录工作。
4. **共享内存名不是固定值**  
   - 代码里有默认值 `Global\\WinSvcSharedMem`，但注入前会被随机名覆盖。
   - 真正运行时要看 `svc.cfg` 第一行。
5. **就绪判断不是只看一个 ready 文件**
   - 当前主判断条件是日志中同时出现 `Ready. Mode:` 和 `CAPTURED L=`。
6. **注入成功 ≠ 立即可执行 Lua**
   - 只有当游戏自然触发过一次 `lua_pcallk`，payload 才能捕获到 `L`。
   - 若只看到 Hook 已附加但没有 `CAPTURED L=`，通常要在游戏内再触发一次 Lua 相关行为。
7. **文档里的很多"功能"本质上是 Lua builder**
   - 不同按钮只是调用不同的 `Build*Lua(...)` 分发分支。
   - 先看 builder，再看按钮本身。
8. **游戏有半单机模式，线上模式禁止使用内存功能**
   - 本工具的所有内存类功能（传送、坐标读写、Hook 等）仅适用于半单机模式。
   - 线上模式使用这些功能会被封禁，务必注意。
   - Lua GM 命令类功能（无敌、Buff 等）在两种模式下均有风险，需自行判断。
9. **游戏有 R 和 RH 两个客户端**
   - 进程名均为 `yysls.exe`，AOB 特征码相同（坐标 AOB: `C5 F8 11 83 40 03 00 00 C4`）。
   - 两个客户端同时运行时，注入和坐标操作必须精确定位到正确的进程（通过记录注入时的 PID）。
   - CT 表参考：`E:\ceshi\reference\定怪加速\定所有怪R.CT`（R版）、`定所有怪RH.CT`（RH版）。

### 建议先看的源码

| 顺序 | 文件 | 作用 |
|------|------|------|
| 1 | `E:\ceshi\banyi\FridaGMTool.NetFx.cs` | 主程序；包含 UI、注入、部署、状态机、Lua 命令构造 |
| 2 | `E:\ceshi\banyi\FridaGM\gm_payload.js` | 注入后执行引擎；AOB 扫描、短暂 Hook、Lua 执行、通信回写 |
| 3 | `E:\ceshi\banyi\FridaGM\buff_config.txt` | Buff 分类外部配置；改 ID 首先看这里 |
| 4 | `E:\ceshi\banyi\buff分类说明.md` | Buff 分类说明与按钮语义 |
| 5 | `E:\ceshi\7\yylaoliucn\yylaoliu_decoded\stealth_all_base64_decoded.lua` | 国服老六参考实现；部分 API 与逻辑来源 |
| 6 | `E:\ceshi\banyi\PackTool.cs` | 单文件打包、VMProtect/ConfuserEx 串联、发布过滤规则 |

### 源码模块速览

`FridaGMTool.NetFx.cs` 里最关键的是这几块：

- **路径与运行目录**
  - `ToolDir` / `WorkDir`
  - `ConfigFile`
  - `BuffConfigFile`
- **随机通信配置**
  - `GenerateRandomCommConfig()`
  - `UpdateCommPaths()`
  - `svc.cfg`
- **注入链路**
  - `BtnInject_Click_B(...)`
  - `InternalInjectDll(...)`
  - `BuildGadgetConfig()`
- **就绪状态**
  - `DiagnoseInjectionResult()`
  - `IsConnectorReadyFromLog()`
  - `SyncReadyState(...)`
- **Lua builder**
  - `BuildLoopFeatureLua(...)`
  - `BuildCombatExperimentLua(...)`
  - `BuildYyLaoLiuLua(...)`
  - `BuildGameFeatureLua(...)`
  - `BuildOutfitListLua(...)`
  - `BuildOutfitApplyLua(...)`
- **单文件部署 / 伪装运行**
  - `PerformAntiDetectDeployment()`
  - `SpawnRandomCopyAndExit()`
  - `ExtractPayload(...)`

### 界面分区（v38.2，4 个标签页）

界面不是按“底层模块”分的，而是按功能页分的。窗口尺寸 514×530，标签栏末尾有「置」置顶按钮（绿色框表示当前置顶）。

- **启动**（原「快速启动」，已删除选择目录/启动游戏/刷新状态按钮）
  - 第一行：注入工具、工具目录、打开日志、清除日志
  - 第二行：状态
  - 第三行：初始化内存、启用内存功能
  - 下面：公告
- **功能**（合并了原 Buff 标签页内容）
  - 无敌、隐身、NPC 变笨、超级闪避、一击必杀、自动拾取、一键恢复
  - 节奏游戏、象棋秒赢、投壶圈变大、终止动画、关闭标志
  - Buff 施加：攻击 Buff、防御 Buff、最小 Buff、采集 Buff、辅助 Buff、未知 Buff、移除全部 Buff、老六移除 Buff
  - 循环功能（再点停止）：强力 Buff、防御 Buff、自动拾取、自动恢复
  - 剧情速度
- **传送**（独立标签页）
  - 左侧：坐标列表（双击选中不自动传送，选中项滚动到中间）
  - 右侧：保存到桌面、传送到选中、刷新、上一条、下一条
  - 坐标输入：X/Y/Z 手动输入、保存到桌面
  - 启用内存功能：默认关闭，勾选后才能执行传送操作
  - 热键：F11 传送到选中、左/右箭头切换并传送上一条/下一条（循环）
- **测试**（合并了原「工具」标签页内容）
  - 资源控制：无限体力、无限潜水、清空战斗、恢复体力
  - 速度 / 倍率：攻击倍率（x2/x4/x8）、攻击速度（x1.5/x3/x5/x7.5/x10/x30）

### 功能与 builder 对应关系

如果你要改功能，优先按下面的映射找：

- **Buff / 循环 / 剧情速度 / 攻击倍率**
  - 看 `BuildLoopFeatureLua(...)`
- **无敌 / 隐身 / 一击必杀 / 攻击速度 / 终止过场动画 / 资源类**
  - 看 `BuildCombatExperimentLua(...)`
- **自动拾取 / 一键恢复 / NPC 变笨 / 老六相关**
  - 看 `BuildYyLaoLiuLua(...)`
- **节奏游戏 / 象棋 / 投壶**
  - 看 `BuildGameFeatureLua(...)`
- **换皮列表与应用**
  - 看 `OutfitIds`、`OutfitNames`、`BuildOutfitListLua(...)`、`BuildOutfitApplyLua(...)`

### 控件 / builder / mode 入口索引

这张表比“功能说明”更适合接手排查或改功能，能直接追到发送入口：

| UI页签 | 控件/动作 | builder / 入口 | mode / action | 备注 |
|------|------|------|------|------|
| 启动 | 注入工具 | `BtnInject_Click_B(...)` | - | 生成 `svc.cfg`、创建 MMF、注入 `core.dll` |
| 启动 | 工具目录 | `BtnBrowse_Click(...)` | - | 选择/打开运行目录 |
| 启动 | 打开日志 | `BtnOpenLog_Click(...)` | - | 打开当前随机日志 |
| 启动 | 清除日志 | `BtnClearLog_Click(...)` | - | 清理历史日志文件 |
| 启动 | 初始化内存 | `BtnInitMem_Click(...)` | - | 启用内存功能前置 |
| 启动 | 启用内存功能 | `chkEnableMem` | - | 勾选后才能使用传送等内存功能 |
| 功能 | 无敌 | `BuildCombatExperimentLua(...)` | `god` / `god_off` | 复选框切换 |
| 功能 | 隐身 | `BuildCombatExperimentLua(...)` | `invis` / `invis_off` | 复选框切换 |
| 功能 | NPC变笨 | `BuildYyLaoLiuLua(...)` | `yy_npcdumb` / `yy_npcdumb_off` | 复选框切换 |
| 功能 | 超级闪避 | `BuildCombatExperimentLua(...)` | `super_dodge` / `super_dodge_off` | 复选框切换 |
| 功能 | 一击必杀 | `BuildCombatExperimentLua(...)` / `BuildLoopFeatureLua(...)` | `onehit` / `onehit_off` | 开启与还原走不同 builder |
| 功能 | 自动拾取 | `BuildLoopFeatureLua(...)` | `loot_once` | 一次性 |
| 功能 | 一键恢复 | `BuildYyLaoLiuLua(...)` | `yy_recover` | 一次性 |
| 功能 | 节奏游戏 | `BuildGameFeatureLua(...)` | `rhythm_game` | 原“辅助”页功能已迁移 |
| 功能 | 象棋秒赢 | `BuildGameFeatureLua(...)` | `chess_win` | 原“辅助”页功能已迁移 |
| 功能 | 投壶圈变大 | `BuildGameFeatureLua(...)` | `pitch_pot_easy` | 原“辅助”页功能已迁移 |
| 功能 | 终止动画 | `BuildCombatExperimentLua(...)` | `cutscene_kill` | 原“终止过场动画” |
| 功能 | 关闭标志 | `BuildLoopFeatureLua(...)` | `stealth_flags` | 原“关闭安全标志” |
| 功能 | 攻击Buff | `BuildCombatExperimentLua(...)` | `atkbuff_combo` | 当前不是 `BuildLoopFeatureLua` |
| 功能 | 防御Buff | `BuildCombatExperimentLua(...)` | `defbuff` | 当前不是 `BuildLoopFeatureLua` |
| 功能 | 最小Buff | `BuildLoopFeatureLua(...)` | `minimal_buff` | 默认 `{30372,70063,1053070}` |
| 功能 | 采集Buff | `BuildLoopFeatureLua(...)` | `gather_buff` | 读 `buff_config.txt` |
| 功能 | 辅助Buff | `BuildLoopFeatureLua(...)` | `aux_buff` | 读 `buff_config.txt` |
| 功能 | 未知Buff | `BuildLoopFeatureLua(...)` | `unknown_buff` | 读 `buff_config.txt` |
| 功能 | 移除全部Buff | `BuildLoopFeatureLua(...)` | `remove_all_buffs` | 当前主移除入口 |
| 功能 | 老六移除Buff | `BuildYyLaoLiuBuffToolLua(...)` | `yy_remove_buffs` | 老六脚本系独立入口 |
| 功能 | 强力Buff | `BuildLoopFeatureLua(...)` | `loop_buff` | 循环（原“循环强力Buff”），再点停止 |
| 功能 | 防御Buff(循环) | `BuildLoopFeatureLua(...)` | `loop_defense` | 循环（原“循环防御Buff”），再点停止 |
| 功能 | 自动拾取(循环) | `BuildLoopFeatureLua(...)` | `loop_loot` | 循环（原“循环自动拾取”），再点停止 |
| 功能 | 自动恢复(循环) | `BuildLoopFeatureLua(...)` | `loop_recover` | 循环（原“循环自动恢复”），再点停止 |
| 功能 | 剧情速度 | `ApplyDialogSpeedSelection()` → `BuildLoopFeatureLua(...)` | `dialog_speed_<值>` | 输入框生成动态 mode |
| 功能 | 还原剧情速度 | `BuildLoopFeatureLua(...)` | `dialog_speed_reset` | 固定还原 |
| 传送 | 坐标传送 | `WriteMemCoord(...)` | - | v38.1 起改为静态指针链方案（直接玩家对象 Double 写入），Hook 后备；v38.0 曾用 Frida Hook + Double |
| 传送 | 读取坐标 | `ReadMemCoord(...)` | - | v38.1 起优先静态指针链读 Double 玩家坐标；Hook 后备时按 slot 区分 Float/Double+相机校正 |
| 传送 | 初始化Hook | `InitCoordHook()` | - | 读取 `coord_ptr.txt` 获取 Frida 分配的 rbx 指针地址 |
| 传送 | 验证AOB | `VerifyCoordAOB()` | - | 仅验证 AOB 唯一命中，不读坐标 |
| 传送 | 启用内存功能 | `chkEnableMem` | - | 默认关闭，勾选后才能执行传送操作（原“启用传送”） |
| 传送 | 传送到选中 | `BtnTeleportSelected_Click(...)` | - | F11 热键也走此入口 |
| 传送 | 上一条/下一条 | `BtnPrev_Click/BtnNext_Click` | - | 左/右箭头热键切换并传送（循环） |
| 传送 | 记录坐标 | `BuildLogPositionLua()` | `log_pos` | 已从构造函数抽出独立 builder |
| 测试 | 无限体力 | `BuildCombatExperimentLua(...)` | `stamina_lock` | 原“锁体力消耗” |
| 测试 | 无限潜水 | `BuildCombatExperimentLua(...)` | `stamina_dive` | 原“无限潜水资源” |
| 测试 | 清空战斗 | `BuildCombatExperimentLua(...)` | `stamina_empty` | 原“清空战斗资源” |
| 测试 | 恢复体力 | `BuildCombatExperimentLua(...)` | `stamina_reset_all` | 原“恢复体力设置” |
| 测试 | 攻击倍率 | `ApplyAttackMultiplierSelection()` → `BuildCombatExperimentLua(...)` | `atk_mul_2/4/8` | 下拉框生成 mode |
| 测试 | 还原攻击倍率 | `BuildCombatExperimentLua(...)` | `atk_mul_reset` | 还原 `do_direct_damage` |
| 测试 | 攻击速度 | `ApplyAtkSpeedSelection()` → `BuildCombatExperimentLua(...)` | `atk_speed_1.5/3/5/7.5/10/30` | 下拉框生成 mode |
| 测试 | 还原速度 | `BuildCombatExperimentLua(...)` | `atk_speed_reset` | 原“还原攻击速度” |
| 换装 | 列表生成 | `BuildOutfitListLua(...)` | `outfit_picker` | 依赖 `OutfitIds` / `OutfitNames` |
| 换装 | 应用换装 | `BuildOutfitApplyLua(...)` | `outfit_apply_<id>` | 使用 `main_player:add_buff(outfit_id)` |

### 通信与就绪机制

- **优先通信方式**：共享内存
- **降级通信方式**：文件轮询
- **配置文件**：`svc.cfg`
  - 第 1 行：共享内存名
  - 第 2 行：命令文件名
  - 第 3 行：结果文件名
  - 第 4 行：日志文件名
- **共享内存布局**：前 128KB 写命令，后 128KB 写结果
- **就绪关键字**：
  - `Ready. Mode:`
  - `CAPTURED L=`
- **常见未就绪原因**：
  - 签名扫描失败
  - payload 未读到 `svc.cfg`
  - `core.config` / `gm_payload.js` 路径不可访问
  - 游戏尚未触发可捕获的 `lua_pcallk`

### 运行形态

有两种运行形态：

1. **开发/发布目录直跑**
   - 直接在 `FridaGM/` 目录放 `app.exe`、`core.dll`、`core.config`、`gm_payload.js` 等文件运行。
2. **单文件版运行**
   - 先从单文件产物里提取内容到伪装目录。
   - 默认优先部署到 `D:\ProgramData\Microsoft\Windows\INetCache\IE\Content.IE5\...`
   - 然后把主程序随机改名，再通过桌面快捷方式运行。
   - 启动后还会再复制一个随机系统风格名字的 exe 运行，尽量避免固定进程名暴露。

### 下一步重点（按优先级，详见“12. 下一步路线图”）
1. 🔴 **自编译 Frida**：从源码编译 Frida，彻底替换所有特征
2. 🟡 **硬件断点 Hook**：使用 Dr0-Dr3 寄存器，不修改代码段
3. 🟡 **延迟 ErasePE**：在 Frida 完全 detach 后再擦除 PE 头
4. 🟢 **patchStrings优化**：实现安全的异步内存扫描

### 关键路径速查
- 编译：见下文“编译命令”（注意 Git Bash 下需 `MSYS_NO_PATHCONV=1` + 反斜杠路径）
- 游戏进程名：`yysls.exe`
- 运行目录：程序实际运行目录就是 `ToolDir`
- 注入必要文件：`core.dll`、`core.config`、`gm_payload.js`
- 运行时通信配置：`svc.cfg`
- Buff 外部配置：`buff_config.txt`
- 就绪日志：运行时随机日志文件名
- 加新按钮/功能：优先改 `FridaGMTool.NetFx.cs` 的字段声明、按钮创建、tab 布局、对应 builder 分发

---

## 2. 工具定位

通过 Frida 框架注入游戏进程，调用游戏内部 Lua 5.4 引擎执行 GM 命令的 Windows 桌面工具。

**技术路线：** 内嵌注入 → `core.dll`（Frida Gadget，已替换字符串） → Gadget 自动加载 `gm_payload.js` → 短暂 Hook `lua_pcallk` 入口捕获 `lua_State` → 共享内存通信 → 执行 Lua 命令

> 📄 本工具属于总项目的「7号 Lua 功能分区」。

---

## 3. 当前目录结构（FridaGM 工具范围）

```
E:\ceshi\
├── 项目文档.md                           # 项目总览文档
├── FridaGMTool_单文件版_整理后.exe       # 当前测试通过的单文件产物
│
└── banyi\                               # 当前开发主目录
    ├── GM工具开发文档.md                # FridaGM 工具专项文档
    ├── FridaGMTool.NetFx.cs             # v37.0 主源码（改功能优先看这里）
    ├── PackTool.cs                      # 单文件打包器源码
    ├── PackTool.exe                     # 单文件打包器可执行文件
    ├── gm_payload.js                    # 开发侧镜像脚本
    ├── buff分类说明.md                  # Buff 分类说明（开发侧）
    ├── config.txt                       # 本地路径配置
    ├── protect_app.vmp                  # VMProtect 项目文件
    └── FridaGM\                         # 当前运行目录（可直接运行）
        ├── app.exe                      # 编译出的主程序
        ├── app_protected.exe            # VMProtect 保护后的主程序
        ├── core.dll                     # Frida Gadget（已替换字符串）
        ├── core.config                  # 自动加载配置（与 `core.dll` 同名）
        ├── gm_payload.js                # payload 脚本（v37.0）
        └── buff_config.txt              # Buff 外部配置
```

---

## 4. 防检测机制

### 内嵌注入

- 不依赖 Extreme Injector 等公开注入器
- 使用标准 Windows API：`OpenProcess` → `VirtualAllocEx` → `WriteProcessMemory` → `CreateRemoteThread(LoadLibraryA)`
- 无注入器进程特征

### 短暂 Hook 与 Detach

- 启动时短暂 Hook `lua_pcallk` 捕获 Lua 状态指针
- 捕获成功后立即 `Interceptor.detach()`，不持续修改代码段
- Hook 只存在几毫秒

### 共享内存通信

- C# 端创建共享内存（256KB，名称运行时随机生成并同步给 payload）
- 命令传递：C# 写入 → Frida 读取（前 128KB）
- 结果传递：Frida 写入 → C# 读取（后 128KB）
- 正常工作时优先走共享内存，MMF 不可用时自动降级为文件通信

### Frida 字符串替换

- 当前发布目录直接使用已预替换字符串的 `core.dll`
- `frida` → `xrkza`，`gum` → `sys`，`v8` → `r8` 等
- 同长度替换，不影响 DLL 结构

### 其他防检测策略

- 去掉`send()`调用，避免Frida命名管道检测
- Buff操作随机延迟（0.5-2秒）
- 日志缓冲写入（3秒批量刷新）
- 轮询间隔1.5-2秒
- 随机chunk名伪装脚本名

---

## 5. 共享内存布局

```
偏移量          大小      用途
0-3            4字节     命令seq（C#递增）
4-7            4字节     命令长度
8-131071       131064字节 命令内容（UTF8）
131072-131075  4字节     结果seq（Frida递增）
131076-131079  4字节     结果长度
131080-262143  131064字节 结果内容（UTF8）
```

说明：
- 整体 MMF 大小固定 `262144` 字节（256KB）。
- 前半区 `0-131071` 为命令区，后半区 `131072-262143` 为结果区。
- 每半区都先写 `seq + len` 8 字节头，再写 UTF-8 正文。

---

## 6. 主要功能

### 战斗与状态

- 无敌
- 隐身
- NPC变笨
- 超级闪避
- 一击必杀（换装ID: 400148）
- 攻击速度 x1.5/x3/x5/x7.5/x10/x30
- 还原速度
- 攻击倍率 x2/x4/x8
- 还原攻击倍率
- 无限体力 / 无限潜水 / 清空战斗 / 恢复体力

### Buff 系统

- 防御buff（158种）
- 辅助buff（9种）
- 攻击buff（5种）
- 收集buff（45种）
- 未知buff（16种）
- 自动buff循环
- 配置文件自定义buff
- Buff随机延迟（0.5-2秒）

### 换装系统

- 当前内置 122 种换装ID
- 按分类显示（特效、变身、NPC等）
- 自定义换装ID输入
- 还原默认外观

### 剧情控制

- 剧情速度x20/x80
- 还原剧情速度
- 终止动画

### 辅助与小游戏

- 自动拾取
- 一键恢复
- 自动开箱收集
- 记录坐标 / 坐标传送
- 节奏游戏
- 象棋秒赢
- 投壶圈变大

---

## 7. 编译与发布

### 编译命令

```bash
# 注意：Git Bash 下需 MSYS_NO_PATHCONV=1 防止 /target 等参数被转成路径
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /platform:x64 /out:"E:\ceshi\banyi\FridaGM\app.exe" "E:\ceshi\banyi\FridaGMTool.NetFx.cs"
```

### 单文件发布（防检测部署版）

工具支持把 `FridaGM/` 下所有文件打包成一个独立 exe，分发时只需这一个文件。

#### 阶段一：准备 VMProtect 输入文件并自动加壳

```bash
Set-Location "E:\ceshi\banyi"; .\PackTool.exe --prepare-vmprotect
```

行为：
1. 编译 `FridaGMTool.NetFx.cs` 生成 `app.exe`。
2. 复制到 `E:\ceshi\banyi\FridaGM\app.exe`。
3. 若已配置 VMProtect Ultimate 命令行（`VMProtect_Con.exe`）且存在 `protect_app.vmp`，则自动调用生成 `app_protected.exe`；否则提示手动用 VMProtect GUI 处理。

#### 阶段二：打包成单文件版

```bash
Set-Location "E:\ceshi\banyi"; .\PackTool.exe --pack-final
```

行为：
1. 编译主程序。
2. 自动调用 **ConfuserEx** 混淆该主程序（字符串加密、控制流混淆、反 ILDasm）。
3. 检测 `FridaGM\app_protected.exe`，存在则用它作为内部 `app.exe` 打包；否则使用第 2 步混淆后的主程序。
4. 打包 `FridaGM/` 下全部文件，输出 `E:\ceshi\FridaGMTool_单文件版.exe`。

> 保护层级：外层 stub 经 ConfuserEx 混淆，内部 `app.exe` 经 VMProtect 加壳。

#### Frida 字符串替换（当前状态）

当前发布目录直接使用**已预替换字符串**的 `core.dll`，无需再单独执行脚本。

### 发布文件清单

| 文件 | 大小 | 作用 |
|------|------|------|
| `app.exe` | ~230KB | 目录直跑时的主程序 |
| `app_protected.exe` | ~1.1MB | 经 VMProtect 处理后的内部主程序 |
| `core.dll` | ~23MB | Frida Gadget（已替换字符串） |
| `core.config` | 61B | 自动加载配置 |
| `gm_payload.js` | ~21KB | Hook + 共享内存脚本 |
| `buff_config.txt` | ~3.5KB | Buff 外部配置 |
| `config.txt` | 8B | 游戏路径配置 |
| `buff分类说明.md` | ~7KB | Buff 分类说明文档 |

### 运行时生成物

以下文件多数由程序在运行期创建或清理，排障时很重要：

| 文件 | 位置 | 作用 | 生命周期 |
|------|------|------|----------|
| `svc.cfg` | `ToolDir` | 保存随机 MMF 名、命令文件名、结果文件名、日志文件名 | 注入前生成，退出时清理 |
| `coord_ptr.txt` | `ToolDir` | Frida 分配的 rbx 基址指针地址（十六进制字符串） | payload 启动时生成，退出时清理 |
| 随机 `.dat` 命令文件 | `ToolDir` | 文件轮询降级时的命令输入 | 运行期生成，退出时清理 |
| 随机 `.dat` 结果文件 | `ToolDir` | 文件轮询降级时的结果输出 | 运行期生成，退出时清理 |
| 随机 `.log` 日志文件 | `ToolDir` | 当前主日志；ready 诊断依赖它 | 运行期生成，可手动保留排障 |
| `Windows系统服务.lnk` | 桌面 | 单文件部署后的启动入口 | 首次部署时创建 |

补充说明：
- `ToolDir = AppDomain.CurrentDomain.BaseDirectory`，以上路径都围绕程序实际运行目录。
- 目录直跑时，`ToolDir` 通常就是 `E:\ceshi\banyi\FridaGM\`。
- 单文件版部署后，`ToolDir` 会变成伪装缓存目录，而不是根开发目录。
- 历史兼容文件名与旧路径说明已下沉到文末 `附录 / 历史研究`。

### 使用流程

```
1. 运行 `FridaGMTool_单文件版.exe`
2. 首次运行时执行安全部署：文件提取到伪装目录、主程序随机命名、创建桌面快捷方式
3. 后续从桌面快捷方式启动，程序可能再拉起一个随机系统风格名称的副本进程
4. 启动游戏 → 点“注入”（内嵌注入，无 Extreme Injector）
5. 进入游戏并触发一次 Lua 相关调用 → 工具检测到 `Ready. Mode:` + `CAPTURED L=` 后可用
```

## 8. Buff 配置与 ID 清单

### 外部配置机制

`buff_config.txt`（工具同目录）按分类配置，格式 `分类 = {id1, id2, ...}`，**每分类必须单行**。改后重新点按钮即可生效，不需重新编译。行首 `#` 或 `--` 为注释。

分类：`MINIMAL`、`AUTO`、`ATTACK`、`DEFENSE`、`LOOP_STRONG`、`GATHER`、`AUX`、`UNKNOWN`、`COMBAT_SKILL`、`SCROLL`、`ATTR`、`PERMANENT`、`REMOVE`。

> Buff ID 详细分类与中文说明见 `E:\ceshi\banyi\buff分类说明.md`；国际服 6817 条全量字典见 `Where Winds Meet  Lua Script/05_主数据库/`，速查见 `Buff_ID参考.md`。

### 常用 ID 速查

- **最小Buff (MINIMAL)**：30372(无敌减90%真伤)、70063(满状态恢复)、1053070(伤害提升，国际服验证的秒杀级)
- **攻击倍率**：1053017(x2)、1053018(x4)、1053019(x8)
- **防御/无敌**：30372、30310、70184、200071、200086、30366、200031(GM无敌)
- **采集 (GATHER)**：104002-104007、104013-104051 系列（约45个）
- **一击必杀**：400148（换装）

---

## 9. 核心技术链路

### Lua 执行链
1. 内嵌注入 `core.dll` 到游戏进程
2. Frida 短暂 Hook `lua_pcallk` 入口，捕获 `lua_State *L`
3. 立即 `Interceptor.detach()`，不持续修改代码段
4. 通过共享内存接收Lua命令
5. 调用 `luaL_loadbufferx` 编译（用随机 chunk 名伪装）
6. 调用 `lua_pcallk` 执行
7. 结果写回共享内存

### 关键 Lua API 定位

- 通过 AOB 特征码在游戏主模块内动态扫描 Lua 入口，不依赖固定 RVA。
- 捕获的入口为 `lua_pcallk`（6 参数版本），用于执行 `luaL_loadbufferx` 编译后的代码。

### 坐标读写链路（v38.1，当前主链路）

1. C# 端调用 `ResolveCoordBase(hProcess, out ptr2)` 解析坐标基址
2. `ResolveCoordBase` **优先**调用 `ResolveCoordBaseByPtrChain()`：
   - Step 1：`ReadInt64(yysls_base + STATIC_OFFSET_DEFAULT=0x083F46D8)` → P1
   - Step 2：`ReadInt64(P1 + 0x58)` → P2
   - Step 3：`ReadInt64(P2 + 0x00)` → OBJ（玩家对象基址）
   - 校验：P1 必须是堆地址、OBJ 在合理范围、`OBJ+0x340` 的 Double X 在 ±100000 内
3. 若硬编码偏移失效（P1=0 或校验失败）且尚未扫描过，自动调用 `AOBScanStaticOffset()`：
   - 读取 yysls.exe 前 64MB 代码段
   - 搜索 `48 8B/8D 05/0D/15/1D/25/2D/35/3D`（`mov/lea r64,[rip+disp32]`）指令
   - 统计每个被引用偏移的出现次数，按引用次数降序
   - 对引用≥3次的候选，跑完整指针链 + 坐标合理性验证
   - 命中后缓存到 `cachedStaticOffset`，避免重复扫描
4. `lastResolveWasPtrChain = true` 标记本次解析来源
5. `ReadMemCoord`：指针链方案直接 `ReadDouble(OBJ + 0x340/0x348/0x350)` 读 X/Z/Y，**跳过相机偏移校正**
6. `WriteMemCoord`：直接 `WriteDouble` 写玩家坐标；启动 16ms 定时器冻结约 1 秒（60 帧），并设置 `lockCoordIsPtrChain = lastResolveWasPtrChain`
7. `CoordFreezeTick`：根据 `lockCoordIsPtrChain` 走对应分支反复写入

### 坐标读写链路（v38.0，Hook 后备链路）

仅当静态指针链完全失效时启用（`lastResolveWasPtrChain = false`）：

1. `gm_payload.js` 启动时，AOB 扫描坐标写指令 `C5 F8 11 83 40 03 00 00 C4`（`vmovups [rbx+0x340],xmm0`）
2. 在命中地址安装 `Interceptor.attach`，`onEnter` 中读取 `this.context.rbx`
3. 检查 `[rbx+0x54]==0`（过滤本人对象，CT 表中 `cmp [rbx+54],0` 逻辑）
4. 匹配时将 rbx 写入 `g_coordBaseMem`（Frida `Memory.alloc(8)` 分配的 8 字节内存）
5. 将 `g_coordBaseMem` 地址写入 `coord_ptr.txt`
6. C# 端点击"初始化Hook" → 读取 `coord_ptr.txt` → 打开游戏进程 → `ReadInt64` 读 rbx 值
7. 后续读取坐标：`ReadDouble(rbx+0x340)` / `ReadDouble(rbx+0x348)` / `ReadDouble(rbx+0x350)` → X/Z/Y
8. 传送：`WriteDouble` 写入坐标 + 启动 16ms 定时器冻结约 1 秒（60 帧），防止引擎覆盖
9. 数据类型：**Double（8 字节）**，非 Float（4 字节）

> ⚠️ 坐标内存功能**仅适用于半单机模式**，线上模式使用会被封禁。
> ⚠️ R 和 RH 两个客户端进程名均为 `yysls.exe`，AOB 相同；同时运行时需通过注入时记录的 PID 精确定位。

### 循环机制
- 状态变量：`_G.__GM_TOOL_<name>_ACTIVE` / `_ACTION`
- 执行引擎：`cc.Director:getInstance():getRunningScene():runAction(cc.RepeatForever(cc.Sequence(cc.DelayTime(n), cc.CallFunc(fn))))`
- 再点击发送停止逻辑

### 加新功能/按钮（改 `FridaGMTool.NetFx.cs` 4 处）
1. 字段声明（如 `Button btnXxx;`）
2. 按钮创建 + `.Click` 绑定 `SendExperimentCommand("名", BuildXxxLua("mode"))`
3. `place()` 布局把按钮放进对应标签页
4. `BuildLoopFeatureLua`（或对应 builder）里加 `else if (mode == "xxx")` 分发 + 对应 Lua 函数

## 10. 已知问题

| 功能 | 状态 / 原因 |
|------|------|
| ErasePE导致崩溃 | 擦除core.dll的PE头会导致Frida内部状态损坏；内嵌注入中已注释掉该代码 |
| HideModule失败 | Frida gadget不在标准PEB链表中，无法通过PEB摘链隐藏；内嵌注入中已禁用 |
| patchStrings禁用 | 异步内存扫描可能导致崩溃，已改用预替换core.dll |

---

## 11. 故障排查矩阵

| 症状 | 先看哪里 | 预期现象 | 最可能原因 |
|------|----------|----------|------------|
| 点击“注入”后无反应 | `svc.cfg`、当前随机日志 | `svc.cfg` 已生成，日志开始写入 | `core.dll` / `core.config` / `gm_payload.js` 未就位 |
| 日志有 `Ready. Mode:` 但没有 `CAPTURED L=` | 当前随机日志 | 已附加但未捕获 Lua 状态 | 游戏尚未自然触发一次 `lua_pcallk` |
| 只有旧 `gm_tool.log`，没有随机日志 | `svc.cfg`、`gm_tool.log` | JS 仍在旧默认路径写日志 | payload 未正确读取 `svc.cfg` |
| 注入后立即报签名失败 | 当前随机日志 | AOB 扫描失败日志 | 游戏版本变动导致 `lua_pcallk` / `luaL_loadbufferx` 特征失效 |
| 发送命令无结果 | MMF + 随机结果 `.dat` | 至少有 MMF 或结果文件回写 | MMF 打不开，且文件降级链路也异常 |
| 单文件版能部署但打不开功能界面 | 伪装部署目录、桌面快捷方式 | 目录中应有 `core.dll` / `core.config` / `gm_payload.js` | 只启动了外层包，未从快捷方式/随机副本进入实际运行形态 |

定位源码时优先看：
- 注入流程：`E:\ceshi\banyi\FridaGMTool.NetFx.cs`
- 注入诊断：`E:\ceshi\banyi\FridaGMTool.NetFx.cs`
- 随机通信配置：`E:\ceshi\banyi\FridaGMTool.NetFx.cs`
- 通信路径同步：`E:\ceshi\banyi\FridaGMTool.NetFx.cs`
- 就绪检测：`E:\ceshi\banyi\FridaGMTool.NetFx.cs`
- payload 共享内存 / ready 写回：`E:\ceshi\banyi\FridaGM\gm_payload.js`

---

## 12. 下一步路线图

| 优先级 | 任务 | 类型 | 说明与依据 |
|--------|------|------|------------|
| 🔴 高 | **自编译 Frida** | 代码 | 从源码编译 Frida，彻底替换所有特征（线程名、模块名、API模式） |
| 🟡 中 | **延迟 ErasePE** | 代码 | 在 Frida 完全 detach 后再擦除 PE 头，避免破坏 Frida 内部状态 |
| 🟡 中 | **硬件断点 Hook** | 代码 | 使用 Dr0-Dr3 寄存器，不修改代码段，更隐蔽 |
| 🟡 中 | **patchStrings优化** | 代码 | 实现安全的异步内存扫描，替换运行时内存中的特征字符串 |
| 🟢 低 | **通信改命名管道** | 代码 | 进一步消除共享内存特征 |
| 🟢 低 | **Lua payload 字符串混淆** | 代码 | `set_niubility`/`add_buff` 等明文，运行时拼接或 XOR |

---

## 13. 当前硬约束
- VMProtect 可能阻止部分 Hook，但 `lua_pcallk` Hook 已验证可用
- `core.dll`（Frida Gadget）默认使用内嵌注入
- Lua 执行必须在 `lua_pcallk` 自然调用现场
- Buff 施加用 `rpc_fake_add_buff` 或 `add_buff`，循环用 `cc.Director runAction`
- 共享内存名称运行时随机生成并同步，进程名 `yysls.exe` 写死
- 就绪检测关键字为 `Ready. Mode:` 和 `CAPTURED L=`

---

## 14. 保护层级说明

当前单文件版具备四层保护：

| 层级 | 工具 | 保护内容 |
|------|------|----------|
| .NET 混淆 | ConfuserEx | 外层 stub：字符串加密、控制流混淆、反 ILDasm、资源加密 |
| PE 加壳 | VMProtect | 内部 `app.exe`：压缩、内存保护、反调试、节区名随机化 |
| DLL 字符串替换 | 预替换 `core.dll` | 直接使用已替换 `core.dll` 中的 1892 处 Frida 特征字符串 |
| 行为隐藏 | C# 代码 | 内嵌注入、短暂 Hook、共享内存优先、去掉 `send()`、随机进程名、Buff 随机延迟 |

---

## 15. 附录 / 历史研究

### 历史兼容文件与旧路径

以下对象不再属于当前主运行链路，但在排障、兼容清理、识别旧构建残留时仍有参考价值：

| 文件 | 位置 | 历史作用 | 当前状态 |
|------|------|----------|----------|
| `gm_tool.log` | `ToolDir` 或游戏目录 | 旧默认日志名 | 非当前主路径，仅兼容旧版检测 |
| `ready.txt` / `trace.txt` / `gm_signal.txt` | `ToolDir` | 旧就绪/调试信号文件 | 当前版本会尝试清理 |
| `connector_log.txt` / `gm_tool_ui.log` / `gm_tool_all.log` | `ToolDir` | 旧日志名 / 兼容清理目标 | 当前版本会尝试清理 |

### 历史参考 RVA

- `lua_pcallk ≈ 0x48dbcd0`
- `luaL_loadbufferx wrapper ≈ 0x48deb00`
- 以上仅作为历史定位记录，当前实现以 AOB 扫描为准，不依赖固定 RVA。

### 附录A：版本迭代历史

#### 阶段一：纯 C++ dinput8.dll 方案 (v3-v7) — 已放弃
无法在游戏主线程上下文安全调用 Lua API。

#### 阶段二：Frida 框架方案 (v8-v33.0) — 关键链路打通
- v8-v13：Frida 基础框架 + Python 连接器
- v14-v26：lua_State 搜索（多种策略均失败）
- v27-v29：Hook 捕获 L + 函数验证
- v30.0-v30.7：**最小执行链打通**
- v30.8-v33.0：外部脚本执行 + 命令直调执行器

#### 阶段三：C# UI 工具 (v5.2-v11.8)
功能补全 + UI 重构 + Buff 外部配置 + 换装（122项）

#### 阶段四：C# FridaCLR 连接器 (v12.0-v12.1)
从 Python 迁移到 C# 内置连接器，但需 .NET 10 Runtime

#### 阶段五：方案B 自动加载 + .NET Framework (v13.0)
- 验证 Gadget 自动加载脚本可行（无需连接器）
- 重构为 .NET Framework 4.x 单文件 EXE（无外部 Runtime 依赖）
- 通信文件统一指向游戏目录

#### 阶段六：最小Buff + 防检测第一轮 (v13.1 / payload v34.0)
- 最小Buff（MINIMAL 分类）、关闭反作弊标志按钮
- payload 随机 chunk 名 + 轮询间隔放宽 + `lua_pcallk` AOB 扫描

#### 阶段七：注入稳定 + 部署优化 (v13.2)
- Manual Map 改 Standard，解决闪退
- 配置 StealthInject/ErasePE/HideModule/CloseOnInject/AutoInject（后续 v37.0 内嵌注入中已禁用 ErasePE/HideModule）
- 单文件版支持覆盖更新

#### 阶段八：VMProtect + ConfuserEx (v13.4-v13.5)
- VMProtect Ultimate 命令行集成
- ConfuserEx 字符串加密/控制流混淆
- 保护架构：ConfuserEx（外层 stub） → VMProtect（内部 app.exe） → 行为隐藏

#### 阶段九：内嵌注入 + 共享内存 + 字符串替换 (v35.0-v37.0)
- v35.0：信号文件机制、日志缓冲写入、轮询间隔优化
- v36.0：共享内存单向通信（C#→Frida）、文件轮询降级
- v37.0：
  - 内嵌注入（无 Extreme Injector）
  - 短暂 Hook + Detach
  - 共享内存双向通信（优先 MMF，降级文件）
  - 去掉 `send()`
  - Frida 字符串替换（1892处）
  - Buff 随机延迟（0.5-2秒）
  - 日志缓冲写入

#### 阶段十：坐标 Frida Hook 方案 + 安全增强 (v38.0)
- 废弃失效的静态指针链（`module+0x07C04698→+0x58→...`），改为 Frida Interceptor Hook 抓 rbx 基址
- 坐标数据类型从 Float(4字节) 改为 Double(8字节)
- 传送功能增加"启用传送"开关（默认关闭，防止误操作）
- 记录注入时目标进程 PID，解决 R/RH 双客户端同时运行时的进程定位问题
- 传送标签页从工具 tab 独立拆分
- 新增"初始化Hook"按钮和 `coord_ptr.txt` 通信机制
- 明确标注：内存类功能仅适用于半单机模式，线上模式使用会被封禁

#### 阶段十一：坐标静态指针链方案 (v38.1，2026-06-29)
- 从 AAA.exe 逆向提取新的静态指针链：`yysls.exe + 0x083F46D8 → +0x58 → +0x00 → OBJ`，直接获取**玩家对象**（非相机）
- 新增 `ResolveCoordBaseByPtrChain()` / `AOBScanStaticOffset()` 方法
- 修改 `ResolveCoordBase` / `ReadMemCoord` / `WriteMemCoord` / `CoordFreezeTick` 适配三分支
- 修正 Y/Z 偏移命名（旧值互换）
- Hook 方案保留为后备（`coordHookStoreAddr`），保证兼容性
- 编译通过：`E:\ceshi\banyi\FridaGM\app.exe`，255488 字节
- 详见 `E:\ceshi\6\AAA_EXTRACT_REPORT.md` 第十章

#### 阶段十二：UI 大重构 + 传送按键修复 (v38.2，2026-06-30)
- **标签页重构**：删除「工具」「Buff」标签页；4 个标签改为「启动」「功能」「传送」「测试」
- **Buff 功能转移**：原 Buff 标签页内容移入「功能」标签页的「Buff 施加」「循环功能」区段
- **工具内容转移**：原「工具」标签页内容移入「测试」标签页
- **窗口尺寸精简**：560×600 → 514×530；边距 12→6；按钮 118×28→110×26；行距 34→30；分区间距 8→4
- **置顶按钮**：标签栏末尾添加「置」按钮（默认置顶时绿色框），点击切换 TopMost
- **按钮文字精简**：快速启动→启动、NPC节奏游戏→节奏游戏、终止过场动画→终止动画、关闭安全标志→关闭标志、锁体力消耗→无限体力、无限潜水资源→无限潜水、清空战斗资源→清空战斗、恢复体力设置→恢复体力、还原攻击速度→还原速度、循环强力Buff→强力Buff（所有循环按钮去掉「循环」两字）
- **传送按键广播修复**：PostKeyPress 改为广播式，向游戏窗口+所有可见子窗口发 PostMessageW；用 AttachThreadInput 让目标窗口线程获得键盘焦点；FireTeleportKeys 增加诊断日志输出窗口标题/类名
- **传送时序对齐 AAA 逆向**：WriteMemCoord 改为后台线程 Task.Run，先 FireTeleportKeys()（SPACE↓↑→Q↓↑）再 WriteMemCoordCore()（写内存+冻结）
- **方向传送基于人物朝向**：新增 TryNudgeDirectional(forward, right, up)，读取 YAW(0x358) 角度后用 sin/cos 转换为世界坐标 dx/dy
- **飞天遁地/瞬移热键**：方向键↑飞天/↓遁地、Alt+方向键↑↓←→ 瞬移；全局低级键盘钩子 WH_KEYBOARD_LL
- **坐标列表优化**：选中项滚动到中间、双击选中不自动传送
- **传送热键**：F11 传送到选中、左/右箭头切换并传送上一条/下一条（循环）

### 附录B：经验教训 / 历史踩坑

1. 纯 C++ dinput8.dll 方案不可行 — 无法安全调用 Lua API
2. 标准 Lua 5.4 结构偏移不适用 — 游戏修改了 Lua 源码
3. 被动追踪比盲猜调用更有效 — v30.5 通过自然调用参数定位到正确入口
4. 先跑最小链路，再加载外部脚本
5. 循环功能不要依赖游戏内 ccui 菜单，用 `cc.Director runAction` 自建
6. 攻击类功能（一击/增伤）是最难突破的，Buff ID 方式效果有限
7. **`core.config` 必须在注入前就位** — gadget 只在 LoadLibrary 瞬间读 config
8. **ErasePE不能在Frida运行时执行** — 会破坏Frida内部状态导致崩溃
9. **patchStrings异步扫描可能导致崩溃** — 已改用预替换core.dll方式
10. **共享内存必须在注入前创建** — 否则Frida端无法打开
11. **HideModule对Frida gadget无效** — Frida不在标准PEB链表中
12. **Git Bash 编译要加 `MSYS_NO_PATHCONV=1`** — 否则 `/target` `/out` 等参数被转成 Windows 路径
13. **旧版本可能留下随机名 exe 副本** — 如历史构建目录中存在残留文件，打包前应清理；当前发布目录不再将其作为标准产物
14. **静态指针链不可靠** — 游戏版本更新后全局基址偏移失效（0x07C04698 已过期），必须用 Hook 运行时抓寄存器
15. **坐标是 Double 不是 Float** — CT 表确认 X/Z/Y 为 8 字节 Double；按 Float(4字节) 读会得到垃圾值
16. **R/RH 双客户端 PID 必须匹配** — 两个客户端进程名相同，坐标操作必须定位到被注入的那个进程，否则 `coord_ptr.txt` 中的地址在错误进程空间无效
17. **就绪检测字符串会变化** — 当前检测 `Ready. Mode:` 和 `CAPTURED L=`，旧的 `Ready. Command direct executor is polling:` 已废弃
18. **静态指针链可重新可用（v38.1）** — 之前 v38.0 因 0x07C04698 偏移失效而弃用静态指针链；从 AAA.exe 逆向提取的新偏移 `0x083F46D8` 已验证可用，且配套 AOB 扫描兜底
19. **Y/Z 偏移命名容易写反** — 0x348 是 Z/高度（小值约 -37），0x350 是 Y/横向（大值约 -2052）；写反会导致 UI 显示与实际坐标轴错位
20. **AOB 扫描必须配合指针链验证** — 仅统计 `mov/lea r64,[rip+disp32]` 引用次数不够，必须对候选偏移跑完整指针链解引用 + 坐标合理性检查，否则会误判到其他模块全局变量
21. **PostMessageW 必须广播到子窗口** — 游戏可能用子窗口接收键盘输入（AAA 逆向显示 hWnd=0x1b0456 是子窗口），只发到顶层 EnumWindows 找到的窗口按键不会生效；必须 EnumChildWindows + IsWindowVisible 收集所有目标，逐一 PostMessageW
22. **AttachThreadInput 可解决 PostMessageW 焦点丢失** — 把调用线程的输入队列 Attach 到目标窗口线程，目标窗口才能收到键盘事件；用完必须 Detach（finally 块），否则会卡住目标线程输入队列
23. **Edit 工具对 CRLF + 中文文件不可靠** — `GM工具开发文档.md` 用 `\r\n` 行尾且 UTF-8 无 BOM，Edit 工具匹配失败；FridaGMTool.NetFx.cs 用 `\n` 行尾且 UTF-8 with BOM，Edit 也偶发失败；改用 Python `-c` 单行替换 + `chr(34)` 表示双引号，或写 .py 脚本批量替换最稳
24. **C# 5 编译器不支持 inline `out` 变量声明** — `GetWindowThreadProcessId(hWnd, out uint _dummy)` 会报 CS1525；必须先 `uint _dummy;` 再 `GetWindowThreadProcessId(hWnd, out _dummy)`
25. **PowerShell 不支持 bash heredoc** — `<< 'PYEOF'` 语法在 PowerShell 中无效（保留给未来使用）；多行 Python 脚本必须用 Write 工具创建 .py 文件再 `python file.py` 执行
26. **WndProc 自绘标题栏按钮容易失败** — 在非客户区 WM_NCPAINT/WM_NCHITTEST 自绘按钮，渲染时机和命中测试容易出问题导致按钮不显示；改为在客户区 tabNav 末尾放普通 Button 控件更稳，且无需拦截 NC 消息
27. **传送时序必须先后台线程再发按键** — UI 线程同步 PostMessageW 后立即写内存会阻塞键盘钩子超时闪退；WriteMemCoord 改为 Task.Run 后台线程，先 FireTeleportKeys() 再 WriteMemCoordCore()
