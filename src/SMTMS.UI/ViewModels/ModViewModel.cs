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
        try
        {
            // 解析依赖
            var scopeFactory = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
            
            // 创建 History VM
            var historyVm = new ModHistoryViewModel(UniqueID, scopeFactory);
            
            // 订阅 Apply 事件
            historyVm.OnApplyHistory += (selectedManifest) =>
            {
                if (selectedManifest == null) return;
                
                // 只保留用户需要的字段：名称和描述（也许还有作者？）
                // 用户需求：“应用选中行的名称/描述”
                // History item VM 逻辑决定显示什么，但传回的是完整的 Manifest 对象。
                // 此外用户说：“如果我选了第一行... 名称从 C 变为 B”。
                // 所以我们直接取快照 Manifest 中的值。
                
                if (!string.IsNullOrEmpty(selectedManifest.Name))
                {
                    Name = selectedManifest.Name;
                }
                
                if (!string.IsNullOrEmpty(selectedManifest.Description))
                {
                    Description = selectedManifest.Description;
                }
                
                // 如果需要作者/版本，也可以在这里添加。
                // 目前用户强调了名称/描述。
                // 为了保险起见，我们把作者也加上，因为它是标识的一部分。
                if (!string.IsNullOrEmpty(selectedManifest.Author))
                {
                    Author = selectedManifest.Author;
                }
                
                WeakReferenceMessenger.Default.Send(new StatusMessage($"已应用历史版本: {selectedManifest.Name}", StatusLevel.Success));
            };

            // 打开窗口
            // 理想情况下应该使用 WindowService，但为了简化直接实例化
            var window = new SMTMS.UI.Views.ModHistoryWindow(historyVm);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
             var errorMsg = $"打开历史失败: {ex.Message}";
             if (ex.InnerException != null)
             {
                 errorMsg += $"\nInner: {ex.InnerException.Message}";
             }
             WeakReferenceMessenger.Default.Send(new StatusMessage(errorMsg, StatusLevel.Error));
        }
    }
}
