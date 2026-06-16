# 燕云十六声 FridaGM 辅助工具

## 项目介绍

通过 Frida 框架注入游戏进程，调用游戏内部 Lua 5.4 引擎执行 GM 命令的 Windows 桌面工具。

**技术路线：** 注入器 → `frida-gadget.dll` → gadget 自动加载 `gm_payload.js` → Hook `lua_pcall` 捕获 `lua_State` → 轮询 `gm_cmd.txt` 执行 Lua 命令

**当前版本：** v13.0（.NET Framework 4.x，Win10/11 自带 Runtime，单 exe）

---

## 目录结构

```
E:\ceshi\7\
├── banyi/                          # 开发目录（源码、文档、测试）
│   ├── FridaGMTool.NetFx.cs        # v13.0 主源码（.NET Framework 4.x）
│   ├── FridaGMTool.cs              # v12.1 旧源码（.NET 10 + FridaCLR，备用参考）
│   ├── FridaGMTool.Net.csproj      # v12.1 项目文件（备用参考）
│   ├── NuGet.Config                # NuGet 配置（v12.1 用）
│   ├── gm_payload.js               # Frida Hook 脚本（核心 payload）
│   ├── GM工具开发文档.md            # 本文档
│   └── yylaoliu_decoded/           # 老六脚本解码
│       └── stealth_all_base64_decoded.lua
│
├── FridaGM/                        # 发布目录（可直接使用）
│   ├── FridaGMTool.exe             # 主程序（201KB 单 exe）
│   ├── frida-gadget.dll            # Frida Gadget（注入目标）
│   ├── frida-gadget.config         # 自动加载配置（方案B）
│   ├── gm_payload.js               # Hook 脚本
│   ├── Extreme Injector v3.exe     # DLL 注入器
│   ├── settings.xml                # 注入器配置
│   ├── buff_config.txt             # Buff 外部配置
│   ├── buff分类说明.md              # Buff 分类说明
│   └── config.txt                  # 游戏路径配置
│
├── yylaoliucn/                     # 燕云老六（国服）参考项目
├── Where Winds Meet/               # 国服适配脚本参考
├── WhereWindsMeet-Lua-Injector-main/ # 国际服注入器参考
└── Test.lua-main/                  # 节奏游戏/象棋/GM训练面板参考
```

---

## 当前进度

### 已完成

- [x] Frida Hook lua_pcall 捕获 lua_State（最小执行链打通）
- [x] gm_payload.js 轮询 gm_cmd.txt 执行 Lua 命令
- [x] 方案B验证：gadget 自动加载脚本，无需连接器
- [x] v13.0 .NET Framework 4.x 重构：单 exe，无外部 Runtime 依赖
- [x] 4标签页 UI：战斗增强 / Buff循环 / 游戏辅助 / 换皮速度
- [x] 15+ GM 功能：无敌、自动拾取、NPC变笨、超级闪避、Buff施加、循环功能、传送、换皮(124项)、剧情速度、攻击倍率、自动开箱、节奏游戏、象棋秒赢等
- [x] Buff 外部配置机制（buff_config.txt）
- [x] 内存加速（偏移链写 float）

### 待测试

- [ ] v13.0 完整流程测试：复制文件 → 启动游戏 → 注入 → 自动就绪 → GM 功能
- [ ] 各 Buff 效果实测
- [ ] 内存加速在新版本游戏中的偏移链验证

### 已知未生效

| 功能 | 原因 |
|------|------|
| 一击必杀 | `combat_train_action` 在国服不存在 |
| 攻击Buff增伤 | Buff 施加成功但体感不明显 |

---

## 编译与发布

### 编译命令

```bash
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /platform:x64 /out:E:\ceshi\7\FridaGM\FridaGMTool.exe E:\ceshi\7\banyi\FridaGMTool.NetFx.cs
```

### 发布文件清单

| 文件 | 大小 | 作用 |
|------|------|------|
| `FridaGMTool.exe` | 201KB | 主程序（.NET Framework 4.x，Win10/11 自带 Runtime） |
| `frida-gadget.dll` | ~22MB | Frida Gadget |
| `frida-gadget.config` | 61B | 自动加载配置 |
| `gm_payload.js` | ~12KB | Hook 脚本 |
| `Extreme Injector v3.exe` | ~1.9MB | DLL 注入器 |
| `settings.xml` | 3KB | 注入器配置 |
| `buff_config.txt` | 2KB | Buff 外部配置 |
| `config.txt` | 8B | 游戏路径配置 |

