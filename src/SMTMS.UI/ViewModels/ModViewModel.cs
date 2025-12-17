using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using System.IO;
using Newtonsoft.Json;

namespace SMTMS.UI.ViewModels;

public partial class ModViewModel : ObservableObject
{
    private readonly ModManifest _manifest;
    private readonly IGitService _gitService; // Added
    private ModMetadata? _metadata;

    public ModViewModel(ModManifest manifest, IGitService gitService, ModMetadata? metadata = null) // Updated signature
    {
        _manifest = manifest;
        _gitService = gitService;
        _metadata = metadata;
        UpdateStatus();
    }
    
    // Status Logic
    [ObservableProperty]
    private string _translationStatus = "Unknown";

    [ObservableProperty]
    private bool _hasLocalChanges;

    [ObservableProperty]
    private string? _dbTranslatedName;

    [ObservableProperty]
    private string? _dbTranslatedDescription;
    
    public void UpdateMetadata(ModMetadata metadata)
    {
        _metadata = metadata;
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (_metadata == null)
        {
            TranslationStatus = "New / Untracked";
            HasLocalChanges = false;
            return;
        }

        DbTranslatedName = _metadata.TranslatedName;
        DbTranslatedDescription = _metadata.TranslatedDescription;

        bool nameMatch = string.IsNullOrEmpty(_metadata.TranslatedName) || _metadata.TranslatedName == Name;
        bool descMatch = string.IsNullOrEmpty(_metadata.TranslatedDescription) || _metadata.TranslatedDescription == Description;

        if (nameMatch && descMatch)
        {
            TranslationStatus = "Synced";
            HasLocalChanges = false;
        }
        else
        {
            // If DB has translation but local differs -> Changed (Update or Manual Edit)
            TranslationStatus = "Changed (Local differs from DB)";
            HasLocalChanges = true;
        }
    }

    public ModManifest Manifest => _manifest;

    public string? ManifestPath => _manifest.ManifestPath;

    public string Name
    {
        get => _manifest.Name;
        set
        {
            if (_manifest.Name != value)
            {
                _manifest.Name = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }
    
    public string Author
    {
        get => _manifest.Author;
        set
        {
            if (_manifest.Author != value)
            {
                _manifest.Author = value;
                OnPropertyChanged();
            }
        }
    }

    public string Version
    {
        get => _manifest.Version;
        set
        {
            if (_manifest.Version != value)
            {
                _manifest.Version = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => _manifest.Description;
        set
        {
            if (_manifest.Description != value)
            {
                _manifest.Description = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }

    public string UniqueID
    {
        get => _manifest.UniqueID;
        set
        {
            if (_manifest.UniqueID != value)
            {
                _manifest.UniqueID = value;
                OnPropertyChanged();
            }
        }
    }
    
    [RelayCommand]
    public async Task ShowHistoryAsync()
    {
        if (_metadata == null || string.IsNullOrEmpty(_metadata.RelativePath))
        {
             // Fallback or warning
             System.Windows.MessageBox.Show("Cannot show history: Mod metadata missing.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
             return;
        }

        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            // Based on architecture, files are in "Mods" subfolder in repo
            var repoRelativePath = Path.Combine("Mods", _metadata.RelativePath); 

            var history = await Task.Run(() => _gitService.GetFileHistory(appDataPath, repoRelativePath));

            var dialog = new SMTMS.UI.Views.ModHistoryDialog(Name, history);
            // dialog.Owner = System.Windows.Application.Current.MainWindow; // Optional

            if (dialog.ShowDialog() == true && dialog.SelectedCommit != null)
            {
                await RollbackToVersionAsync(dialog.SelectedCommit.FullHash, repoRelativePath);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading history: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task RollbackToVersionAsync(string commitHash, string repoRelativePath)
    {
        if (string.IsNullOrEmpty(ManifestPath)) return;

        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
           
            // 1. Rollback in Repo (Update Shadow Copy to old version)
            await Task.Run(() => _gitService.RollbackFile(appDataPath, commitHash, repoRelativePath));
             
            // 2. Sync back to Game Mods Directory (Restore)
            var repoFile = Path.Combine(appDataPath, repoRelativePath);
       
            if (File.Exists(repoFile))
            {
                // Backup current? No, rollback is destructive/overwrite as per requirement.
                File.Copy(repoFile, ManifestPath, true);
                
                // 3. Update In-Memory Manifest to reflect change
                var json = await File.ReadAllTextAsync(ManifestPath);
                var storedManifest = Newtonsoft.Json.JsonConvert.DeserializeObject<ModManifest>(json);
                if (storedManifest != null)
                {
                    Name = storedManifest.Name;
                    Description = storedManifest.Description;
                    Author = storedManifest.Author;
                    Version = storedManifest.Version;
                    UpdateStatus(); // Will likely show "Changed" because DB is still at HEAD (or different)
                }
                
                System.Windows.MessageBox.Show($"Rolled back '{Name}' to version {commitHash.Substring(0,7)}.", "Success");
            }
        }
        catch (Exception ex)
        {
           System.Windows.MessageBox.Show($"Rollback Error: {ex.Message}", "Error");
        }
    }
}

