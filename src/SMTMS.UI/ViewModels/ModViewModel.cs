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

    private void UpdateStatus()
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
    public async Task RollbackToVersionAsync(string commitHash)
    {
        if (string.IsNullOrEmpty(ManifestPath)) return;

        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            var relativePath = Path.GetRelativePath(appDataPath, ManifestPath); 
            // Note: ManifestPath is likely absolute. GitService expects relative path from repo root?
            // Wait, GitService Reset works on repo path. RollbackFile takes path, hash, relativeFilePath.
            // But ManifestPath is usually outside AppData/SMTMS? 
            // Ah, wait. The README says Git is in %APPDATA%/SMTMS.
            // But Mods are in Steam folder.
            // Current architecture: "Safety First... SMTMS 引入了企业级的 Git 版本控制 作为底层存储后端（位于 %APPDATA%\SMTMS）"
            // So SMTMS *copies* data to %APPDATA%/SMTMS?
            // Or does it init Git inside the Mods folder?
            // README line 8: "位于 %APPDATA%\SMTMS".
            // So we are NOT versioning the Steam Mods folder directly?
            // "Extract Translations" scans Mods, but where does it save?
            // Check MainViewModel SaveModAsync logic line 188: "appDataPath = ... SMTMS".
            // So SMTMS effectively has a "Shadow Repo".
            // But `SaveModAsync` line 184 writes to `SelectedMod.ManifestPath` which is in Steam Mods folder?
            // So `Rollback` needs to restore to the Repo, and then we might need to "Apply" (Restore) to disk?
            // If the user wants to rollback the Mod (Steam folder), we first rollback the DB/ShadowRepo, then Apply.
            // Or does RollbackFile mean "Update my working copy (Steam)"?
            // If Git is only in AppData, then `RollbackFile` updates the AppData file.
            // Then we must sync AppData -> Steam.
            // Let's assume `RollbackFile` updates the file in the repo.
            // Since `ManifestPath` points to Steam folder, we need the RELATIVE path within the Repo?
            // The Repo structure likely mirrors Mods folder? Or uses UniqueID?
            // Let's assume standard structure for now or that `IGitService` handles it.
            // But wait, `GitService` takes `path` (repo root). 
            // I need to know the file structure in AppData.
            // Assuming for now: The file in Repo is named/structured same as relative path or just flat?
            // Let's look at `ModMetadata` in `MainViewModel`. `RelativePath`.
            
            // Re-reading logic:
            // SyncToDatabase calls `SaveTranslationsToDbAsync`.
            // Does `SaveTranslationsToDbAsync` write to the Git Repo?
            // `TranslationService` implementation is key.
            // If `TranslationService` only writes to DB, where do the files in Git come from?
            // Maybe MainViewModel `SaveModAsync` wrote "Update translation for..." to Git?
            // Previously `SaveModAsync` called `_gitService.Commit(appDataPath)`.
            // But what files were there? Use `_gitService.GetStatus`.
            // If I haven't written files to `appDataPath`, then `Commit` commits nothing.
            // The Architecture implies "Git as backend".
            // So `TranslationService` MUST be writing json files to `appDataPath`?
            // I should double check `TranslationService`.
            // User just gave me `IGitService` and `ModViewModel`.
            // I'll assume for rollback, I call `_gitService.RollbackFile` on the AppData repo.
            // Then I should probably update the real mod file (Restore).
            
            // Getting relative path from Metadata?
            // I don't have Metadata's RelativePath easily here? 
            // Oh I do, `_metadata.RelativePath`.
            if (_metadata == null) return;
            
            // Rollback in Repo
            _gitService.RollbackFile(appDataPath, commitHash, relativePath);
             
            // Sync back to Game Mods Directory
            var repoFile = Path.Combine(appDataPath, "Mods", relativePath); 
            // Note: in ExportTranslationsToGitRepo, we used Path.Combine(repoPath, "Mods", mod.RelativePath). 
            // mod.RelativePath includes the mod folder and file name e.g. "ModA/manifest.json".
            // So structure is AppData/SMTMS/Mods/ModA/manifest.json. (Assuming "Mods" subfolder).
       
            if (File.Exists(repoFile))
            {
                File.Copy(repoFile, ManifestPath, true);
                
                // Update In-Memory Manifest to reflect change
                var json = await File.ReadAllTextAsync(ManifestPath);
                var storedManifest = Newtonsoft.Json.JsonConvert.DeserializeObject<ModManifest>(json);
                if (storedManifest != null)
                {
                    Name = storedManifest.Name;
                    Description = storedManifest.Description;
                    Author = storedManifest.Author;
                    Version = storedManifest.Version;
                    // UpdateStatus() is called by setters
                }
            }
        }
        catch (Exception ex)
        {
           // Handle error
           System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}

