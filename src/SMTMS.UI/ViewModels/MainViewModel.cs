using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using SMTMS.Core.Interfaces;

using SMTMS.Core.Aspects;
using SMTMS.Core.Models;

namespace SMTMS.UI.ViewModels;

[Log]
public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;
    private readonly IGitService _gitService;
    private readonly IGamePathService _gamePathService;
    private readonly ITranslationService _translationService;

    [ObservableProperty]
    private string _applicationTitle = "SMTMS - Stardew Mod Translation & Management System";

    [ObservableProperty]
    private string _modsDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods"; // Default path, can be configurable
    
    [ObservableProperty]
    private string _gitStatusLog = "Git Ready.";

    [ObservableProperty]
    private string _commitMessage = "";

    [ObservableProperty]
    private ModViewModel? _selectedMod;



    [ObservableProperty]
    private GitCommitModel? _selectedCommit;

    public ObservableCollection<ModViewModel> Mods { get; } = new();
    public ObservableCollection<GitCommitModel> CommitHistory { get; } = new();

    public MainViewModel(IModService modService, IGitService gitService, IGamePathService gamePathService, ITranslationService translationService)
    {
        _modService = modService;
        _gitService = gitService;
        _gamePathService = gamePathService;
        _translationService = translationService;

        // Auto-detect mods path
        var detectedPath = _gamePathService.GetModsPath();
        if (!string.IsNullOrEmpty(detectedPath))
        {
            ModsDirectory = detectedPath;
        }
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        Mods.Clear();
        var manifests = await _modService.ScanModsAsync(ModsDirectory);
        
        foreach (var manifest in manifests)
        {
            Mods.Add(new ModViewModel(manifest));
        }

    }

    [RelayCommand]
    private async Task SaveModAsync()
    {
        if (SelectedMod == null)
        {
             GitStatusLog = "Save Error: No mod selected.";
             return;
        }

        if (string.IsNullOrEmpty(SelectedMod.ManifestPath))
        {
             GitStatusLog = "Save Error: Invalid manifest path.";
             return;
        }

        try
        {
            await _modService.WriteManifestAsync(SelectedMod.ManifestPath, SelectedMod.Manifest);
            GitStatusLog = $"Saved '{SelectedMod.Name}' successfully.";
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Save Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void GitStatus()
    {
        try
        {
            var status = _gitService.GetStatus(ModsDirectory);
            GitStatusLog = $"Status check: {status.Count()} changed files.";
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private void GitStage()
    {
        try
        {
            _gitService.StageAll(ModsDirectory);
            GitStatusLog = "All changes staged.";
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Stage Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GitCommit()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CommitMessage))
            {
                GitStatusLog = "Error: Commit message cannot be empty.";
                return;
            }

            // Fix: Stage all changes before committing
            _gitService.StageAll(ModsDirectory);

            // Hardcoded author for now
            _gitService.Commit(ModsDirectory, CommitMessage, "SMTMS User", "user@smtms.local");
            GitStatusLog = "Commit successful.";
            CommitMessage = ""; // Clear message
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Commit Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GitPull()
    {
        try
        {
            // Hardcoded author for now, should be configurable
            _gitService.Pull(ModsDirectory, "SMTMS User", "user@smtms.local");
            GitStatusLog = "Pull successful.";
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Pull Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadHistory()
    {
        try
        {
            var history = _gitService.GetHistory(ModsDirectory);
            CommitHistory.Clear();
            foreach (var commit in history)
            {
                CommitHistory.Add(commit);
            }
            GitStatusLog = "History loaded.";
        }
        catch (Exception ex)
        {
            GitStatusLog = $"History Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GitRollback()
    {
        if (SelectedCommit == null)
        {
             GitStatusLog = "Rollback Error: No commit selected.";
             return;
        }

        try
        {
            _gitService.Reset(ModsDirectory, SelectedCommit.FullHash);
            GitStatusLog = $"Rolled back to '{SelectedCommit.ShortHash}'.";
            // Refresh logic if needed
            LoadHistory();
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Rollback Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BackupTranslationsAsync()
    {
        try
        {
            GitStatusLog = "Backing up translations...";
            string backupPath = Path.Combine(ModsDirectory, "xlgChineseBack.json");
            var result = await _translationService.BackupTranslationsAsync(ModsDirectory, backupPath);
            
            if (result.successCount > 0)
            {
                 GitStatusLog = result.message;
            }
            else
            {
                 GitStatusLog = $"Backup Warning: {result.message}";
            }
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Backup Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreTranslationsAsync()
    {
         try
        {
            GitStatusLog = "Restoring translations...";
            string backupPath = Path.Combine(ModsDirectory, "xlgChineseBack.json");
            var result = await _translationService.RestoreTranslationsAsync(ModsDirectory, backupPath);

            GitStatusLog = result.message;
            
            // Reload mods to show changes
            if (result.restoredCount > 0)
            {
                await LoadModsAsync();
            }
        }
        catch (Exception ex)
        {
            GitStatusLog = $"Restore Error: {ex.Message}";
        }
    }
}
