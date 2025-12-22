# SMTMS 架构总览（Architecture Overview）

本文件从三个层次描述 SMTMS：

1. **顶层模块依赖图**：各项目（Core/Data/GitProvider/NexusClient/Translation/UI）之间的引用关系。
2. **模块内部组件图**：每个模块内部的接口、实现类和关键模型，以及它们之间的依赖。
3. **关键用例时序图**：加载 Mods、保存翻译、导入历史数据、应用翻译到 manifest、Git 回滚等核心流程。

> 说明：本文件中的 Mermaid 图可在支持 Mermaid 的 Markdown 工具中直接渲染；也可用 Mermaid CLI 或在线工具导出为 HTML/SVG。

---

## 1. 顶层模块依赖关系

```mermaid
flowchart LR
    UI["SMTMS.UI (WPF Shell)"] --> Core["SMTMS.Core"]
    UI --> Data["SMTMS.Data"]
    UI --> Git["SMTMS.GitProvider"]
    UI --> Nexus["SMTMS.NexusClient"]
    UI --> Trans["SMTMS.Translation"]

    Core --> ModsDir["SMAPI Mods Folder"]
    Data --> Sqlite["SQLite smtms.db"]
    Git --> GitRepo["Git repo in AppData/SMTMS"]
```

### 1.1 模块职责简述

- **SMTMS.Core**：领域与接口层

  - 定义接口：`IModService`, `IModRepository`, `IGitService`, `IGitDiffCacheService`, `INexusClient`, `ITranslationService`, `IGamePathService`, `ISettingsService`, `IFileSystem`。
  - 定义模型：`ModManifest`, `ModMetadata`, `TranslationMemory`, `GitCommitModel`, `ModDiffModel`, `GitDiffCache`, `NexusModDto`, `AppSettings` 等。
  - 定义通用类型：`Result<T>`, `OperationResult` (统一错误处理模式)。
  - 服务实现：`ModService`, `RegistryGamePathService`, `PhysicalFileSystem`。

- **SMTMS.Data**：数据访问层

  - `AppDbContext`（EF Core + SQLite）。
  - `ModRepository : IModRepository`，负责 `ModMetadata` / `TranslationMemory` / `GitDiffCache` 的 CRUD。
  - `GitDiffCacheService : IGitDiffCacheService`，负责 Git Diff 缓存的读取、保存和清理（使用 MessagePack 序列化）。
  - `SettingsService : ISettingsService`，负责应用配置（如最后使用的 Mods 目录、窗口尺寸等）的持久化。
  - **性能优化**：
    - 为常用查询字段添加索引（`LastTranslationUpdate`, `RelativePath`, `Engine`, `Timestamp`, `CreatedAt`）。
    - 新增 `LastFileHash` 用于内容指纹快速比对。
    - 所有只读查询使用 `AsNoTracking()` 减少内存占用。
    - 所有异步方法支持 `CancellationToken`，允许取消长时间操作。

- **SMTMS.GitProvider**：Git 集成

  - `GitService : IGitService`，基于 LibGit2Sharp 对 `%APPDATA%/SMTMS` 仓库进行 `Init/Commit/Reset/GetHistory` 等操作。

- **SMTMS.NexusClient**：Nexus API 客户端

  - `NexusClient : INexusClient`，占位实现，后续连接 Nexus Mods REST/GraphQL API。

- **SMTMS.Translation**：翻译服务实现

  - 核心实现 `TranslationService : ITranslationService`，负责在 "本地 manifest.json" 与 "SQLite 数据库" 之间双向同步翻译数据 (Extract/Restore)。
  - **架构改进**：
    - 从 Core 层移至独立的 Translation 层，实现真正的分层架构。
    - 使用构造函数注入 `ILogger<TranslationService>`、`IServiceScopeFactory` 和 `IFileSystem`。
    - 所有方法返回 `OperationResult`，提供统一的错误处理和详细的操作结果。
    - 通过 `IFileSystem` 抽象所有文件操作，实现可测试性。
  - **性能优化**：
    - 使用 C# 11+ `[GeneratedRegex]` 特性，编译时生成优化的正则表达式代码。
    - 并行文件处理（`Task.WhenAll`），充分利用多核 CPU。
    - 批量数据库操作，减少往返次数。
    - 支持 `CancellationToken`，允许取消长时间操作。
  - **日志改进**：
    - 结构化日志记录，包含关键业务上下文（文件数量、成功/失败计数、错误详情）。
    - 使用适当的日志级别（Debug/Information/Warning/Error）。

