using CommunityToolkit.Mvvm.ComponentModel;
using SMTMS.Core.Models;
using Newtonsoft.Json;

namespace SMTMS.Avalonia.ViewModels;

public partial class ModHistoryItemViewModel : ObservableObject
{
    private readonly ModTranslationHistory _currentHistory;
    private readonly ModManifest? _currentManifest;
    
    // 显示属性
    public int SnapshotId => _currentHistory.SnapshotId;
    public string SnapshotTime => _currentHistory.Snapshot?.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
    
    // 我们仅在与列表中的“上一个”版本（按时间顺序更早的版本）相比发生更改时显示这些内容
    // 需求说明：“如果描述没有变化，就空着”
    // 这意味着我们需要知道“上一个”值来决定是否显示当前值。

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _displayDescription = string.Empty;

    [ObservableProperty]
    private string _displayAuthor = string.Empty;
    
    [ObservableProperty]
    private string _displayVersion = string.Empty;

    public ModManifest? Manifest => _currentManifest;

    public ModHistoryItemViewModel(ModTranslationHistory history, ModTranslationHistory? olderHistory)
    {
        _currentHistory = history;

        // 解析当前版本
        try
        {
            if (!string.IsNullOrEmpty(history.JsonContent))
                _currentManifest = JsonConvert.DeserializeObject<ModManifest>(history.JsonContent);
        }
        catch
        {
            // ignored
        }

        // 解析旧版本
        ModManifest? olderManifest = null;
        try
        {
            if (olderHistory != null && !string.IsNullOrEmpty(olderHistory.JsonContent))
                olderManifest = JsonConvert.DeserializeObject<ModManifest>(olderHistory.JsonContent);
        }
        catch
        {
            // ignored
        }

        // 决定显示什么
        // 规则：如果与旧版本不同，则显示值。
        // 如果 olderHistory 为 null（意味着这是第一条记录），我们要显示所有内容。
        
        var curName = _currentManifest?.Name ?? "";
        var oldName = olderManifest?.Name ?? "";
        DisplayName = (olderHistory == null || curName != oldName) ? curName : "";

        var curDesc = _currentManifest?.Description ?? "";
        var oldDesc = olderManifest?.Description ?? "";
        DisplayDescription = (olderHistory == null || curDesc != oldDesc) ? curDesc : "";
        
        var curAuth = _currentManifest?.Author ?? "";
        var oldAuth = olderManifest?.Author ?? "";
        DisplayAuthor = (olderHistory == null || curAuth != oldAuth) ? curAuth : "";

        var curVer = _currentManifest?.Version ?? "";
        var oldVer = olderManifest?.Version ?? "";
        DisplayVersion = (olderHistory == null || curVer != oldVer) ? curVer : "";
    }
}
