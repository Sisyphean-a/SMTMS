# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SMTMS (Stardew Mod Translation & Management System) is a cross-platform translation management tool for Stardew Valley mods. It provides a complete solution for mod localization with SQLite-based version control, ensuring translation work is never lost when mods update.

**Key Philosophy**: Non-invasive translation management that preserves JSON comments and formatting in manifest files.

## Build & Run Commands

### Build
```bash
# Build entire solution
dotnet build SMTMS.sln

# Build specific project
dotnet build src/SMTMS.Avalonia/SMTMS.Avalonia.csproj
```

### Run
```bash
# Run the application
dotnet run --project src/SMTMS.Avalonia/SMTMS.Avalonia.csproj

# Watch mode (auto-rebuild on changes)
dotnet watch run --project src/SMTMS.Avalonia/SMTMS.Avalonia.csproj
```

### Test
```bash
# Run all tests
dotnet test

# Run tests in specific project
dotnet test tests/SMTMS.Tests/SMTMS.Tests.csproj

# Run specific test
dotnet test --filter "FullyQualifiedName~ManifestTextReplacerTests"

# List available tests
dotnet test --list-tests
```

### Database Migrations
```bash
# Create new migration (from SMTMS.Data directory)
cd src/SMTMS.Data
dotnet ef migrations add <MigrationName> --startup-project ../SMTMS.Avalonia/SMTMS.Avalonia.csproj

# Apply migrations
dotnet ef database update --startup-project ../SMTMS.Avalonia/SMTMS.Avalonia.csproj
```

## Architecture Overview

### Project Structure
```
SMTMS.sln
├── SMTMS.Core          - Domain models, interfaces, core services
├── SMTMS.Data          - EF Core, SQLite, repositories
├── SMTMS.Translation   - Translation scan/restore/import services
├── SMTMS.NexusClient   - Nexus Mods API integration
└── SMTMS.Avalonia      - UI layer (MVVM with Avalonia)
```

### Layered Architecture (Clean Architecture)
```
UI (Avalonia) → Core (Interfaces/Models/Services) → Data/Translation/NexusClient
```

**Dependency Flow**: UI depends on Core; Data/Translation/NexusClient implement Core interfaces. Core has no dependencies on other layers.

### Key Domain Models

- **ModManifest**: Represents a mod's `manifest.json` structure (Name, Author, Version, Description, UniqueID, UpdateKeys, etc.)
- **ModMetadata**: Database entity storing translations and metadata (OriginalName/Description, TranslatedName/Description, NexusId, file hashes)
- **HistorySnapshot**: Version control snapshot (like a Git commit) with timestamp and message
- **ModTranslationHistory**: Incremental history record for a mod at a specific snapshot (stores JSON content only when changed)
- **ModDiffModel**: Represents changes between versions (Added/Modified/Deleted with field-level diffs)

### Data Storage

- **SQLite Database**: `%APPDATA%/SMTMS/smtms.db` (Windows) or `~/.local/share/SMTMS/smtms.db` (Linux/Mac)
- **Mods Folder**: Detected via Windows Registry (`HKEY_CURRENT_USER\Software\SMAPI.installer\install-path-1`) or manual selection
- **Database is source of truth**: Translations are stored in DB, not in manifest files

## Critical Implementation Patterns

### 1. Non-Destructive Manifest Updates (Regex-Based)

**CRITICAL**: SMAPI `manifest.json` files contain `//` comments that standard JSON parsers strip. We MUST preserve them.

```csharp
// ❌ NEVER serialize/deserialize to update manifest.json
var manifest = JsonConvert.DeserializeObject<ModManifest>(content);
manifest.Name = newName;
File.WriteAllText(path, JsonConvert.SerializeObject(manifest));

// ✅ ALWAYS use ManifestTextReplacer for in-place regex edits
var updatedContent = ManifestTextReplacer.ReplaceName(content, newName);
File.WriteAllText(path, updatedContent);
```

See `SMTMS.Core/Helpers/ManifestTextReplacer.cs` for reference implementation.