- **SMTMS.Tests**：单元测试项目

  - **测试覆盖**：39个测试，100%通过
    - Git 服务测试（22个）：初始化、提交、推送、拉取、回滚等
    - Mod 服务测试（7个）：扫描、加载、保存等
    - 翻译服务测试（5个）：提取、恢复、同步等
    - 文件系统测试（5个）：读写、目录操作等
  - **Mock 实现**：
    - `InMemoryFileSystem`：内存文件系统，用于测试文件操作
    - `InMemoryModRepository`：内存数据库，用于测试数据访问
  - **测试框架**：xUnit + Moq

- **SMTMS.UI**：WPF 前端 & 组合根
  - 使用 `Host.CreateDefaultBuilder` 配置 DI、数据库、服务与 ViewModel。
  - 应用数据库迁移，创建并显示 `MainWindow`。
  - 所有依赖通过构造函数注入，遵循标准 DI 模式。
  - 注册 `IFileSystem` 为 `PhysicalFileSystem`（生产环境）。
  - **MVVM 架构**：
    - `MainViewModel`：Shell 容器，负责全局状态和导航。
    - `ModListViewModel`：模组列表管理，负责扫描、加载和单项编辑。
    - `HistoryViewModel`：Git 历史管理，负责提交历史和差异对比。
    - 使用 `WeakReferenceMessenger` 实现组件间解耦通信（状态消息、刷新请求等）。

---

## 2. 模块内部结构

### 2.1 核心层（SMTMS.Core）组件关系

```mermaid
flowchart TD
    subgraph SMTMS_Core[SMTMS.Core]
        IMod["IModService"] --> ModService["ModService"]
        ITrans["ITranslationService"]
        IGame["IGamePathService"] --> RegistryGamePathService["RegistryGamePathService"]

        IRepo["IModRepository"]
        IGit["IGitService"]
        INexus["INexusClient"]

        ModService --> ModManifest["ModManifest"]

        Result["Result<T>"]
        OpResult["OperationResult"]
    end

    subgraph SMTMS_Translation[SMTMS.Translation]
        TranslationService["TranslationService"] --> ITrans
        TranslationService --> ModMetadata["ModMetadata"]
        TranslationService --> IRepo
    end
```

要点：

- **接口全部定义在 Core 中**，实现分别在 Core/Data/GitProvider/NexusClient/Translation 等模块中。
- `TranslationService` 位于独立的 Translation 层，通过 `IServiceScopeFactory` 动态获取 `IModRepository`，避免直接依赖 SMTMS.Data。
- 所有服务使用构造函数注入 `ILogger<T>` 和 `IFileSystem`，遵循标准依赖注入模式。
- 使用 `Result<T>` 和 `OperationResult` 模式进行统一错误处理。
- `IFileSystem` 抽象层使得所有文件操作可测试，生产环境使用 `PhysicalFileSystem`，测试环境使用 `InMemoryFileSystem`。

### 2.2 数据层（SMTMS.Data）

```mermaid
flowchart TD
    subgraph SMTMS_Data[SMTMS.Data]
        AppDb["AppDbContext"] --> Sqlite["smtms.db"]
        ModRepo["ModRepository"] --> AppDb
        SettingsSvc["SettingsService"] --> AppDb
    end

    ModRepo --> IModRepository["IModRepository"]
    SettingsSvc --> ISettingsService["ISettingsService"]
```

- `AppDbContext`：配置 SQLite，定义 `DbSet<ModMetadata>`、`DbSet<TranslationMemory>`、`DbSet<GitDiffCache>` 和 `DbSet<AppSettings>`。
- `ModRepository`：实现 `IModRepository`，封装对 EF Core 的访问逻辑，提供单个和批量操作接口：
  - `GetModAsync(string uniqueId, CancellationToken)`：查询单个 Mod。
  - `GetModsByIdsAsync(IEnumerable<string> uniqueIds, CancellationToken)`：批量查询多个 Mod，返回 `Dictionary<string, ModMetadata>`，避免 N+1 查询问题。
  - `UpsertModAsync(ModMetadata mod, CancellationToken)`：插入或更新单个 Mod。
  - `UpsertModsAsync(IEnumerable<ModMetadata> mods, CancellationToken)`：批量插入/更新 Mod，收集所有变更后一次性提交，大幅减少数据库往返次数。
