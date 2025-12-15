using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SMTMS.Core.Interfaces;

using SMTMS.Core.Aspects;

namespace SMTMS.UI.ViewModels;

[Log]
public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;
    private readonly IGitService _gitService;
    private readonly IGamePathService _gamePathService;

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

    public ObservableCollection<ModViewModel> Mods { get; } = new();

    public MainViewModel(IModService modService, IGitService gitService, IGamePathService gamePathService)
    {
        _modService = modService;
        _gitService = gitService;
        _gamePathService = gamePathService;

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
}