### 2. Incremental History Storage

`ModTranslationHistory` uses hash-based deduplication:
- Only stores JSON content when it changes (detected via MD5 hash)
- Reduces storage by avoiding duplicate snapshots
- See `TranslationScanService.SaveTranslationsToDbAsync()` for implementation

### 3. Repository Scoping in Singleton Services

`IModRepository` and `IHistoryRepository` are registered as **Scoped** (tied to EF Core DbContext lifecycle). Singleton services MUST use `IServiceScopeFactory`:

```csharp
public class TranslationService : ITranslationService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public async Task DoWorkAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        // Use repo...
    }
}
```

### 4. MVVM with Messaging

Uses CommunityToolkit.Mvvm:
- `[ObservableProperty]` for bindable properties
- `[RelayCommand]` for commands
- `WeakReferenceMessenger` for decoupled ViewModel communication

Key messages:
- `StatusMessage`: Status updates with level (Info/Warning/Error/Success)
- `ModsDirectoryChangedMessage`: Directory changed
- `HistoryAppliedMessage`: History version applied to mod
- `RefreshModsRequestMessage`: Request to reload mods

## Key Workflows

### Sync to Database
1. User clicks "Sync to Database" → shows commit message dialog
2. `TranslationScanService.SaveTranslationsToDbAsync()`:
   - Scans manifest files (one level deep)
   - Computes MD5 hashes for change detection
   - Skips unchanged files
   - Creates/updates `ModMetadata` records
   - Creates `ModTranslationHistory` records (only for changed content)
   - Creates `HistorySnapshot` (if changes detected)
   - Batch saves to database
3. Refreshes UI (HistoryViewModel, ModListViewModel)

### Restore from Database
1. User clicks "Restore from Database"
2. `TranslationRestoreService.RestoreTranslationsFromDbAsync()`:
   - Gets all translated mods from database
   - Finds all manifest.json files in directory
   - For each file (parallel processing):
     - Parses manifest
     - Looks up translation in database
     - Applies translations using `ManifestTextReplacer`
     - Writes back if changed
3. Refreshes UI

### Rollback (Destructive Reset)
1. User selects snapshot and clicks "Rollback"
2. `TranslationService.RollbackSnapshotAsync()`:
   - Gets history for selected snapshot
   - Applies history state to `ModMetadata` in database
   - Restores files from database
3. `HistoryRepository.DeleteSnapshotsAfterAsync()`:
   - Deletes all snapshots with ID > selected snapshot
   - Deletes associated history records
   - Ensures clean history continuation (prevents "reverse changes" on next sync)
4. Refreshes UI

## Translation Semantics

- **OriginalName/Description**: First-seen values when mod is discovered (immutable reference)
- **TranslatedName/Description**: Current user-edited values (what gets restored to files)
- **Database is source of truth**: Translations live in DB, manifest files are derived
- **NexusId**: Parsed from `UpdateKeys` array (e.g., `"Nexus:12345"`)
  - `IsNexusIdUserAdded`: Distinguishes user-added vs. built-in Nexus IDs
  - User-added IDs are editable; built-in IDs are read-only

## Service Registration (Dependency Injection)

Configured in `SMTMS.Avalonia/App.axaml.cs`:

**Infrastructure**:
- `IFileSystem` → `PhysicalFileSystem` (Singleton)
- `IModService` → `ModService` (Singleton)
- `IGamePathService` → `RegistryGamePathService` (Windows) / `ManualGamePathService` (Mac/Linux) (Singleton)

**Data Access**:
- `AppDbContext` (Scoped - per operation)
- `IModRepository` → `ModRepository` (Scoped)
- `IHistoryRepository` → `HistoryRepository` (Scoped)

**Domain Services**:
- `IDiffService` → `DiffService` (Singleton)
- `ITranslationService` → `TranslationService` (Singleton)
- `ISettingsService` → `SettingsService` (Scoped)
- `ITranslationApiService` → `GoogleTranslationService` (Singleton)

