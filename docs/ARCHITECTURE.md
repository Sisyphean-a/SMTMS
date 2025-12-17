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

  - 定义接口：`IModService`, `IModRepository`, `IGitService`, `INexusClient`, `ITranslationService`, `IGamePathService`。
  - 定义模型：`ModManifest`, `ModMetadata`, `TranslationMemory`, `GitCommitModel`, `NexusModDto` 等。
  - `ModService`, `TranslationService` (负责提取/恢复翻译), `RegistryGamePathService`。
  - 提供横切基础设施：`LogAttribute`（AOP 日志）、`ServiceLocator`。

- **SMTMS.Data**：数据访问层

  - `AppDbContext`（EF Core + SQLite）。
  - `ModRepository : IModRepository`，负责 `ModMetadata` / `TranslationMemory` 的 CRUD。

- **SMTMS.GitProvider**：Git 集成

  - `GitService : IGitService`，基于 LibGit2Sharp 对 `%APPDATA%/SMTMS` 仓库进行 `Init/Commit/Reset/GetHistory` 等操作。

- **SMTMS.NexusClient**：Nexus API 客户端

  - `NexusClient : INexusClient`，占位实现，后续连接 Nexus Mods REST/GraphQL API。

- **SMTMS.Translation**：翻译服务实现

  - 核心实现 `TranslationService`，负责在 "本地 manifest.json" 与 "SQLite 数据库" 之间双向同步翻译数据 (Extract/Restore)。

- **SMTMS.UI**：WPF 前端 & 组合根
  - 使用 `Host.CreateDefaultBuilder` 配置 DI、数据库、服务与 ViewModel。
  - 初始化 `ServiceLocator`，创建并显示 `MainWindow`。

---

## 2. 模块内部结构

### 2.1 核心层（SMTMS.Core）组件关系

```mermaid
flowchart TD
    subgraph SMTMS_Core[SMTMS.Core]
        IMod["IModService"] --> ModService["ModService"]
        ITrans["ITranslationService"] --> TranslationService["TranslationService"]
        IGame["IGamePathService"] --> RegistryGamePathService["RegistryGamePathService"]

        IRepo["IModRepository"]
        IGit["IGitService"]
        INexus["INexusClient"]

        ModService --> ModManifest["ModManifest"]
        TranslationService --> ModMetadata["ModMetadata"]
        TranslationService --> TranslationMemory["TranslationMemory"]
    end

    subgraph Aspects_Infra["Aspects & Infra"]
        LogAttr["LogAttribute (Rougamo Aspect)"] --> Logger["ILogger (via ServiceLocator)"]
        ServiceLocator["ServiceLocator"]
    end

    TranslationService --> IRepo
```

要点：

- **接口全部定义在 Core 中**，实现分别在 Core/Data/GitProvider/NexusClient 等模块中。
- `TranslationService` 通过 `IServiceScopeFactory` 动态获取 `IModRepository`，避免直接依赖 SMTMS.Data。
- `LogAttribute` 通过 `ServiceLocator` 拿到 `ILogger&lt;T&gt;`，对带 `[Log]` 的类/方法织入统一日志。

### 2.2 数据层（SMTMS.Data）

```mermaid
flowchart TD
    subgraph SMTMS_Data[SMTMS.Data]
        AppDb["AppDbContext"] --> Sqlite["smtms.db"]
        ModRepo["ModRepository"] --> AppDb
    end

    ModRepo --> IModRepository["IModRepository"]
```

- `AppDbContext`：配置 SQLite，定义 `DbSet<ModMetadata>` 与 `DbSet<TranslationMemory>`。
- `ModRepository`：实现 `IModRepository`，封装对 EF Core 的访问逻辑。

### 2.3 Git 集成模块（SMTMS.GitProvider）

```mermaid
flowchart TD
    subgraph SMTMS_GitProvider[SMTMS.GitProvider]
        GitService["GitService"] --> LibGit2Sharp["LibGit2Sharp"]
    end

    GitService --> IGitService["IGitService"]
```

- `GitService`：使用 LibGit2Sharp，实现 `IsRepository/Init/Commit/Checkout/GetStatus/GetHistory/Reset`。

### 2.4 UI 层与依赖注入（SMTMS.UI）

```mermaid
flowchart TD
    App["App.xaml.cs"] --> Host["IHost"]
    Host --> DI["ServiceCollection"]

    DI --> MainWindow["MainWindow"]
    DI --> MainVM["MainViewModel"]

    MainVM --> IGamePath["IGamePathService"]
    MainVM --> IModSvc["IModService"]
    MainVM --> ITransSvc["ITranslationService"]
    MainVM --> IGitSvc["IGitService"]
    MainVM --> Scope["IServiceScopeFactory"]

    Scope --> IRepo["IModRepository"]
```