- `GitDiffCacheService`：实现 `IGitDiffCacheService`，提供 Git Diff 缓存管理：
  - `GetCachedDiffAsync(string commitHash, CancellationToken)`：从缓存中获取 Diff 数据。
  - `SaveDiffCacheAsync(string commitHash, List<ModDiffModel> diffData, CancellationToken)`：保存 Diff 数据到缓存。
  - `ClearOldCachesAsync(int daysToKeep, CancellationToken)`：清理过期缓存（基于时间）。
  - `ClearLRUCachesAsync(int maxCacheCount, CancellationToken)`：LRU 缓存清理策略，保留最新 N 个缓存。
  - `SmartClearCachesAsync(int daysToKeep, int maxCacheCount, CancellationToken)`：智能缓存清理，结合时间和数量限制。
- **性能优化**：
  - `GitDiffCache` 表用于缓存 Git Diff 结果，避免重复计算。
  - 为高频查询字段添加数据库索引（`LastTranslationUpdate`, `RelativePath`, `Engine`, `Timestamp`, `CreatedAt`）。
  - 支持 MessagePack 序列化以加速数据读写。
  - 批量数据库操作减少往返次数。
  - 并行文件读取利用多核CPU加速I/O操作。
  - 所有只读查询使用 `AsNoTracking()` 减少内存占用。
  - 正则表达式使用 `[GeneratedRegex]` 编译时优化。
  - 所有异步方法支持 `CancellationToken`，允许取消长时间操作。
  - LRU 缓存清理策略防止缓存表无限增长。

### 2.3 Git 集成模块（SMTMS.GitProvider）

```mermaid
flowchart TD
    subgraph SMTMS_GitProvider[SMTMS.GitProvider]
        GitService["GitService"] --> LibGit2Sharp["LibGit2Sharp"]
    end

    GitService --> IGitService["IGitService"]
```

- `GitService`：使用 LibGit2Sharp，实现 `IsRepository/Init/Commit/Checkout/GetStatus/GetHistory/Reset`。
- **性能优化**：
  - `GetStructuredDiff` 使用并行处理（`Task.Run` + `Task.WaitAll`）加速多文件解析。
  - 支持增量加载和缓存机制（通过 `GitDiffCache` 表）。

### 2.4 UI 层与依赖注入（SMTMS.UI）

```mermaid
flowchart TD
    App["App.xaml.cs"] --> Host["IHost"]
    Host --> DI["ServiceCollection"]

    DI --> MainWindow["MainWindow"]
    DI --> MainVM["MainViewModel (Shell)"]
    DI --> ModListVM["ModListViewModel"]
    DI --> HistoryVM["HistoryViewModel"]

    MainVM --> ModListVM
    MainVM --> HistoryVM
    MainVM --> IGamePath["IGamePathService"]
    MainVM --> ITransSvc["ITranslationService"]
    MainVM --> IGitSvc["IGitService"]
    MainVM --> Scope["IServiceScopeFactory"]

    ModListVM --> IModSvc["IModService"]
    ModListVM --> Scope
    ModListVM --> Messenger["WeakReferenceMessenger"]

    HistoryVM --> IGitSvc
    HistoryVM --> Scope
    HistoryVM --> Messenger

    Scope --> IRepo["IModRepository"]
```

- `App` 构造函数中配置：
  - `AddDbContext<AppDbContext>()`（SQLite）。
  - `AddSingleton<IGitService, SMTMS.GitProvider.Services.GitService>()`。
  - `AddSingleton<IModService, ModService>()`。
  - `AddSingleton<IGamePathService, RegistryGamePathService>()`。
  - `AddSingleton<IFileSystem, PhysicalFileSystem>()`（生产环境）。
  - `AddScoped<IModRepository, ModRepository>()`。
  - `AddScoped<IGitDiffCacheService, GitDiffCacheService>()`。
  - `AddScoped<ISettingsService, SettingsService>()`。
  - `AddSingleton<ITranslationService, SMTMS.Translation.Services.TranslationService>()`。
  - `AddSingleton<ModListViewModel>()`, `AddSingleton<HistoryViewModel>()`。
  - `AddSingleton<MainViewModel>()`, `AddSingleton<MainWindow>()`。
- **MVVM 架构**：
  - `MainViewModel` 作为 Shell 容器，持有 `ModListViewModel` 和 `HistoryViewModel`。
  - 各 ViewModel 通过 `WeakReferenceMessenger` 发送和接收消息，实现解耦通信。
  - 消息类型包括：`StatusMessage`（状态更新）、`RefreshModsRequestMessage`（刷新模组列表）、`ModsLoadedMessage`（模组加载完成）等。
