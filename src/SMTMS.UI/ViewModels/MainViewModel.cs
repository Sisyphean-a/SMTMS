using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using SMTMS.Core.Interfaces;

using SMTMS.Core.Aspects;
using SMTMS.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace SMTMS.UI.ViewModels;

[Log]
public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;
    private readonly IGitService _gitService;
    private readonly IGamePathService _gamePathService;
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory; // Added

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

    public ObservableCollection<ModViewModel> Mods { get; } = new();
    public ObservableCollection<GitCommitModel> CommitHistory { get; } = new();

    public MainViewModel(
        IModService modService, 
        IGitService gitService, 
        IGamePathService gamePathService, 
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory) // Added scopeFactory
    {
        _modService = modService;
        _gitService = gitService;
        _gamePathService = gamePathService;
        _translationService = translationService;
        _scopeFactory = scopeFactory; // Assigned

        // Auto-detect mods path
        var detectedPath = _gamePathService.GetModsPath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            ModsDirectory = detectedPath;
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
        
        // 2. Sync with DB
        using (var scope = _scopeFactory.CreateScope())
        {
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
            
            foreach (var manifest in manifests)
            {
                var mod = await modRepo.GetModAsync(manifest.UniqueID);
                if (mod == null)
                {
                    mod = new SMTMS.Core.Models.ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        OriginalName = manifest.Name,
                        OriginalDescription = manifest.Description,
                        RelativePath = Path.GetRelativePath(ModsDirectory, Path.GetDirectoryName(manifest.ManifestPath)!)
                    };
                    await modRepo.UpsertModAsync(mod);
                }
                else
                {
                    // Update transient fields if needed, e.g. path might verify
                     mod.RelativePath = Path.GetRelativePath(ModsDirectory, Path.GetDirectoryName(manifest.ManifestPath)!);
                     await modRepo.UpsertModAsync(mod);
                }

                // Add to UI collection (using DB data)
                // We need to merge Manifest info (author, version) with DB info (Translation)
                // Current ModViewModel likely uses ModManifest. We might need to update ModViewModel to support metadata.
                // For now, let's inject valid data into ModViewModel
                
                var viewModel = new ModViewModel(manifest, _gitService, mod);
                
                // Do NOT overwrite Name with translation here. 
                // ModViewModel.UpdateStatus() will handle the comparison status.
                // Logic:
                // 1. Manifest is from Disk (Actual state)
                // 2. Mod (Metadata) is from DB (Stored Translation)
                // ViewModel will check if they match.
                
                Mods.Add(viewModel);
            }
        }
        
        StatusMessage = $"Loaded {Mods.Count} mods.";
        LoadHistory(); // Refresh history
    }

    [RelayCommand]
    private async Task SaveModAsync()
    {
        if (SelectedMod == null)
        {
             StatusMessage = "保存错误: 未选择模组。";
             return;
        }

        try
        {
             // Update DB
             using (var scope = _scopeFactory.CreateScope())
            {
                var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
                var mod = await modRepo.GetModAsync(SelectedMod.UniqueID);
                
                if (mod != null)
                {
                    mod.TranslatedName = SelectedMod.Name;
                    mod.TranslatedDescription = SelectedMod.Description;
                    mod.LastTranslationUpdate = DateTime.Now;
                    await modRepo.UpsertModAsync(mod);
                    
                    // Update VM metadata to reflect that DB is now synced
                    SelectedMod.UpdateMetadata(mod);
                }
            }
            
            // Also write to disk (manifest.json)
            // Ideally we do this via TranslationService logic or helper, but for now we write directly if we want to "Apply" immediately.
            // But the user workflow might be "Edit -> Save to DB" then "Restore/Apply".
            // However, usually "Save" implies saving to disk too.
            // Let's assume this Edit is for the DB + Disk.
            
            // Re-use logic or simple write? 
            // Let's just update the manifest on disk to match.
            if (!string.IsNullOrEmpty(SelectedMod.ManifestPath))
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(SelectedMod.Manifest, Newtonsoft.Json.Formatting.Indented);
                await File.WriteAllTextAsync(SelectedMod.ManifestPath, json);
            }

            // REMOVED: Auto-Commit. Now using manual SyncToDatabase.
            // var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            // _gitService.Commit(appDataPath, $"Update translation for {SelectedMod.Name}");

            StatusMessage = $"已保存 '{SelectedMod.Name}' (本地)。请点击 '同步到数据库' 以创建版本。";
            // LoadHistory(); // History won't change
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"History Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GitRollback()
    {
        if (SelectedCommit == null)
        {
             StatusMessage = "Rollback Error: No commit selected.";
             return;
        }

        try
        {
            // Release DB locks
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            
            await Task.Run(() => _gitService.Reset(appDataPath, SelectedCommit.FullHash));
            
            StatusMessage = $"Rolled back to '{SelectedCommit.ShortHash}'.";
            await LoadModsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rollback Error: {ex.Message}";
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

    [RelayCommand]
    private async Task SyncToDatabaseAsync()
    {
        // TODO: Show Dialog to get Commit Message
        string commitMessage = $"Sync update {DateTime.Now}";

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
