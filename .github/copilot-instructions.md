# SMTMS (Stardew Mod Translation System) - AI Coding Instructions

## Project Overview
SMTMS is a **non-invasive translation management tool** for Stardew Valley mods that separates translation data from mod files using SQLite + Git versioning. The core philosophy: **never lose translation work**, even when mods update.

## Architecture & Module Boundaries

### Layered Structure (Clean Architecture)
```
UI (WPF) → Core (Interfaces/Models/Services) → Data/GitProvider/NexusClient
```

- **SMTMS.Core**: Domain layer with ALL interfaces (`IModService`, `IGitService`, `IModRepository`, etc.), models (`ModManifest`, `ModMetadata`), and business services
- **SMTMS.Data**: EF Core + SQLite persistence (`AppDbContext`, `ModRepository`)
- **SMTMS.GitProvider**: LibGit2Sharp wrapper for automated Git operations in `%APPDATA%/SMTMS`
- **SMTMS.UI**: WPF entry point; configures DI, initializes `ServiceLocator`, owns ViewModels

### Critical Data Flow
1. **Extract**: `manifest.json` (disk) → `ModMetadata.TranslatedName/Description` (SQLite)
2. **Restore**: SQLite → `manifest.json` via **regex replacement** (preserves JSON comments)
3. **Versioning**: All DB operations trigger Git commits in isolated repo (not in Mods folder)

## Key Patterns & Conventions

### 1. Regex-Based Manifest Updates (Non-Destructive)
**Why**: SMAPI `manifest.json` files contain `//` comments that JSON parsers strip. We MUST preserve them.

```csharp
// ❌ NEVER serialize/deserialize to update manifest.json
// ✅ ALWAYS use Regex.Replace for in-place edits
Regex.Replace(content, @"(""Name""\s*:\s*"")[^""]*("")", $"${{1}}{escapedName}${{2}}")
```
See [TranslationService.cs](src/SMTMS.Core/Services/TranslationService.cs#L225-L235) for reference implementation.

### 2. ServiceLocator for AOP (LogAttribute)
Rougamo AOP aspects can't use constructor injection. Access services via:
```csharp
ServiceLocator.Provider.GetService<ILogger<T>>()
```
Initialized in [App.xaml.cs](src/SMTMS.UI/App.xaml.cs#L52) before MainWindow creation.

### 3. Scoped Repository Access in Singletons
`IModRepository` is registered as **Scoped** (EF DbContext lifecycle). Singleton services MUST use `IServiceScopeFactory`:
```csharp
using var scope = _scopeFactory.CreateScope();
var repo = scope.ServiceProvider.GetRequiredService<IModRepository>();
```
Example: [TranslationService.cs](src/SMTMS.Core/Services/TranslationService.cs#L34)

### 4. AOP Logging with [Log] Attribute
Mark classes/methods with `[Log]` for automatic entry/exit/exception logging via Rougamo.Fody:
```csharp
[Log] // Weaves ILogger calls at compile-time
public class TranslationService : ITranslationService { }
```
Config in `FodyWeavers.xml` for projects using aspects.

## Development Workflows

### Build & Run
```powershell
dotnet build SMTMS.sln
dotnet run --project src/SMTMS.UI/SMTMS.UI.csproj
```

### Database Migrations (EF Core)
```powershell
# From SMTMS.Data directory
dotnet ef migrations add <MigrationName> --startup-project ../SMTMS.UI/SMTMS.UI.csproj
dotnet ef database update --startup-project ../SMTMS.UI/SMTMS.UI.csproj
```

### Testing Git Operations Locally
The Git repo lives at `%APPDATA%/SMTMS/`. View commits:
```powershell
cd $env:APPDATA\SMTMS
git log --oneline
git show <commit-hash>
```

## Project-Specific Rules

### Data Storage Locations
- **SQLite DB**: `%APPDATA%/SMTMS/smtms.db` (excluded from Git via `.gitignore`)
- **Git Repo**: `%APPDATA%/SMTMS/` (tracks translation snapshots)
- **Mods Folder**: Read from/write to only; never create new files there
- **Game Path Detection**: Registry key at `HKEY_CURRENT_USER\Software\SMAPI.installer\install-path-1`

### Translation Semantics
- `OriginalName/Description`: First-seen values when mod is discovered
- `TranslatedName/Description`: Current user-edited values (what gets restored)
- Database is **source of truth** for translations, not the manifest files

### Error Handling
- `ModService.ReadManifestAsync` returns `null` on parse errors (catches all exceptions)
- `TranslationService` logs to console for file-level errors but continues processing other mods
- Git operations throw exceptions up to UI layer for user notification

## External Dependencies
- **Rougamo.Fody**: AOP weaving (requires `FodyWeavers.xml` in projects)
- **LibGit2Sharp**: All Git operations; no `git.exe` subprocess calls
- **Newtonsoft.Json**: Preferred over System.Text.Json for comment-aware parsing
- **CommunityToolkit.Mvvm**: ViewModels use `[ObservableProperty]`, `[RelayCommand]`

## Reference Files
- [ARCHITECTURE.md](docs/ARCHITECTURE.md): Mermaid diagrams of data flows and component relationships
- [DESIGN_PHILOSOPHY.md](docs/DESIGN_PHILOSOPHY.md): Core principles (Safety First, Non-Invasive, Visual)
- [docs/reference/](docs/reference/): Legacy Python scripts this tool replaces
