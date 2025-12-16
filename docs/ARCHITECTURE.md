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
  - 提供服务实现：`ModService`, `TranslationService`, `RegistryGamePathService`。
  - 提供横切基础设施：`LogAttribute`（AOP 日志）、`ServiceLocator`。

- **SMTMS.Data**：数据访问层
  - `AppDbContext`（EF Core + SQLite）。
  - `ModRepository : IModRepository`，负责 `ModMetadata` / `TranslationMemory` 的 CRUD。

- **SMTMS.GitProvider**：Git 集成
  - `GitService : IGitService`，基于 LibGit2Sharp 对 `%APPDATA%/SMTMS` 仓库进行 `Init/Commit/Reset/GetHistory` 等操作。

- **SMTMS.NexusClient**：Nexus API 客户端
  - `NexusClient : INexusClient`，占位实现，后续连接 Nexus Mods REST/GraphQL API。

- **SMTMS.Translation**：外部翻译引擎集成（预留）
  - 当前仅引用 Core/Data；未来承载 `ITranslationService` 的具体远程翻译实现及翻译记忆相关逻辑。

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

### 3.2 保存当前 Mod 的翻译并自动提交 Git

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository
    participant DB as SQLite
    participant Git as IGitService(GitService)

    U->>VM: 点击 "Save" 当前选中 Mod
    VM->>VM: 检查 SelectedMod 是否为空
    VM->>Scope: CreateScope()
    Scope-->>VM: IServiceScope

    VM->>Repo: GetModAsync(SelectedMod.UniqueID)
    Repo->>DB: 查询 ModMetadata
    DB-->>Repo: 返回记录
    Repo-->>VM: ModMetadata

    VM->>Repo: 更新 TranslatedName/TranslatedDescription, UpsertModAsync
    Repo->>DB: 保存变更

    VM->>Git: Commit(appDataPath, message)
    Git->>Git: 使用 LibGit2Sharp 提交所有改动

    VM->>VM: Status = "Saved '...' successfully."
```

### 3.3 导入旧版 xlgChineseBack.json

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant TS as ITranslationService(TranslationService)
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository
    participant DB as SQLite
    participant Git as IGitService

    U->>VM: 点击 "Import Legacy Data"
    VM->>TS: ImportFromLegacyJsonAsync(backupPath)

    TS->>FS: 读取 xlgChineseBack.json
    TS->>Scope: CreateScope()
    Scope-->>TS: IServiceScope

    TS->>Repo: GetModAsync/UpsertModAsync(基于 UniqueID)
    Repo->>DB: 插入/更新 ModMetadata (TranslatedName/Description)

    TS-->>VM: (successCount, errorCount, message)
    VM->>Git: Commit(appDataPath, "Import legacy translations")
    VM->>VM: LoadHistory(), LoadModsAsync()
```

### 3.4 将数据库翻译应用到 manifest.json

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant TS as ITranslationService(TranslationService)
    participant Scope as IServiceScopeFactory
    participant Repo as IModRepository
    participant DB as SQLite
    participant FS as FileSystem

    U->>VM: 点击 "Apply Translations"
    VM->>TS: ApplyTranslationsAsync(ModsDirectory)

    TS->>Scope: CreateScope()
    Scope-->>TS: IServiceScope
    TS->>Repo: GetAllModsAsync()
    Repo->>DB: 查询所有 ModMetadata
    DB-->>Repo: 列表
    Repo-->>TS: IEnumerable<ModMetadata>

    loop 每个有翻译的 Mod
        TS->>FS: 读取 manifest.json 原始文本
        TS->>TS: 使用 Regex 替换 "Name"/"Description" 的值
        TS->>FS: 写回 manifest.json
    end

    TS-->>VM: (appliedCount, errorCount, message)
    VM->>VM: Status = message
```

### 3.5 Git 回滚

```mermaid
sequenceDiagram
    participant U as User
    participant VM as MainViewModel
    participant Git as IGitService(GitService)

    U->>VM: 选中某个 Commit, 点击 "Rollback"
    VM->>VM: 检查 SelectedCommit 是否为空
    VM->>Git: Reset(appDataPath, SelectedCommit.FullHash)
    Git->>Git: 使用 LibGit2Sharp Reset(ResetMode.Hard)

    VM->>VM: Status = $"Rolled back to '{ShortHash}'."
    note over VM: 当前实现中，用户需再次点击 Scan Mods 刷新 UI 数据
```

