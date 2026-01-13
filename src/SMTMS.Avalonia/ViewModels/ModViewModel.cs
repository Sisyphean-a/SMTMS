using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMTMS.Core.Models;
using SMTMS.Avalonia.Messages;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SMTMS.Avalonia.ViewModels;

public partial class ModViewModel : ObservableObject
{
    private readonly ModManifest _manifest;
    private ModMetadata? _metadata;

    // 原始值用于检测更改
    private string _originalName;
    private string _originalAuthor;
    private string _originalVersion;
    private string _originalDescription;

    public ModViewModel(ModManifest manifest, ModMetadata? metadata = null)
    {
        _manifest = manifest;
        _metadata = metadata;
        
        // 保存原始值
        _originalName = _manifest.Name;
        _originalAuthor = _manifest.Author;
        _originalVersion = _manifest.Version;
        _originalDescription = _manifest.Description;
        
        ParseNexusId();
        
        UpdateStatus();

        WeakReferenceMessenger.Default.Register<HistoryAppliedMessage>(this, OnHistoryApplied);
    }

    private void OnHistoryApplied(object recipient, HistoryAppliedMessage message)
    {
        if (message.Value.UniqueID != this.UniqueID) return;

        var selectedManifest = message.Value;

        if (!string.IsNullOrEmpty(selectedManifest.Name))
        {
            Name = selectedManifest.Name;
        }

        if (!string.IsNullOrEmpty(selectedManifest.Description))
        {
            Description = selectedManifest.Description;
        }
        
        if (!string.IsNullOrEmpty(selectedManifest.Author))
        {
            Author = selectedManifest.Author;
        }

        WeakReferenceMessenger.Default.Send(new StatusMessage($"已应用历史版本: {selectedManifest.Name}", StatusLevel.Success));
    }
    
    // 状态逻辑
    [ObservableProperty]
    private string _translationStatus = "Unknown";

    [ObservableProperty]
    private bool _hasLocalChanges;

    [ObservableProperty]
    private string? _dbTranslatedName;

    [ObservableProperty]
    private string? _dbTranslatedDescription;

    [ObservableProperty]
    private bool _isDirty;

    private void CheckDirty()
    {
        IsDirty = _manifest.Name != _originalName ||
                  _manifest.Author != _originalAuthor ||
                  _manifest.Version != _originalVersion ||
                  _manifest.Description != _originalDescription;
    }

    public void ResetDirtyState()
    {
        _originalName = _manifest.Name;
        _originalAuthor = _manifest.Author;
        _originalVersion = _manifest.Version;
        _originalDescription = _manifest.Description;
        IsDirty = false;
    }
    
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

        var nameMatch = string.IsNullOrEmpty(_metadata.TranslatedName) || _metadata.TranslatedName == Name;
        var descMatch = string.IsNullOrEmpty(_metadata.TranslatedDescription) || _metadata.TranslatedDescription == Description;

        if (nameMatch && descMatch)
        {
            TranslationStatus = "Synced";
            HasLocalChanges = false;
        }
        else
        {
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
            if (_manifest.Name == value) return;
            _manifest.Name = value;
            OnPropertyChanged();
            CheckDirty();
            UpdateStatus();
        }
    }
    
    public string Author
    {
        get => _manifest.Author;
        set
        {
            if (_manifest.Author == value) return;
            _manifest.Author = value;
            OnPropertyChanged();
            CheckDirty();
        }
    }

    public string Version
    {
        get => _manifest.Version;
        set
        {
            if (_manifest.Version == value) return;
            _manifest.Version = value;
            OnPropertyChanged();
            CheckDirty();
        }
    }

    public string Description
    {
        get => _manifest.Description;
        set
        {
            if (_manifest.Description == value) return;
            _manifest.Description = value;
            OnPropertyChanged();
            CheckDirty();
            UpdateStatus();
        }
    }

    public string UniqueID
    {
        get => _manifest.UniqueID;
        set
        {
            if (_manifest.UniqueID == value) return;
            _manifest.UniqueID = value;
            OnPropertyChanged();
        }
    }
    
    [RelayCommand]
    public void ShowHistory()
    {
        WeakReferenceMessenger.Default.Send(new OpenHistoryRequestMessage(UniqueID));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNexusUrl))]
    private string? _nexusId;

    public bool HasNexusUrl => !string.IsNullOrEmpty(NexusId);

    private void ParseNexusId()
    {
        if (_manifest.UpdateKeys == null || _manifest.UpdateKeys.Length == 0)
        {
            NexusId = null;
            return;
        }

        foreach (var key in _manifest.UpdateKeys)
        {
            var match = Regex.Match(key, @"Nexus:(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                NexusId = match.Groups[1].Value;
                break; 
            }
        }
    }

    [RelayCommand]
    public void OpenNexusPage()
    {
        if (string.IsNullOrEmpty(NexusId)) return;

        var url = $"https://www.nexusmods.com/stardewvalley/mods/{NexusId}";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // TODO: Log error or show notification
            Console.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
}
