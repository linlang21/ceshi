# 燕云十六声 FridaGM 辅助工具 — 开发文档

> **当前版本：** v37.0（C# .NET Framework 4.x 单 exe）｜payload v37.0
> **最后更新：** 2026-06-21

---

## 🚀 快速上手区（给其他工具 / 协作者）

> 不熟悉本项目的工具或人，先读这一节即可掌握全貌。

### 现在做到哪了
GM 工具核心链路**已全部打通并可用**：内嵌注入 → 短暂Hook Lua 调用入口 → 共享内存通信 → 执行 Lua。
已有 15+ 功能（无敌 / 拾取 / 变速 / 换皮 / Buff 等）。

**最近完成（v37.0）：**
- **内嵌注入**：不再依赖Extreme Injector，直接用Windows API（CreateRemoteThread + LoadLibraryA）
- **短暂Hook + detach**：启动时短暂Hook捕获lua_State，成功后立即解除Hook
- **共享内存双向通信**：优先走共享内存，不可用时降级文件通信
- **去掉send()**：避免Frida命名管道检测
- **Frida字符串替换**：core.dll中1892处特征字符串已替换
- **Buff随机延迟**：所有buff操作间隔0.5-2秒随机延迟
- **日志缓冲写入**：3秒批量刷新，减少IO频率

**最近完成（v36.0）：**
- 共享内存单向通信（C#→Frida）
- 文件轮询降级

**最近完成（v35.0）：**
- 信号文件机制
- 日志缓冲写入
- 轮询间隔优化

### 下一步要做什么（按优先级，详见 [下一步路线图](#下一步路线图）)
1. 🔴 **自编译Frida**：从源码编译Frida，彻底替换所有特征
2. 🟡 **硬件断点Hook**：使用Dr0-Dr3寄存器，不修改代码段
3. 🟡 **延迟ErasePE**：在Frida完全detach后再擦除PE头
4. 🟢 **patchStrings优化**：实现安全的异步内存扫描

### 优先查看哪些文件
| 顺序 | 文件 | 看什么 |
|------|------|--------|
| 1 | `banyi/FridaGMTool.NetFx.cs` | **主源码**（UI + 所有 Lua 命令生成），改功能都在这 |
| 2 | `FridaGM/gm_payload.js` | Frida Hook 脚本（注入后的执行引擎、防检测） |
| 3 | `FridaGM/buff_config.txt` | Buff 分类配置，改 ID 不用重新编译 |
| 4 | `FridaGM/core.dll` | Frida Gadget（已替换字符串） |
| 5 | `banyi/yylaoliu_decoded/stealth_all_base64_decoded.lua` | 国服老六脚本（防检测/GM API 参考来源） |

