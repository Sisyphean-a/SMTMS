using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using SMTMS.Core.Interfaces;

using SMTMS.Core.Aspects;
using SMTMS.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SMTMS.UI.ViewModels;

[Log]
public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;
    private readonly IGitService _gitService;
    private readonly IGamePathService _gamePathService;
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory; // Added

    // 保存前请求更新绑定的事件
    public event EventHandler? SaveRequested;

    [ObservableProperty]
    private string _applicationTitle = "SMTMS - Stardew Mod Translation & Management System";

    [ObservableProperty]
    private string _modsDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods"; // Default path, can be configurable
    
    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private ModViewModel? _selectedMod;

    [ObservableProperty]
    private GitCommitModel? _selectedCommit;

    [ObservableProperty]
    private ModDiffModel? _selectedDiffItem;

    public ObservableCollection<ModViewModel> Mods { get; } = new();
    public ObservableCollection<GitCommitModel> CommitHistory { get; } = new();
    public ObservableCollection<ModDiffModel> ModDiffChanges { get; } = new();

    public MainViewModel(
        IModService modService, 
        IGitService gitService, 
        IGamePathService gamePathService, 
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory)
    {
        _modService = modService;
        _gitService = gitService;
        _gamePathService = gamePathService;
        _translationService = translationService;
        _scopeFactory = scopeFactory; // Assigned

        // 优先从设置中加载上次保存的目录
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // 使用scope访问scoped服务
        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var settings = await settingsService.GetSettingsAsync();
        
        // 优先使用上次保存的目录
        if (!string.IsNullOrEmpty(settings.LastModsDirectory) && Directory.Exists(settings.LastModsDirectory))
        {
            ModsDirectory = settings.LastModsDirectory;
        }
        else
        {
            // 回退到自动检测
            var detectedPath = _gamePathService.GetModsPath();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                ModsDirectory = detectedPath;
            }
        }
        
        // Ensure Git is initialized in AppData/SMTMS
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var smtmsPath = Path.Combine(appDataPath, "SMTMS");
        // We probably need to pass this path to methods or GitService should know it.
        // For now, let's assume specific operations know the path or we configure GitService?
        // Actually GitService Init(path) is called.
        // We should ensure it's initialized on startup or first usage.
        if (!Directory.Exists(smtmsPath))
        {
            Directory.CreateDirectory(smtmsPath);
        }
        if (!_gitService.IsRepository(smtmsPath))
        {
            _gitService.Init(smtmsPath);
        }

        // Auto-scan if mods directory exists
        if (!string.IsNullOrEmpty(ModsDirectory) && Directory.Exists(ModsDirectory))
        {
            if (Directory.GetFiles(ModsDirectory).Length > 0 || Directory.GetDirectories(ModsDirectory).Length > 0)
            {
                // Fire and forget auto-scan safely
                _ = LoadModsAsync();
            }
        }
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        StatusMessage = "Scanning mods...";
        Mods.Clear();

        // 1. Scan files
        var manifests = await _modService.ScanModsAsync(ModsDirectory);
        var manifestList = manifests.ToList();

        // 2. Sync with DB (批量操作优化)
        using (var scope = _scopeFactory.CreateScope())
        {
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            // 批量获取所有 Mod 的元数据
            var uniqueIds = manifestList.Select(m => m.UniqueID).ToList();
            var existingMods = await modRepo.GetModsByIdsAsync(uniqueIds);

            var modsToUpdate = new List<SMTMS.Core.Models.ModMetadata>();

            foreach (var manifest in manifestList)
            {
                SMTMS.Core.Models.ModMetadata mod;

                if (!existingMods.TryGetValue(manifest.UniqueID, out mod!))
                {
                    // 新 Mod
                    mod = new SMTMS.Core.Models.ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        OriginalName = manifest.Name,
                        OriginalDescription = manifest.Description,
                        RelativePath = Path.GetRelativePath(ModsDirectory, Path.GetDirectoryName(manifest.ManifestPath)!)
                    };
                    modsToUpdate.Add(mod);
                }
                else
                {
                    // 更新路径（可能移动了）
                    var newRelativePath = Path.GetRelativePath(ModsDirectory, Path.GetDirectoryName(manifest.ManifestPath)!);
                    if (mod.RelativePath != newRelativePath)
                    {
                        mod.RelativePath = newRelativePath;
                        modsToUpdate.Add(mod);
                    }
                }

                // Add to UI collection (using DB data)
                var viewModel = new ModViewModel(manifest, _gitService, mod);
                Mods.Add(viewModel);
            }

            // 🔥 批量保存所有变更（一次数据库操作）
            if (modsToUpdate.Any())
            {
                await modRepo.UpsertModsAsync(modsToUpdate);
            }
        }

        StatusMessage = $"Loaded {Mods.Count} mods.";
        LoadHistory(); // Refresh history
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择Stardew Valley的Mods目录",
            ShowNewFolderButton = false,
            SelectedPath = ModsDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ModsDirectory = dialog.SelectedPath;
            
            // 保存到数据库
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settingsService.UpdateLastModsDirectoryAsync(ModsDirectory);
            
            StatusMessage = $"已设置Mods目录: {ModsDirectory}";
        }
    }

    [RelayCommand]
    private async Task SaveModAsync()
    {
        if (SelectedMod == null)
        {
             StatusMessage = "保存错误: 未选择模组。";
             return;
        }

        // 触发事件，让 View 更新所有 Explicit 绑定
        SaveRequested?.Invoke(this, EventArgs.Empty);

        try
        {
            // 使用正则表达式替换保留JSON注释
            if (!string.IsNullOrEmpty(SelectedMod.ManifestPath))
            {
                var content = await File.ReadAllTextAsync(SelectedMod.ManifestPath);
                bool changed = false;

                // 替换Name
                var manifest = SelectedMod.Manifest;
                string escapedName = JsonConvert.ToString(manifest.Name).Trim('"');
                if (Regex.IsMatch(content, @"""Name""\s*:\s*""[^""]*"""))
                {
                    string newContent = Regex.Replace(content, @"(""Name""\s*:\s*"")[^""]*("")", $"${{1}}{escapedName}${{2}}");
                    if (content != newContent)
                    {
                        content = newContent;
                        changed = true;
                    }
                }

                // 替换Author
                string escapedAuthor = JsonConvert.ToString(manifest.Author).Trim('"');
                if (Regex.IsMatch(content, @"""Author""\s*:\s*""[^""]*"""))
                {
                    string newContent = Regex.Replace(content, @"(""Author""\s*:\s*"")[^""]*("")", $"${{1}}{escapedAuthor}${{2}}");
                    if (content != newContent)
                    {
                        content = newContent;
                        changed = true;
                    }
                }

                // 替换Version - DISABLED (User request: prevent version changes)
                // string escapedVersion = JsonConvert.ToString(manifest.Version).Trim('"');
                // if (Regex.IsMatch(content, @"""Version""\s*:\s*""[^""]*""")) ...

                // 替换Description
                string escapedDesc = JsonConvert.ToString(manifest.Description).Trim('"');
                if (Regex.IsMatch(content, @"""Description""\s*:\s*""[^""]*"""))
                {
                    string newContent = Regex.Replace(content, @"(""Description""\s*:\s*"")[^""]*("")", $"${{1}}{escapedDesc}${{2}}");
                    if (content != newContent)
                    {
                        content = newContent;
                        changed = true;
                    }
                }

                if (changed)
                {
                    await File.WriteAllTextAsync(SelectedMod.ManifestPath, content);
                }
            }

            // 重置IsDirty状态
            SelectedMod.ResetDirtyState();
            SelectedMod.UpdateStatus();
            
            StatusMessage = $"已保存 '{SelectedMod.Name}' (本地)。请点击 '同步到数据库' 以创建版本。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存错误: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void LoadHistory()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
             if (_gitService.IsRepository(appDataPath))
            {
                var history = _gitService.GetHistory(appDataPath);
                CommitHistory.Clear();
                foreach (var commit in history)
                {
                    CommitHistory.Add(commit);
                }
            }
            else
            {
                 StatusMessage = "History Warning: No repository found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"History Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GitRollback()
    {
        if (SelectedCommit == null || string.IsNullOrEmpty(SelectedCommit.FullHash))
        {
             StatusMessage = "Rollback Error: No valid commit selected.";
             return;
        }

        try
        {
            // Release DB locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            
            await Task.Run(() => _gitService.Reset(appDataPath, SelectedCommit.FullHash));
            
            // Sync DB with the reverted Git Repo state
            await _translationService.ImportTranslationsFromGitRepoAsync(appDataPath);
            
            // Auto-sync: Apply the restored DB state to game files
            // Wait, Reset reverted the whole repo, including smtms.db (if tracked) or files in Mods/? 
            // If smtms.db IS tracked, it reverted.
            // Then RestoreFromDatabaseAsync reads the reverted DB and updates manifests.
            // This syncs the Game Mods with the rolled-back state.
            await RestoreFromDatabaseAsync();

            StatusMessage = $"Rolled back to '{SelectedCommit.ShortHash}' and applied to files.";
            await LoadModsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rollback Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task HardResetAsync()
    {
        try
        {
             // Release DB locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            
            // 1. Delete .git folder
            await Task.Run(() => _gitService.DeleteRepository(appDataPath));

            // 2. Delete DB file
            var dbPath = Path.Combine(appDataPath, "smtms.db");
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
            
            // 3. Re-create DB Tables
            using (var scope = _scopeFactory.CreateScope())
            {
                 var context = scope.ServiceProvider.GetRequiredService<SMTMS.Data.Context.AppDbContext>();
                 context.Database.EnsureCreated();
            }

            StatusMessage = "Initialization complete. All history and data cleared.";
            CommitHistory.Clear();
            await LoadModsAsync(); // Rescan, will treat as new/untracked
            
            // Re-init git
             _gitService.Init(appDataPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Init Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportLegacyDataAsync()
    {
       // Deprecated or redirect to SaveTranslationsToDbAsync (Sync)
       await SyncToDatabaseAsync();
    }

    [RelayCommand]
    private async Task ApplyTranslationsAsync()
    {
        // This command was "Apply All Translations". 
        await RestoreFromDatabaseAsync();
    }

    [ObservableProperty]
    private string _diffText = "Select a commit to see changes.";

    [ObservableProperty]
    private bool _isLoadingDiff = false;

    [ObservableProperty]
    private string _diffLoadingMessage = "";

    partial void OnSelectedCommitChanged(GitCommitModel? value)
    {
        // 清空选中的 Diff 项
        SelectedDiffItem = null;

        if (value != null)
        {
            // 异步加载 Diff，避免阻塞 UI
            _ = LoadDiffAsync(value);
        }
        else
        {
            DiffText = "Select a commit to see changes.";
            ModDiffChanges.Clear();
        }
    }

    private async Task LoadDiffAsync(GitCommitModel commit)
    {
        IsLoadingDiff = true;
        DiffLoadingMessage = "正在加载变更...";
        ModDiffChanges.Clear();

        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            List<ModDiffModel> structuredDiff;

            // 使用 scope 访问 scoped 服务
            using (var scope = _scopeFactory.CreateScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<IGitDiffCacheService>();

                // 1. 先尝试从缓存读取
                DiffLoadingMessage = "正在检查缓存...";
                var cachedDiff = await cacheService.GetCachedDiffAsync(commit.FullHash);

                if (cachedDiff != null)
                {
                    // 缓存命中
                    DiffLoadingMessage = "从缓存加载...";
                    structuredDiff = cachedDiff;
                }
                else
                {
                    // 缓存未命中，计算 Diff
                    DiffLoadingMessage = "正在计算变更...";
                    structuredDiff = await Task.Run(() => _gitService.GetStructuredDiff(appDataPath, commit.FullHash).ToList());

                    // 保存到缓存（异步，不阻塞 UI）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var saveScope = _scopeFactory.CreateScope();
                            var saveCacheService = saveScope.ServiceProvider.GetRequiredService<IGitDiffCacheService>();
                            await saveCacheService.SaveDiffCacheAsync(commit.FullHash, structuredDiff);
                        }
                        catch
                        {
                            // 忽略缓存保存失败
                        }
                    });
                }
            }

            // 回到 UI 线程更新集合
            DiffLoadingMessage = "正在更新界面...";
            foreach (var diff in structuredDiff)
            {
                ModDiffChanges.Add(diff);
            }

            DiffText = $"共 {ModDiffChanges.Count} 个模组发生变更";
        }
        catch (Exception ex)
        {
            DiffText = $"Error loading diff: {ex.Message}";
            ModDiffChanges.Clear();
        }
        finally
        {
            IsLoadingDiff = false;
            DiffLoadingMessage = "";
        }
    }

    [RelayCommand]
    private async Task SyncToDatabaseAsync()
    {
        var dialog = new SMTMS.UI.Views.CommitDialog($"Scan & Update {DateTime.Now:yyyy/MM/dd HH:mm}");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string commitMessage = dialog.CommitMessage;

        StatusMessage = "正在同步到数据库...";
        try
        {
            // 1. Extract/Update DB
            await _translationService.SaveTranslationsToDbAsync(ModsDirectory);
            
            // 2. Export to Git Repo (Staging)
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            await _translationService.ExportTranslationsToGitRepo(ModsDirectory, appDataPath);

            // 3. Create Git Snapshot
            _gitService.CommitAll(appDataPath, commitMessage);

            StatusMessage = "同步成功：已创建新版本。";
            LoadHistory(); 
            await LoadModsAsync(); // Refresh status
        }
        catch (Exception ex)
        {
            StatusMessage = $"同步错误: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreFromDatabaseAsync()
    {
        // TODO: Show Dialog to select Version. Default to Latest.
        // For now, restoring latest means:
        // 1. Apply DB translations to manifests (RestoreTranslationsFromDbAsync)
        // 2. Or Git Reset to HEAD?
        // The user request says "Restore from Database -> manual select version -> default last".
        // "Restore" in this context (sync/restore pair) usually means "Pull from storage to disk".
        
        StatusMessage = "正在从数据库恢复...";
        try
        {
            // For now, act as "Apply latest translations from DB"
            await _translationService.RestoreTranslationsFromDbAsync(ModsDirectory);
            
            // If we want to restore file state from Git (e.g. deleted files?), we might need Git Reset.
            // _gitService.Reset(appDataPath, "HEAD"); 
            
            StatusMessage = "已恢复最新翻译。";
            await LoadModsAsync(); 
        }
        catch (Exception ex)
        {
            StatusMessage = $"恢复错误: {ex.Message}";
        }
    }
}
