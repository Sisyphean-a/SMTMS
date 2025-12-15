# 星露谷物语模组本地化与管理系统 (SMTMS)

## 项目简介
SMTMS 是一个基于 .NET 8 WPF 构建的现代化模组管理工具，旨在解决非英语母语玩家在管理和汉化《星露谷物语》模组时面临的痛点。

## 功能特性
- **现代化 UI**：基于 WPF 的高性能列表展示。
- **Git 集成**：内置版本控制，保障数据安全（TODO）。
- **自动化元数据**：自动从 Nexus Mods 获取信息（TODO）。
- **AI 辅助翻译**：集成机器翻译 API（TODO）。

## 项目结构
- `src/SMTMS.Core`: 核心领域模型和接口。
- `src/SMTMS.Data`: SQLite 数据库层。
- `src/SMTMS.UI`: WPF 用户界面。
- `src/SMTMS.GitProvider`: Git 操作封装。
- `src/SMTMS.NexusClient`: Nexus API 客户端。

## 快速开始

### 开发环境
- .NET 8 SDK
- Visual Studio 2022 / Rider / VS Code

### 运行
1. 打开 `SMTMS.sln`。
2. 将 `SMTMS.UI` 设为启动项目。
3. 运行应用程序。
4. 在主界面输入你的 Mods 文件夹路径（默认为 Steam 路径）。
5. 点击 "Load Mods" 按钮。

## 后续开发计划 (Roadmap)

### 1. Git 集成完善
- [ ] 在 `LoadModsAsync` 中，为每个模组检查 Git 状态。
- [ ] 实现 `Stage` 和 `Commit` 按钮功能。
- [ ] 添加历史版本回滚 UI。

### 2. Nexus API 对接
- [ ] 完善 `NexusClient`，实现真实的 API 请求。
- [ ] 在界面显示模组封面图和点赞数。
- [ ] 添加 API Key 设置界面。

### 3. 翻译功能
- [ ] 实现 `TranslationService` (DeepL/Google)。
- [ ] 添加翻译编辑器界面（左右对照）。
- [ ] 实现一键批量翻译功能。

### 4. 数据库缓存
- [ ] 在 `LoadModsAsync` 时，优先从 SQLite 读取元数据。
- [ ] 实现翻译记忆库（Translation Memory）的存储与查询。
