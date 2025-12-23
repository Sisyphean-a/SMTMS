using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMTMS.Core.Models;
using SMTMS.UI.Messages;

namespace SMTMS.UI.ViewModels;

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
        // 简单发送消息，让 HistoryViewModel 处理或显示提示
        // 暂时只显示提示，因为 HistoryViewModel 目前没有 "Filter By Mod" 的公开方法 (虽然有 GetHistoryForModAsync 接口)
        WeakReferenceMessenger.Default.Send(new StatusMessage($"请在“历史记录”页签中查看全局历史 (暂不支持单模组历史弹窗)", StatusLevel.Info));
    }
}