- `OnStartup` 中：
  - 启动 Host。
  - 应用数据库迁移（`dbContext.Database.MigrateAsync()`）。
  - 解析并显示 `MainWindow`。

---

## 3. 关键用例时序图

本节用简化的时序图展示几个核心用户操作背后的调用关系。

### 3.1 加载 Mods（扫描并同步到数据库）

```mermaid
sequenceDiagram
    participant U as User
    participant V as MainWindow
    participant ModListVM as ModListViewModel
    participant MS as IModService(ModService)
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository(ModRepository)
    participant Msg as WeakReferenceMessenger
    participant FS as FileSystem
    participant DB as SQLite

    U->>V: 点击 "Scan Mods" 按钮
    V->>ModListVM: LoadModsAsync()
    ModListVM->>Msg: Send(StatusMessage("Scanning..."))
    ModListVM->>MS: ScanModsAsync(ModsDirectory)
    MS->>FS: 并行读取所有 manifest.json (Task.WhenAll)
    FS-->>MS: ModManifest 列表
    MS-->>ModListVM: IEnumerable<ModManifest>

    ModListVM->>Scope: CreateScope()
    Scope-->>ModListVM: IServiceScope
    ModListVM->>Repo: GetModsByIdsAsync(uniqueIds) - 批量查询
    Repo->>DB: 一次性查询所有 Mod
    DB-->>Repo: Dictionary<string, ModMetadata>

    ModListVM->>ModListVM: 收集需要更新的 Mod
    ModListVM->>Repo: UpsertModsAsync(mods) - 批量保存
    Repo->>DB: 一次性提交所有变更
    DB-->>Repo: 保存成功

    ModListVM->>ModListVM: 创建 ModViewModel 集合
    ModListVM->>Msg: Send(StatusMessage("Loaded {count} mods"))
    ModListVM->>Msg: Send(ModsLoadedMessage)
```

### 3.2 保存当前 Mod 的翻译 (Save Local Only)

```mermaid
sequenceDiagram
    participant U as User
    participant ModListVM as ModListViewModel
    participant Msg as WeakReferenceMessenger
    participant FS as FileSystem

    U->>ModListVM: 点击 "Save" 当前选中 Mod
    ModListVM->>ModListVM: 检查 SelectedMod 是否为空

    ModListVM->>FS: 写入 manifest.json (正则替换保留格式)
    ModListVM->>Msg: Send(StatusMessage("已保存 '...' (本地)"))
    note right of ModListVM: 此时数据库未更新，Git 未创建提交
```

### 3.3 同步到数据库 & 创建版本 (Sync / Checkpoint)

```mermaid
sequenceDiagram
    participant U as User
    participant MainVM as MainViewModel
    participant HistoryVM as HistoryViewModel
    participant ModListVM as ModListViewModel
    participant TS as ITranslationService
    participant GB as IGitService
    participant Msg as WeakReferenceMessenger
    participant Repo as IModRepository
    participant DB as SQLite
    participant FS as FileSystem

    U->>MainVM: 点击 "Sync to Database"
    MainVM->>U: 弹出 CommitDialog
    U->>MainVM: 输入提交信息 (Message) & 确认

    MainVM->>Msg: Send(StatusMessage("正在同步..."))
    MainVM->>TS: SaveTranslationsToDbAsync(ModsDirectory)
    TS->>FS: 并行计算所有文件 Hash (Task.WhenAll)
    TS->>TS: 对比数据库 LastFileHash，过滤未变更文件
    TS->>FS: 并行读取和解析变更的 JSON (Task.WhenAll)
    TS->>Repo: GetModsByIdsAsync(uniqueIds) - 批量查询
    Repo->>DB: 一次性查询所有 Mod
    TS->>Repo: UpsertModsAsync(mods) - 批量保存
    Repo->>DB: 一次性提交所有变更

    MainVM->>TS: ExportTranslationsToGitRepo(ModsDirectory, AppDataPath)
    TS->>FS: 并行导出所有 manifest.json (Task.WhenAll)

    MainVM->>GB: CommitAll(AppDataPath, message)
    GB->>GB: Stage * -> Commit

    MainVM->>Msg: Send(StatusMessage("同步成功"))
    MainVM->>HistoryVM: LoadHistory() (通过消息或直接调用)
    MainVM->>Msg: Send(RefreshModsRequestMessage)
    ModListVM->>ModListVM: 接收消息并刷新
```

