using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMTMS.Core.Models;
using SMTMS.Core.Interfaces;
using SMTMS.Avalonia.Messages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    
    // 是否为新增模组
    private bool _isNew;

    public ModViewModel(ModManifest manifest, ModMetadata? metadata = null, bool isNew = false)
    {
        _manifest = manifest;
        _metadata = metadata;
        _isNew = isNew || metadata == null;
        
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

    // 0 = Normal, 1 = Changed, 2 = New/Untracked
    [ObservableProperty]
    private int _rowStatus;

    // IsNew moved to property with backing field

    // 翻译状态
    [ObservableProperty]
    private bool _isTranslatingName;

    [ObservableProperty]
    private bool _isTranslatingDescription;

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
        OnPropertyChanged(nameof(IsNew));
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (IsNew)
        {
            TranslationStatus = "New / Untracked";
            HasLocalChanges = false;
            RowStatus = 2; // New
            return;
        }

        DbTranslatedName = _metadata?.TranslatedName;
        DbTranslatedDescription = _metadata?.TranslatedDescription;

        var nameMatch = string.IsNullOrEmpty(_metadata?.TranslatedName) || _metadata?.TranslatedName == Name;
        var descMatch = string.IsNullOrEmpty(_metadata?.TranslatedDescription) || _metadata?.TranslatedDescription == Description;

        if (nameMatch && descMatch)
        {
            TranslationStatus = "Synced";
            HasLocalChanges = false;
            RowStatus = 0; // Normal
        }
        else
        {
            TranslationStatus = "Changed (Local differs from DB)";
            HasLocalChanges = true;
            RowStatus = 1; // Changed
        }
    }

    public ModManifest Manifest => _manifest;
    
    public bool IsNew => _isNew;

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

    /// <summary>
    /// 翻译名称
    /// </summary>
    [RelayCommand]
    public async Task TranslateNameAsync()
    {
        if (IsTranslatingName || string.IsNullOrWhiteSpace(Name))
            return;

        IsTranslatingName = true;
        try
        {
            var translationService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<ITranslationApiService>();
            var settingsService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IServiceScopeFactory>()?.
                CreateScope().ServiceProvider.GetService<ISettingsService>();

            if (translationService == null || settingsService == null)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage("翻译服务未初始化", StatusLevel.Error));
                return;
            }

            var settings = await settingsService.GetSettingsAsync();
            var translatedText = await translationService.TranslateAsync(
                Name,
                settings.TranslationTargetLang,
                settings.TranslationSourceLang);

            if (!string.IsNullOrWhiteSpace(translatedText) && translatedText != Name)
            {
                Name = translatedText;
                WeakReferenceMessenger.Default.Send(new StatusMessage("名称翻译完成", StatusLevel.Success));
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage($"翻译失败: {ex.Message}", StatusLevel.Error));
        }
        finally
        {
            IsTranslatingName = false;
        }
    }

    /// <summary>
    /// 翻译描述
    /// </summary>
    [RelayCommand]
    public async Task TranslateDescriptionAsync()
    {
        if (IsTranslatingDescription || string.IsNullOrWhiteSpace(Description))
            return;

        IsTranslatingDescription = true;
        try
        {
            var translationService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<ITranslationApiService>();
            var settingsService = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IServiceScopeFactory>()?.
                CreateScope().ServiceProvider.GetService<ISettingsService>();

            if (translationService == null || settingsService == null)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage("翻译服务未初始化", StatusLevel.Error));
                return;
            }

            var settings = await settingsService.GetSettingsAsync();
            var translatedText = await translationService.TranslateAsync(
                Description,
                settings.TranslationTargetLang,
                settings.TranslationSourceLang);

            if (!string.IsNullOrWhiteSpace(translatedText) && translatedText != Description)
            {
                Description = translatedText;
                WeakReferenceMessenger.Default.Send(new StatusMessage("描述翻译完成", StatusLevel.Success));
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage($"翻译失败: {ex.Message}", StatusLevel.Error));
        }
        finally
        {
            IsTranslatingDescription = false;
        }
    }

    /// <summary>
    /// 放弃本次变更，恢复到原始值
    /// </summary>
    [RelayCommand]
    public void DiscardChanges()
    {
        if (!IsDirty) return;

        // 恢复原始值
        if (Name != _originalName) Name = _originalName;
        if (Author != _originalAuthor) Author = _originalAuthor;
        if (Version != _originalVersion) Version = _originalVersion;
        if (Description != _originalDescription) Description = _originalDescription;
        
        // 重置脏状态
        ResetDirtyState();
        
        // 更新 UI 状态
        UpdateStatus();
        
        WeakReferenceMessenger.Default.Send(new StatusMessage("已放弃本次变更", StatusLevel.Info));
    }
}