- `App` 构造函数中配置：
  - `AddDbContext<AppDbContext>()`（SQLite）。
  - `AddSingleton<IGitService, GitService>()`。
  - `AddSingleton<IModService, ModService>()`。
  - `AddSingleton<IGamePathService, RegistryGamePathService>()`。
  - `AddScoped<IModRepository, ModRepository>()`。
  - `AddSingleton<ITranslationService, TranslationService>()`。
  - `AddSingleton<MainViewModel>()`, `AddSingleton<MainWindow>()`。
- `OnStartup` 中：
  - 启动 Host，
  - 调用 `ServiceLocator.Initialize(_host.Services)`，
  - 解析并显示 `MainWindow`。

---

## 3. 关键用例时序图

本节用简化的时序图展示几个核心用户操作背后的调用关系。

### 3.1 加载 Mods（扫描并同步到数据库）

```mermaid
sequenceDiagram
    participant U as User
    participant V as MainWindow
    participant VM as MainViewModel
    participant MS as IModService(ModService)
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository(ModRepository)
    participant FS as FileSystem
    participant DB as SQLite

    U->>V: 点击 "Scan Mods" 按钮
    V->>VM: LoadModsAsync()
    VM->>VM: Status = "Scanning mods..."
    VM->>MS: ScanModsAsync(ModsDirectory)
    MS->>FS: 遍历 Mods 子目录, 读取 manifest.json
    FS-->>MS: ModManifest 列表
    MS-->>VM: IEnumerable<ModManifest>

    VM->>Scope: CreateScope()
    Scope-->>VM: IServiceScope
    VM->>Repo: GetModAsync(UniqueID) / UpsertModAsync(ModMetadata)
    Repo->>DB: 查询/插入/更新 ModMetadata
    DB-->>Repo: 保存成功
    Repo-->>VM: 返回最新 ModMetadata

    VM->>VM: 创建 ModViewModel 集合, 应用翻译字段
    VM->>VM: Status = $"Loaded {count} mods."
```

### 3.2 保存当前 Mod 的翻译 (Save Local Only)

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant FS as FileSystem

    U->>VM: 点击 "Save" 当前选中 Mod
    VM->>VM: 检查 SelectedMod 是否为空

    VM->>FS: 写入 manifest.json (Newtonsoft.Json)
    VM->>VM: Status = "已保存 '...' (本地)。"
    note right of VM: 此时数据库未更新，Git 未创建提交
```

### 3.3 同步到数据库 & 创建版本 (Sync / Checkpoint)

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant TS as ITranslationService
    participant GB as IGitService
    participant DB as SQLite
    participant FS as FileSystem

    U->>VM: 点击 "Sync to Database"
    VM->>TS: SaveTranslationsToDbAsync(ModsDirectory)
    TS->>DB: 更新 ModMetadata 表 (Source of Truth)

    VM->>TS: ExportTranslationsToGitRepo(ModsDirectory, AppDataPath)
    TS->>FS: 将 manifest.json 复制到 AppData/SMTMS/Mods (Shadow Copy)

    VM->>GB: CommitAll(AppDataPath, message)
    GB->>GB: Stage * -> Commit

    VM->>VM: Status = "同步成功：已创建新版本。"
    VM->>VM: LoadHistory()
```

### 3.4 从数据库恢复翻译到模组 (Restore/Apply)

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant TS as ITranslationService
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository
    participant DB as SQLite
    participant FS as FileSystem

    U->>VM: 点击 "Restore Translations" (恢复翻译)
    VM->>TS: RestoreTranslationsFromDbAsync(ModsDirectory)

    TS->>Scope: CreateScope()
    Scope-->>TS: IServiceScope
    TS->>Repo: GetAllModsAsync()
    Repo->>DB: 获取所有带翻译的 Mod
    DB-->>Repo: List<ModMetadata>

    TS->>FS: 遍历目录寻找 manifest.json
    loop 每个 Manifest 文件
        TS->>TS: 匹配 UniqueID
        alt 数据库中有翻译且与本地不同
            TS->>FS: 读取 manifest.json 内容
            TS->>TS: 正则替换 Name/Description
            TS->>FS: 写入更新后的 manifest.json
        end
    end

    TS-->>VM: 完成
    VM->>VM: Status = "已从数据库恢复翻译。"
    VM->>VM: LoadModsAsync()
```

### 3.5 Git 回滚

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant Git as IGitService(GitService)
    participant TS as ITranslationService

    U->>VM: 选中某个 Commit, 点击 "Rollback"
    VM->>Git: Reset(appDataPath, SelectedCommit.FullHash)
    Git->>Git: Reset(Hard) -> 恢复 smtms.db 和 Shadow Files 到旧版本

    VM->>TS: RestoreTranslationsFromDbAsync(ModsDirectory)
    TS->>TS: 从回滚后的 DB 读取数据
    TS->>Game Dir: 覆盖游戏目录下的 manifest.json (应用变更)

    VM->>VM: Status = $"Rolled back to '{ShortHash}' and applied to files."
    VM->>VM: LoadModsAsync()
```
