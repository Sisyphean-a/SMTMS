# 星露谷物语模组翻译与管理系统 (SMTMS)

SMTMS (Stardew Mod Translation & Management System) 是一个专为《星露谷物语》玩家设计的**现代化翻译管理工具**。它致力于解决模组汉化易丢失、管理混乱、版本难以回溯的痛点，通过**“非侵入式”**的理念和**“时光机”**般的数据安全机制，让你的模组汉化工作无后顾之忧。

## 核心理念 (Core Philosophy)

### 1. 🛡️ 数据安全第一 (Safety First)
SMTMS 引入了企业级的 **Git 版本控制** 作为底层存储后端（位于 `%APPDATA%\SMTMS`），但它不需要你懂任何 Git 命令。
- **自动快照**：每一次保存翻译，系统都会自动生成一个“历史快照”。
- **时光机 (Time Machine)**：搞砸了？翻译改错了？一键回滚到任意历史版本。

### 2. 🔄 翻译与模组分离 (Decoupled Translation)
模组更新往往会覆盖 `manifest.json`，导致辛辛苦苦修改的中文名和介绍丢失。SMTMS 采用 **集中式数据库 (SQLite)** 存储你的翻译成果：
- **Extract (提取)**：自动扫描并提取你现有的汉化成果到数据库。
- **Restore (注入)**：模组更新后，只需点击“恢复”，即可将数据库中的翻译重新注入到模组文件中。

### 3. 🎯 极简主义 (Minimalism)
无需复杂的配置，无需手动备份文件。SMTMS 旨在让一切自动化、直观化。

---

## 核心功能 (Features)

- **🔍 智能扫描**：自动识别 `Mods` 目录下安装的所有模组，支持解析 `manifest.json`。
- **💾 双向同步工作流**：
  - **Extract Translations**：将分散在各个模组文件夹中的汉化信息“归档”到本地数据库。
  - **Restore Translations**：将数据库中的汉化信息“应用”回模组文件 (Safe Regex Replace)。
- **📝 实时编辑**：在列表页直接修改模组名称和描述，修改即刻保存并版本化。
- **📜 历史记录与回滚**：全图形化的历史记录界面，清晰展示每一次变更，支持一键硬重置 (Hard Reset) 到指定版本。
- **🇨🇳 完全本地化**：全中文界面支持。

---

## 项目结构 (Architecture)

SMTMS 基于 **.NET 8** 和 **WPF** 构建，採用纯净的 **MVVM** 架构：

| 模块 | 职责 | 关键技术 |
| :--- | :--- | :--- |
| **SMTMS.UI** | 用户界面与展现逻辑 | WPF, CommunityToolkit.Mvvm |
| **SMTMS.Core** | 核心业务模型与防腐层接口 | Rougamo (AOP Logging) |
| **SMTMS.Data** | 数据持久化与查询 | EF Core, SQLite |
| **SMTMS.GitProvider** | 自动化版本控制 | LibGit2Sharp |
| **SMTMS.Translation** | 翻译同步服务 | Newtonsoft.Json, Regex |

---

## 快速开始 (Getting Started)

### 开发环境
- .NET 8 SDK
- Visual Studio 2022 / JetBrains Rider

### 使用指南
1. **启动**：运行程序，确保上方显示的 "Mods 目录" 正确（默认自动检测 Steam 路径）。
2. **扫描**：程序启动时会自动扫描，也可手动点击界面上的刷新按钮。
3. **提取 (首次使用)**：如果你已经手动汉化过模组，点击 **"提取翻译 (Extract)"** 将它们存入数据库。
4. **编辑**：在列表中选中模组，修改名称或描述，点击 **"保存 (Save)"**。此时系统会自动创建 Git 提交。
5. **恢复**：当模组更新导致汉化丢失时，点击 **"恢复翻译 (Restore)"** 即可一键复原。
6. **回溯**：在 "历史记录" 标签页，双击或选中任意提交并点击 "回滚"，即可将数据恢复到该时刻的状态。

---

## 开发计划 (Roadmap)

### ✅ 已完成 (Implemented)
- [x] 基于 WPF 的现代化 UI 框架
- [x] SQLite 数据库与 EF Core 集成
- [x] LibGit2Sharp 后台自动版本控制
- [x] 翻译数据的 **提取 (Extract)** 与 **恢复 (Restore)** 核心逻辑
- [x] 历史记录查看与一键回滚
- [x] AOP 统一日志记录

### 🚧 进行中 (In Progress)
- [ ] **AI 翻译集成**：接入 DeepL / LLM API 实现一键自动翻译。
- [ ] **Nexus Mods 对接**：自动获取模组封面、原文对比、更新检测。
- [ ] **多语言支持**：支持将模组翻译为更多语言。
- [ ] **翻译记忆库 (TM)**：更细粒度的文本复用。

---

> _"让模组管理回归简单，让翻译成果不再丢失。"_