### 关键路径速查
- 编译：见 [编译命令](#编译命令)（注意 Git Bash 下需 `MSYS_NO_PATHCONV=1` + 反斜杠路径）
- 游戏进程名：`yysls.exe`｜通信方式：优先共享内存 `Global\WinSvcSharedMem`，保留文件通信降级
- 加新按钮/功能：改 `FridaGMTool.NetFx.cs`（4 处：字段声明、按钮创建、`place()` 布局、`BuildLoopFeatureLua` mode 分发 + Lua 函数）

---

## 项目介绍

通过 Frida 框架注入游戏进程，调用游戏内部 Lua 5.4 引擎执行 GM 命令的 Windows 桌面工具。

**技术路线：** 内嵌注入 → `core.dll`（Frida Gadget，已替换字符串） → gadget 自动加载 `gm_payload.js` → 短暂Hook `lua_pcallk` 入口捕获 `lua_State` → 共享内存通信 → 执行 Lua 命令

> 📄 本工具属于总项目的「7号 Lua 功能分区」。总览与内存功能分区见根目录 [项目文档.md](../../项目文档.md)。

---

## 目录结构

```
E:\ceshi\7\
├── banyi/                          # 开发目录（源码、文档、测试）
│   ├── FridaGMTool.NetFx.cs        # v37.0 主源码（.NET Framework 4.x）← 改功能看这
│   ├── PackTool.cs                 # 单文件打包器源码
│   ├── PackTool.exe                # 单文件打包器可执行文件
│   ├── gm_payload.js               # Frida Hook 脚本（核心 payload）
│   └── yylaoliu_decoded/           # 老六脚本解码（防检测/API 参考）
│       └── stealth_all_base64_decoded.lua
│
├── FridaGM/                        # 发布目录（可直接使用）
│   ├── app.exe                     # 编译出的主程序（通常由 PackTool 生成）
│   ├── app_protected.exe           # VMProtect保护后的主程序
│   ├── core.dll                    # Frida Gadget（已替换1892处字符串）
│   ├── core.config                 # 自动加载配置（与 core.dll 同名）
│   ├── ServiceServer.exe 等       # 运行后可能残留的随机副本 exe，建议清理
│   ├── gm_payload.js               # Hook 脚本（v37.0）
│   ├── settings.xml                # 注入器配置
│   ├── buff_config.txt             # Buff 外部配置
│   ├── buff分类说明.md              # Buff 分类说明
│   ├── config.txt                  # 游戏路径配置
│   └── GM工具开发文档.md            # 本文档副本
│
├── Where Winds Meet  Lua Script/   # 🌟 国际服论坛情报（含 6817 条 Buff CSV）
│   ├── README.md                   # 情报索引（先看）
│   ├── 国际服资料整理总结.md         # API/反作弊/注入完整分析
│   ├── Buff_ID参考.md               # Buff ID 速查
│   └── 05_主数据库/全量Buff数据库_6817条.csv
│
├── yylaoliucn/                     # 燕云老六（国服）参考项目
├── Where Winds Meet/               # 国服适配脚本参考
├── WhereWindsMeet-Lua-Injector-main/ # 国际服注入器参考
├── Test.lua-main/                  # 节奏游戏/象棋/GM训练面板参考
└── GM工具开发文档.md                 # 本文档
```

---

## 防检测机制

### 1. 内嵌注入

- 不依赖Extreme Injector等公开注入器
- 使用标准Windows API: `OpenProcess` → `VirtualAllocEx` → `WriteProcessMemory` → `CreateRemoteThread(LoadLibraryA)`
- 无注入器进程特征

### 2. 短暂Hook + Detach

- 启动时短暂Hook `lua_pcall`捕获Lua状态指针
- 捕获成功后立即`Interceptor.detach()`，不持续修改代码段
- Hook只存在几毫秒

### 3. 共享内存通信

- C#端创建`Global\WinSvcSharedMem`共享内存（256KB）
- 命令传递: C#写入 → Frida读取（前128KB）
- 结果传递: Frida写入 → C#读取（后128KB）
- 正常工作时优先走共享内存，MMF 不可用时自动降级为文件通信

### 4. Frida字符串替换

- 使用Python脚本替换core.dll中的1892处特征字符串
- `frida` → `xrkza`, `gum` → `sys`, `v8` → `r8`等
- 同长度替换，不影响DLL结构

### 5. 其他防检测

- 去掉`send()`调用，避免Frida命名管道检测
- Buff操作随机延迟（0.5-2秒）
- 日志缓冲写入（3秒批量刷新）
- 轮询间隔1.5-2秒
- 随机chunk名伪装脚本名

---

## 共享内存布局

```
偏移量          大小      用途
0-3            4字节     命令seq（C#递增）
4-7            4字节     命令长度
8-131079       128KB     命令内容（UTF8）
131072-131075  4字节     结果seq（Frida递增）
131076-131079  4字节     结果长度
131080-262143  128KB     结果内容（UTF8）
```

---

## 主要功能

### Buff系统

- 一击必杀（换装ID: 400148）
- 防御buff（158种）
- 辅助buff（9种）
- 攻击buff（5种）
- 收集buff（45种）
- 未知buff（16种）
- 自动buff循环
- 配置文件自定义buff
- Buff随机延迟（0.5-2秒）

### 换装系统

- 120+种换装ID
- 按分类显示（特效、变身、NPC等）
- 自定义换装ID输入
- 还原默认外观

### 剧情控制

- 剧情速度x20/x80
- 还原剧情速度
- 终止过场动画

### 攻击增强

- 攻击倍率x1.5/x3/x5/x7.5/x10/x30
- 还原攻击倍率

### 其他功能

- 隐身模式
- 无限耐力
- 自动回复
- 内存速度优化
- 变大/变小
- 发光特效

---

## 编译与发布

### 编译命令

```bash
# 注意：Git Bash 下需 MSYS_NO_PATHCONV=1 防止 /target 等参数被转成路径
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /platform:x64 /out:"E:\ceshi\7\FridaGM\app.exe" "E:\ceshi\7\banyi\FridaGMTool.NetFx.cs"
```

### 单文件发布（防检测部署版）

工具支持把 `FridaGM/` 下所有文件打包成一个独立 exe，分发时只需这一个文件。

#### 阶段一：准备 VMProtect 输入文件并自动加壳

```bash
Set-Location "E:\ceshi\7\banyi"; .\PackTool.exe --prepare-vmprotect
```

行为：
1. 编译 `FridaGMTool.NetFx.cs` 生成 `app.exe`。
2. 复制到 `E:\ceshi\7\FridaGM\app.exe`。
3. 若已配置 VMProtect Ultimate 命令行（`VMProtect_Con.exe`）且存在 `protect_app.vmp`，则自动调用生成 `app_protected.exe`；否则提示手动用 VMProtect GUI 处理。

#### 阶段二：打包成单文件版

```bash
Set-Location "E:\ceshi\7\banyi"; .\PackTool.exe --pack-final
```

行为：
1. 编译主程序。
2. 自动调用 **ConfuserEx** 混淆该主程序（字符串加密、控制流混淆、反 ILDasm）。
3. 检测 `FridaGM\app_protected.exe`，存在则用它作为内部 `app.exe` 打包；否则使用第 2 步混淆后的主程序。
4. 打包 `FridaGM/` 下全部文件，输出 `E:\ceshi\7\FridaGMTool_单文件版.exe`。

> 保护层级：外层 stub 经 ConfuserEx 混淆，内部 `app.exe` 经 VMProtect 加壳。

#### 替换Frida字符串（可选，推荐）

```bash
python E:\ceshi\7\FridaGM\patch_frida_strings.py
```

替换core.dll中的1892处特征字符串。

### 发布文件清单

| 文件 | 大小 | 作用 |
|------|------|------|
| `app.exe` / `app_protected.exe` | ~1.1MB | 主程序（VMProtect 加壳；外层 stub 经 ConfuserEx 混淆） |
| `core.dll` | ~23MB | Frida Gadget（已替换字符串） |
| `core.config` | 61B | 自动加载配置 |
| `gm_payload.js` | ~18KB | Hook + 共享内存脚本 |
| `settings.xml` | 2.3KB | Extreme Injector 配置文件（当前内嵌注入为主，EI 为回退） |
| `buff_config.txt` | ~3.5KB | Buff 外部配置 |
| `config.txt` | 8B | 游戏路径配置 |
| `Extreme Injector v3.exe` | ~2MB | 注入器（内嵌注入失败时回退使用） |
| `buff分类说明.md` | ~7KB | Buff 分类说明文档 |

### 使用流程

```
1. 运行 FridaGMTool_单文件版.exe
2. 确认防检测部署 → 文件自动放到随机目录
3. 启动游戏 → 点「注入」（内嵌注入，无Extreme Injector）
4. 进入游戏场景 → 工具自动检测就绪，GM 按钮可用
```

---

## 已确认有效的功能

| 功能 | 实现方式 | 说明 |
|------|----------|------|
| 无敌 | 防御 Buff | 防御 Buff 也可达到无敌 |
| 自动拾取 | `_G.RunAutoLoot` / 主玩家拾取接口 | 有效 |
| NPC变笨 | `_G.GM_EnableNPCDUMB` | 有效 |
| 超级闪避 | `_G.ToggleSuperDodge` | 有效 |
| 一键恢复 | Buff 补挂 | 有效 |
| 内存加速 | 进程内存偏移链写 float | 有效 |
| Buff 施加 | `rpc_fake_add_buff` / `add_buff` | 有效 |
| 循环功能 | `cc.Director runAction` + 状态变量 | 有效 |
| 传送 | `_G.RunRandomTP` | 有效 |
| 换皮 | `main_player:add_buff(outfit_id)` | 124 项 |
| 剧情速度 | `dialog_global_time_scale` | 有效 |
| 攻击倍率 | Buff ID 1053017/1053018/1053019 | x2/x4/x8 |
| 自动开箱收集 | AOI 实体搜索 + interact | 有效 |
| NPC节奏游戏 | `gm_combat.auto_rhythm` | 有效 |
| 象棋秒赢 | `gm_combat.chess_instant_win` | 有效 |
| **最小Buff** | `MINIMAL` 分类，默认 `{30372,70063,1053070}` | 单次施加 |
| **一击必杀** | 换装ID 400148 | 已验证有效 |
| **还原一击必杀** | `remove_buff_full` | 使用确认有效的API |

---

## Buff 配置与 ID 清单

### 外部配置机制

`buff_config.txt`（工具同目录）按分类配置，格式 `分类 = {id1, id2, ...}`，**每分类必须单行**。改后重新点按钮即可生效，不需重新编译。行首 `#` 或 `--` 为注释。

分类：`MINIMAL`、`AUTO`、`ATTACK`、`DEFENSE`、`LOOP_STRONG`、`GATHER`、`AUX`、`UNKNOWN`、`COMBAT_SKILL`、`SCROLL`、`ATTR`、`PERMANENT`、`REMOVE`。

> Buff ID 详细分类与中文说明见 `FridaGM/buff分类说明.md`；国际服 6817 条全量字典见 `Where Winds Meet  Lua Script/05_主数据库/`，速查见 `Buff_ID参考.md`。

### 常用 ID 速查

- **最小Buff (MINIMAL)**：30372(无敌减90%真伤)、70063(满状态恢复)、1053070(伤害提升，国际服验证的秒杀级)
- **攻击倍率**：1053017(x2)、1053018(x4)、1053019(x8)
- **防御/无敌**：30372、30310、70184、200071、200086、30366、200031(GM无敌)
- **采集 (GATHER)**：104002-104007、104013-104051 系列（约45个）
- **一击必杀**：400148（换装）

---

## 核心技术链路

### Lua 执行链
1. 内嵌注入 `core.dll` 到游戏进程
2. Frida 短暂Hook `lua_pcallk` 入口，捕获 `lua_State *L`
3. 立即 `Interceptor.detach()`，不持续修改代码段
4. 通过共享内存接收Lua命令
5. 调用 `luaL_loadbufferx` 编译（用随机 chunk 名伪装）
6. 调用 `lua_pcallk` 执行
7. 结果写回共享内存

### 关键 Lua API 定位

- 通过 AOB 特征码在游戏主模块内动态扫描 Lua 入口，不依赖固定 RVA。
- 捕获的入口为 `lua_pcallk`（6 参数版本），用于执行 `luaL_loadbufferx` 编译后的代码。
- 历史参考 RVA（可能随游戏版本变化）：`lua_pcallk ≈ 0x48dbcd0`、`luaL_loadbufferx wrapper ≈ 0x48deb00`。

### 循环机制
- 状态变量：`_G.__GM_TOOL_<name>_ACTIVE` / `_ACTION`
- 执行引擎：`cc.Director:getInstance():getRunningScene():runAction(cc.RepeatForever(cc.Sequence(cc.DelayTime(n), cc.CallFunc(fn))))`
- 再点击发送停止逻辑

### 加新功能/按钮（改 `FridaGMTool.NetFx.cs` 4 处）
1. 字段声明（如 `Button btnXxx;`）
2. 按钮创建 + `.Click` 绑定 `SendExperimentCommand("名", BuildXxxLua("mode"))`
3. `place()` 布局把按钮放进对应标签页
4. `BuildLoopFeatureLua`（或对应 builder）里加 `else if (mode == "xxx")` 分发 + 对应 Lua 函数

---

## 版本迭代历史

### 阶段一：纯 C++ dinput8.dll 方案 (v3-v7) — 已放弃
无法在游戏主线程上下文安全调用 Lua API。

### 阶段二：Frida 框架方案 (v8-v33.0) — 关键链路打通
- v8-v13：Frida 基础框架 + Python 连接器
- v14-v26：lua_State 搜索（多种策略均失败）
- v27-v29：Hook 捕获 L + 函数验证
- v30.0-v30.7：**最小执行链打通**
- v30.8-v33.0：外部脚本执行 + 命令直调执行器

### 阶段三：C# UI 工具 (v5.2-v11.8)
功能补全 + UI 重构 + Buff 外部配置 + 换皮(124项)

### 阶段四：C# FridaCLR 连接器 (v12.0-v12.1)
从 Python 迁移到 C# 内置连接器，但需 .NET 10 Runtime

### 阶段五：方案B 自动加载 + .NET Framework (v13.0)
- 验证 gadget 自动加载脚本可行（无需连接器）
- 重构为 .NET Framework 4.x 单 exe（无外部 Runtime 依赖）
- 通信文件统一指向游戏目录

### 阶段六：最小Buff + 防检测第一轮 (v13.1 / payload v34.0)
- 最小Buff（MINIMAL 分类）、关闭反作弊标志按钮
- payload 随机 chunk 名 + 轮询间隔放宽 + lua_pcall AOB 扫描

### 阶段七：注入稳定 + 部署优化 (v13.2)
- Manual Map 改 Standard，解决闪退
- 配置 StealthInject/ErasePE/HideModule/CloseOnInject/AutoInject（后续 v37.0 内嵌注入中已禁用 ErasePE/HideModule）
- 单文件版支持覆盖更新

### 阶段八：VMProtect + ConfuserEx (v13.4-v13.5)
- VMProtect Ultimate 命令行集成
- ConfuserEx 字符串加密/控制流混淆
- 保护架构：ConfuserEx（外层 stub） → VMProtect（内部 app.exe） → 行为隐藏

### 阶段九：内嵌注入 + 共享内存 + 字符串替换 (v35.0-v37.0)
- v35.0：信号文件机制、日志缓冲写入、轮询间隔优化
- v36.0：共享内存单向通信（C#→Frida）、文件轮询降级
- v37.0：
  - 内嵌注入（无Extreme Injector）
  - 短暂Hook + detach
  - 共享内存双向通信（优先 MMF，降级文件）
  - 去掉send()
  - Frida字符串替换（1892处）
  - Buff随机延迟（0.5-2秒）
  - 日志缓冲写入

---

## 已知问题

| 功能 | 状态 / 原因 |
|------|------|
| ErasePE导致崩溃 | 擦除core.dll的PE头会导致Frida内部状态损坏；内嵌注入中已注释掉该代码，但 Extreme Injector 回退路径仍可能将其设为 true |
| HideModule失败 | Frida gadget不在标准PEB链表中，无法通过PEB摘链隐藏；内嵌注入中已禁用 |
| patchStrings禁用 | 异步内存扫描可能导致崩溃，已改用预替换core.dll |

---

## 下一步路线图

| 优先级 | 任务 | 类型 | 说明与依据 |
|--------|------|------|------------|
| 🔴 高 | **自编译Frida** | 代码 | 从源码编译Frida，彻底替换所有特征（线程名、模块名、API模式） |
| 🟡 中 | **延迟ErasePE** | 代码 | 在Frida完全detach后再擦除PE头，避免破坏Frida内部状态 |
| 🟡 中 | **硬件断点Hook** | 代码 | 使用Dr0-Dr3寄存器，不修改代码段，更隐蔽 |
| 🟡 中 | **patchStrings优化** | 代码 | 实现安全的异步内存扫描，替换运行时内存中的特征字符串 |
| 🟢 低 | **通信改命名管道** | 代码 | 进一步消除共享内存特征 |
| 🟢 低 | **Lua payload 字符串混淆** | 代码 | `set_niubility`/`add_buff` 等明文，运行时拼接或 XOR |

---

## 硬约束与经验教训

### 硬约束
- VMProtect 可能阻止部分 Hook，但 `lua_pcallk` Hook 已验证可用
- `core.dll`（Frida Gadget）默认用内嵌注入，内嵌失败时仍保留 Extreme Injector 回退
- Lua 执行必须在 `lua_pcallk` 自然调用现场
- Buff 施加用 `rpc_fake_add_buff` 或 `add_buff`，循环用 `cc.Director runAction`
- 共享内存名称 `Global\WinSvcSharedMem` 固定，进程名 `yysls.exe` 写死
- 就绪检测关键字为 `Ready. Mode:` 和 `CAPTURED L=`

### 经验教训
1. 纯 C++ dinput8.dll 方案不可行 — 无法安全调用 Lua API
2. 标准 Lua 5.4 结构偏移不适用 — 游戏修改了 Lua 源码
3. 被动追踪比盲猜调用更有效 — v30.5 通过自然调用参数定位到正确入口
4. 先跑最小链路，再加载外部脚本
5. 循环功能不要依赖游戏内 ccui 菜单，用 `cc.Director runAction` 自建
6. 攻击类功能（一击/增伤）是最难突破的，Buff ID 方式效果有限
7. **frida-gadget.config 必须在注入前就位** — gadget 只在 LoadLibrary 瞬间读 config
8. **ErasePE不能在Frida运行时执行** — 会破坏Frida内部状态导致崩溃
9. **patchStrings异步扫描可能导致崩溃** — 已改用预替换core.dll方式
10. **共享内存必须在注入前创建** — 否则Frida端无法打开
11. **HideModule对Frida gadget无效** — Frida不在标准PEB链表中
12. **Git Bash 编译要加 `MSYS_NO_PATHCONV=1`** — 否则 `/target` `/out` 等参数被转成 Windows 路径
13. **运行后会留下随机名 exe 副本** — 工具目录下可能出现 `ServiceServer.exe` 等残留，打包前应清理
14. **就绪检测字符串会变化** — 当前检测 `Ready. Mode:` 和 `CAPTURED L=`，旧的 `Ready. Command direct executor is polling:` 已废弃

---

## 保护层级说明

当前单文件版具备四层保护：

| 层级 | 工具 | 保护内容 |
|------|------|----------|
| .NET 混淆 | ConfuserEx | 外层 stub：字符串加密、控制流混淆、反 ILDasm、资源加密 |
| PE 加壳 | VMProtect | 内部 `app.exe`：压缩、内存保护、反调试、节区名随机化 |
| DLL字符串替换 | Python脚本 | 替换core.dll中1892处Frida特征字符串 |
| 行为隐藏 | C# 代码 | 内嵌注入、短暂Hook、共享内存优先、去掉send()、随机进程名、Buff随机延迟 |