### 使用流程

```
1. 打开 FridaGMTool.exe
2. 选择游戏目录
3. 点「复制文件」→ 写入 frida-gadget.dll + gm_payload.js + frida-gadget.config 到游戏目录
4. 启动游戏
5. 用注入器注入 frida-gadget.dll → gadget 自动加载 payload
6. 进入游戏场景 → 工具自动检测就绪，GM 按钮可用
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

---

## Buff ID 清单

### 攻击Buff (atkbuff_combo)
1053027, 10927, 2005, 102400-102457, 109xx, 10532, 20035/200036, 102407, 102457, 1053026(16倍增伤)

### 防御Buff (defbuff)
30372(无敌减90%), 30310, 70184, 20071, 200059/20083/200099, 200086, 30366, 30303(增加防御), 200031(GM无敌)

### 采集Buff (gather_buff)
104002-104007, 104013-104051 系列（约45个）

### 辅助Buff (aux_buff)
30005(免疫控制), 70110(免疫沉默), 102701(CD-20%), 102702(内力-20%), 102703, 102704, 109602, 109603, 70025(免疫招架)

### 未知Buff (unknown_buff)
200102, 107201-107204, 70182/70183/70186/70187, 20095

### 攻击倍率
1053017(x2), 1053018(x4), 1053019(x8)

### 战斗技巧 (combat_skill)
102502/102503/102505/102508/102705/102706/103007/103008/102605/102606/102607

### 卷轴心法 (scroll_buff)
109604-109609

### 属性Buff (attr_buff)
104009-104012, 104001, 104004

### Buff 外部配置
`buff_config.txt` 支持按分类配置，格式：`ATTACK = {30302, 30314}`，每分类单行。分类包括：ATTACK、DEFENSE、LOOP_STRONG、GATHER、AUX、UNKNOWN、COMBAT_SKILL、SCROLL、ATTR、AUTO、PERMANENT、REMOVE。

---

## 核心技术链路

### Lua 执行链
1. Frida Hook `lua_pcall`，在自然调用现场捕获 `lua_State *L`
2. payload 读取 `gm_cmd.txt` 中的 Lua 代码
3. 调用 `luaL_loadbufferx` (RVA=0x48deb00) 编译
4. 调用 `lua_pcall` (RVA=0x48dbcd0) 执行
5. 结果写入 `gm_cmd_result.txt`

### 关键 Lua API 地址

| 函数 | RVA | 状态 |
|------|-----|------|
| lua_pcall | 0x48dbcd0 | 已确认 |
| luaL_loadbufferx wrapper | 0x48deb00 | 已确认 |
| luaB_pcall | 0x4973710 | 已确认 |
| luaL_loadfilex thunk | 0x48deae0 | 已确认 |

### 循环机制
- 状态变量：`_G.__GM_TOOL_<name>_ACTIVE` / `_ACTION`
- 执行引擎：`cc.Director:getInstance():getRunningScene():runAction(cc.RepeatForever(cc.Sequence(cc.DelayTime(n), cc.CallFunc(fn))))`
- 再点击发送停止逻辑

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

---

## 硬约束与经验教训

### 硬约束
- VMProtect 可能阻止部分 Hook，但 `lua_pcall` Hook 已验证可用
- frida-gadget.dll 必须用原版注入器注入
- Lua 执行必须在 `lua_pcall` 自然调用现场
- `combat_train_action` 在国服不存在，一击路线需另寻入口
- Buff 施加用 `rpc_fake_add_buff` 或 `add_buff`，循环用 `cc.Director runAction`

### 经验教训
1. 纯 C++ dinput8.dll 方案不可行 — 无法安全调用 Lua API
2. 标准 Lua 5.4 结构偏移不适用 — 游戏修改了 Lua 源码
3. 被动追踪比盲猜调用更有效 — v30.5 通过自然调用参数定位到正确入口
4. 先跑最小链路，再加载外部脚本
5. 循环功能不要依赖游戏内 ccui 菜单，用 `cc.Director runAction` 自建
6. 攻击类功能（一击/增伤）是最难突破的，Buff ID 方式效果有限
7. **frida-gadget.config 必须在注入前就位** — gadget 只在 LoadLibrary 瞬间读 config，写入晚了会退化成 listen 模式
8. **"一直等待 Lua 加载"的真正原因** — config/payload 没在 gadget 加载时到达游戏目录
