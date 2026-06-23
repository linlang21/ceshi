# 目录索引 — Where Winds Meet Lua Script（国际服论坛资料）

国外论坛关于燕云十六声**国际服** GM/作弊脚本的讨论与附件，已分类归档。

> 🔗 **关联**：本资料是 [7号 FridaGM 工具](../banyi/GM工具开发文档.md) 的国际服参考来源（同 `hexm` 引擎）。项目总览见 [项目文档.md](../../项目文档.md)。另见国服注入 PoC [Where Winds Meet/README.md](../Where%20Winds%20Meet/README.md)（抓到无敌真实调用链）。

## 📄 先读这两份

- **[国际服资料整理总结.md](国际服资料整理总结.md)** — 主报告：技术链路、API 清单、反作弊情报、对国服工具的行动建议
- **[Buff_ID参考.md](Buff_ID参考.md)** — buff ID 速查（高价值/危险/分类）

## 📁 归档目录

| 目录 | 内容 |
|------|------|
| `01_注入器与Hook/` | C++ DLL 注入器源码、Frida `Hook.js`、编译好的 `Tester.dll`、AOB 特征签名 |
| `02_GM主脚本/` | 4 份 GM Lua 脚本（daima 完整版 / 精简版 / Test.lua / buff专精重构版） |
| `03_Buff清单/` | 8 份人工整理 buff 清单（带效果注释、危险标记） |
| `04_反检测/` | 反作弊机制（VAD Tree 检测、LoadLibrary Hook、安全注入方式、字符串混淆） |
| `05_主数据库/` | `全量Buff数据库_6817条.csv` — 客户端 dump 的权威 buff 字典 |

## 核心结论

国际服与国服**共用 `hexm` 引擎代码库**（`set_niubility` 等拼音函数名为证），其 API 路径、buff 数据库、循环机制基本可迁移到国服 FridaGM 工具。三大可直接利用资产：**6817 条 buff CSV、完整 GM API 清单、VAD Tree 反作弊情报**。
