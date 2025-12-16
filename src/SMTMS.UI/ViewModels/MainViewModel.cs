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
                
                var viewModel = new ModViewModel(manifest);
                // Apply overrides from DB
                if (!string.IsNullOrEmpty(mod.TranslatedName)) viewModel.Name = mod.TranslatedName; // ModViewModel should notify
                if (!string.IsNullOrEmpty(mod.TranslatedDescription)) viewModel.Description = mod.TranslatedDescription;

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
             StatusMessage = "Save Error: No mod selected.";
             return;
        }

        // Original manifest writing logic (if still needed, but DB is primary now)
        // if (string.IsNullOrEmpty(SelectedMod.ManifestPath))
        // {
        //      StatusMessage = "Save Error: Invalid manifest path.";
        //      return;
        // }

        try
        {
            // await _modService.WriteManifestAsync(SelectedMod.ManifestPath, SelectedMod.Manifest); // This is for writing to mod folder, not DB
            
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
                }
            }

            // Auto-Commit
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            _gitService.Commit(appDataPath, $"Update translation for {SelectedMod.Name}");

            StatusMessage = $"Saved '{SelectedMod.Name}' successfully.";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save Error: {ex.Message}";
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
                // StatusMessage = "History loaded."; // Too spammy if called often
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"History Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GitRollback()
    {
        if (SelectedCommit == null)
        {
             StatusMessage = "Rollback Error: No commit selected.";
             return;
        }

        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            _gitService.Reset(appDataPath, SelectedCommit.FullHash);
            StatusMessage = $"Rolled back to '{SelectedCommit.ShortHash}'.";
            
            // Reload mods from DB to reflect rollback
            // Trigger LoadModsAsync? Or just refresh current view?
            // LoadModsAsync requires async, RelayCommand(void) can't await easily unless async void. 
            // Better to refresh manually or just notify user. 
            // Ideally call LoadModsAsync();
            
            // Simplified: User must click "Scan Mods" to refresh UI data for now, or we force it if changed to Task.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rollback Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportLegacyDataAsync()
    {
        StatusMessage = "Importing legacy data...";
        try 
        {
            // Look for xlgChineseBack.json in mods dir
            string backupPath = Path.Combine(ModsDirectory, "xlgChineseBack.json");
            var result = await _translationService.ImportFromLegacyJsonAsync(backupPath);
            StatusMessage = result.message;
            
            if (result.successCount > 0)
            {
                // Commit the import
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
                _gitService.Commit(appDataPath, "Import legacy translations");
                LoadHistory();
                await LoadModsAsync(); // Refresh UI
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyTranslationsAsync()
    {
        StatusMessage = "Applying translations to manifests...";
        try
        {
            var result = await _translationService.ApplyTranslationsAsync(ModsDirectory);
            StatusMessage = result.message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply Error: {ex.Message}";
        }
    }
}