### 3.6 单模组回滚 (Single Mod Rollback)

```mermaid
sequenceDiagram
    participant U as User
    participant ModVM as ModViewModel
    participant Git as IGitService
    participant FS as FileSystem

    U->>ModVM: 点击 "查看历史 (History)"
    ModVM->>Git: GetFileHistory(repoPath, modRelativePath)
    Git-->>ModVM: List<GitCommitModel>

    ModVM->>U: 显示历史列表弹窗 (ModHistoryDialog)
    U->>ModVM: 选中版本并点击 "Rollback"

    ModVM->>Git: RollbackFile(repoPath, commitHash, modRelativePath)
    Git->>Git: Checkout specific file (Overwrite Local Shadow)

    ModVM->>FS: Copy(ShadowFile, GameModPath)
    note right of ModVM: 将 Git 也就是 Shadow Repo 中的文件覆盖回游戏目录

    ModVM->>ModVM: 更新内存中的 Manifest 显示
    ModVM->>U: 提示回滚成功
```

### 3.4 从数据库恢复翻译到模组 (Restore/Apply)

```mermaid
sequenceDiagram
    participant U as User
    participant MainVM as MainViewModel
    participant ModListVM as ModListViewModel
    participant TS as ITranslationService
    participant Msg as WeakReferenceMessenger
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository
    participant DB as SQLite
    participant FS as FileSystem

    U->>MainVM: 点击 "Restore Translations" (恢复翻译)
    MainVM->>Msg: Send(StatusMessage("正在恢复..."))
    MainVM->>TS: RestoreTranslationsFromDbAsync(ModsDirectory)

    TS->>Scope: CreateScope()
    Scope-->>TS: IServiceScope
    TS->>Repo: GetAllModsAsync()
    Repo->>DB: 获取所有带翻译的 Mod
    DB-->>Repo: List<ModMetadata>

    TS->>FS: 扫描所有 manifest.json 文件
    TS->>FS: 并行处理所有文件 (Task.WhenAll)
    par 并行处理每个文件
        TS->>TS: 匹配 UniqueID
        alt 数据库中有翻译且与本地不同
            TS->>FS: 读取 manifest.json 内容
            TS->>TS: 正则替换 Name/Description
            TS->>FS: 写入更新后的 manifest.json
        end
    end

    TS-->>MainVM: 完成
    MainVM->>Msg: Send(StatusMessage("恢复成功"))
    MainVM->>Msg: Send(RefreshModsRequestMessage)
    ModListVM->>ModListVM: 接收消息并刷新
```

### 3.5 Git 回滚

```mermaid
sequenceDiagram
    participant U as User
    participant HistoryVM as HistoryViewModel
    participant ModListVM as ModListViewModel
    participant Git as IGitService(GitService)
    participant TS as ITranslationService
    participant Msg as WeakReferenceMessenger
    participant Repo as IModRepository
    participant DB as SQLite

    U->>HistoryVM: 选中某个 Commit, 点击 "Rollback"
    HistoryVM->>Msg: Send(StatusMessage("正在回滚..."))
    HistoryVM->>Git: Reset(appDataPath, SelectedCommit.FullHash)
    Git->>Git: Reset(Hard) -> 恢复 Shadow Files (manifest.json) 到旧版本

    HistoryVM->>TS: ImportTranslationsFromGitRepoAsync(appDataPath)
    TS->>TS: 并行读取和解析所有 Shadow Files (Task.WhenAll)
    TS->>Repo: GetModsByIdsAsync(uniqueIds) - 批量查询
    Repo->>DB: 一次性查询所有 Mod
    TS->>Repo: UpsertModsAsync(mods) - 批量保存旧版翻译
    Repo->>DB: 一次性提交所有变更

    HistoryVM->>TS: RestoreTranslationsFromDbAsync(ModsDirectory)
    TS->>TS: 从数据库读取数据 (此时已是旧版翻译)
    TS->>Game Dir: 并行覆盖游戏目录下的 manifest.json (Task.WhenAll)

    HistoryVM->>Msg: Send(StatusMessage("回滚成功"))
    HistoryVM->>Msg: Send(RefreshModsRequestMessage)
    ModListVM->>ModListVM: 接收消息并刷新
```