**UI Services**:
- `IFolderPickerService` → `AvaloniaFolderPickerService` (Singleton)
- `ICommitMessageService` → `AvaloniaCommitMessageService` (Singleton)

**ViewModels**: All registered as Transient

## Database Schema

### ModMetadata (Primary Table)
- **UniqueID** (PK): Mod's unique identifier
- **OriginalName/Description**: First-scanned values
- **TranslatedName/Description**: Current translations
- **RelativePath**: Path relative to Mods folder
- **LastFileHash**: MD5 hash for change detection
- **NexusId**: Nexus Mods ID
- **IsNexusIdUserAdded**: User-added vs. built-in flag
- **LastTranslationUpdate**: Timestamp of last change
- **Nexus metadata**: Summary, description, image URL, download/endorsement counts

### HistorySnapshot
- **Id** (PK): Auto-incremented snapshot ID
- **Timestamp**: When snapshot was created
- **Message**: Commit message
- **TotalMods**: Number of mods at this snapshot

### ModTranslationHistory (Incremental Storage)
- **Id** (PK): Auto-incremented
- **SnapshotId** (FK): Associated snapshot
- **ModUniqueId** (FK): Mod identifier
- **JsonContent**: Complete JSON content at this snapshot
- **PreviousHash**: MD5 hash (for deduplication)

**Key Design**: Only stores JSON when content changes (hash comparison).

## Important Files

### Core Services
- `SMTMS.Core/Services/ModService.cs`: Manifest file I/O
- `SMTMS.Core/Services/DiffService.cs`: Text-based diff comparison
- `SMTMS.Core/Helpers/ManifestTextReplacer.cs`: Regex-based JSON updates (preserves comments)

### Translation Services
- `SMTMS.Translation/Services/TranslationService.cs`: High-level coordinator
- `SMTMS.Translation/Services/TranslationScanService.cs`: Scans mods and saves to DB
- `SMTMS.Translation/Services/TranslationRestoreService.cs`: Restores translations from DB to files

### Data Layer
- `SMTMS.Data/Context/AppDbContext.cs`: EF Core DbContext
- `SMTMS.Data/Repositories/ModRepository.cs`: Mod metadata persistence
- `SMTMS.Data/Repositories/HistoryRepository.cs`: Version control operations

### ViewModels
- `SMTMS.Avalonia/ViewModels/MainViewModel.cs`: Root shell (manages global state)
- `SMTMS.Avalonia/ViewModels/ModListViewModel.cs`: Mod list display and editing
- `SMTMS.Avalonia/ViewModels/ModViewModel.cs`: Individual mod with translation state
- `SMTMS.Avalonia/ViewModels/HistoryViewModel.cs`: Version history and rollback

## Testing

Tests are in `tests/SMTMS.Tests/`:
- Focus on `ManifestTextReplacer` (critical for non-destructive updates)
- Tests verify comment preservation, special character escaping, and edge cases

## External Dependencies

- **Avalonia 11.2.3**: Cross-platform UI framework
- **Semi.Avalonia**: UI theme (Semi Design)
- **Entity Framework Core 8.0**: ORM with SQLite
- **CommunityToolkit.Mvvm 8.4.0**: MVVM helpers
- **Newtonsoft.Json**: JSON parsing (preferred over System.Text.Json for comment-aware parsing)
- **DiffPlex**: Text diff comparison
- **MessagePack**: Serialization for ModDiffModel

## Platform-Specific Notes

### Windows
- Game path detected via Registry: `HKEY_CURRENT_USER\Software\SMAPI.installer\install-path-1`
- Data stored in `%APPDATA%\SMTMS\`

### macOS/Linux
- Game path requires manual selection
- Data stored in `~/.local/share/SMTMS/`

## Migration from WPF to Avalonia

This project was migrated from WPF to Avalonia for cross-platform support. The architecture remains similar (MVVM), but:
- UI framework changed from WPF to Avalonia
- Removed Git integration (LibGit2Sharp) in favor of built-in history system
- Simplified to SQLite-only storage (no external Git repo)
