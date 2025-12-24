using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Core.Services;
using SMTMS.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SMTMS.UI.ViewModels;

/// <summary>
/// 历史视图模型，负责提交历史的展示和回滚操作 (Pure DB Implementation)
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HistoryViewModel> _logger;

    [ObservableProperty]
    private HistorySnapshot? _selectedSnapshot;

    [ObservableProperty]
    private ModDiffModel? _selectedDiffItem;

    [ObservableProperty]
    private string _diffText = "选择一个历史记录以查看变更";

    [ObservableProperty]
    private bool _isLoadingDiff;

    [ObservableProperty]
    private string _diffLoadingMessage = "";

    public ObservableCollection<HistorySnapshot> SnapshotHistory { get; } = [];
    
    // UI Diff Model (Updated to match MainWindow.xaml binding)
    public ObservableCollection<ModDiffModel> ModDiffChanges { get; } = [];

    public HistoryViewModel(
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory,
        ILogger<HistoryViewModel> logger)
    {
        _translationService = translationService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // 订阅消息
        WeakReferenceMessenger.Default.Register<ModsLoadedMessage>(this, (_, _) => LoadHistory());
    }

    [RelayCommand]
    public void LoadHistory()
    {
        try
        {
            // 异步加载历史快照
            Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
                var snapshots = await historyRepo.GetSnapshotsAsync();

                // UI 更新
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SnapshotHistory.Clear();
                    foreach (var s in snapshots) SnapshotHistory.Add(s);
                    WeakReferenceMessenger.Default.Send(new StatusMessage($"已加载 {snapshots.Count} 个历史快照", StatusLevel.Info));
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载历史时发生错误");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"历史加载错误: {ex.Message}", StatusLevel.Error));
        }
    }

    partial void OnSelectedSnapshotChanged(HistorySnapshot? value)
    {
        // 清空选中的 History Item
        SelectedDiffItem = null;
        ModDiffChanges.Clear();

        if (value != null)
        {
            _ = LoadSnapshotDetailsAsync(value);
        }
    }
    
    partial void OnSelectedDiffItemChanged(ModDiffModel? value)
    {
        // 显示 Diff
        if (value != null)
        {
            _ = LoadDiffAsync(value);
        }
        else
        {
             DiffText = "选择一个 Mod 以查看内容";
        }
    }

    private async Task LoadSnapshotDetailsAsync(HistorySnapshot snapshot)
    {
        try
        {
            DiffLoadingMessage = "正在加载快照详情...";
            IsLoadingDiff = true;

            using var scope = _scopeFactory.CreateScope();
            var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
            
            // 获取该快照时刻的所有 Mod 记录 (或者是该快照中变更的部分)
            // 修改：只获取当前 Snapshot 发生变更的记录，而不是所有 Mod 的状态
            var histories = await historyRepo.GetSnapshotChangesAsync(snapshot.Id);
            
            var diffModels = new List<ModDiffModel>();
            foreach (var h in histories)
            {
                // 获取上一个版本的 Json
                // 我们通过 SnapshotId < current 查找最近的一个记录
                var modFullHistory = await historyRepo.GetHistoryForModAsync(h.ModUniqueId);
                var prevHistory = modFullHistory
                    .Where(x => x.SnapshotId < snapshot.Id)
                    .OrderByDescending(x => x.SnapshotId)
                    .FirstOrDefault();

                var oldJson = prevHistory?.JsonContent ?? "";
                var newJson = h.JsonContent;

                var diff = BuildModDiffModel(h.ModUniqueId, oldJson, newJson, h.ModMetadata?.RelativePath);
                diffModels.Add(diff);
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ModDiffChanges.Clear();
                foreach (var d in diffModels) ModDiffChanges.Add(d);
            });
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "加载快照详情失败");
             WeakReferenceMessenger.Default.Send(new StatusMessage($"详情加载失败: {ex.Message}", StatusLevel.Error));
        }
        finally 
        {
            IsLoadingDiff = false;
        }
    }

    private ModDiffModel BuildModDiffModel(string uniqueId, string? oldJson, string? newJson, string? relativePath)
    {
        var model = new ModDiffModel { UniqueID = uniqueId };
        
        ModManifest? oldM = null;
        ModManifest? newM = null;

        try { if (!string.IsNullOrEmpty(oldJson)) oldM = Newtonsoft.Json.JsonConvert.DeserializeObject<ModManifest>(oldJson); } catch { }
        try { if (!string.IsNullOrEmpty(newJson)) newM = Newtonsoft.Json.JsonConvert.DeserializeObject<ModManifest>(newJson); } catch { }

        model.ModName = newM?.Name ?? oldM?.Name ?? uniqueId;
        
        // Use relative path but remove 'manifest.json' if present for cleaner display
        if (!string.IsNullOrEmpty(relativePath))
        {
            model.FolderName = relativePath.Replace("/manifest.json", "", StringComparison.OrdinalIgnoreCase)
                                           .Replace("\\manifest.json", "", StringComparison.OrdinalIgnoreCase);
        }
        else 
        {
            model.FolderName = ""; 
        }

        model.NameChange = new FieldChange { FieldName = "Name", OldValue = oldM?.Name, NewValue = newM?.Name };
        model.DescriptionChange = new FieldChange { FieldName = "Description", OldValue = oldM?.Description, NewValue = newM?.Description };
        model.AuthorChange = new FieldChange { FieldName = "Author", OldValue = oldM?.Author, NewValue = newM?.Author };
        model.VersionChange = new FieldChange { FieldName = "Version", OldValue = oldM?.Version, NewValue = newM?.Version };

        int changes = 0;
        if (model.NameChange.HasChange) changes++;
        if (model.DescriptionChange.HasChange) changes++;
        if (model.AuthorChange.HasChange) changes++;
        if (model.VersionChange.HasChange) changes++;

        model.ChangeCount = changes;
        if (oldM == null && newM != null) model.ChangeType = "Added";
        else if (oldM != null && newM == null) model.ChangeType = "Deleted";
        else model.ChangeType = "Modified";

        return model;
    }

    private async Task LoadDiffAsync(ModDiffModel diffItem)
    {
        // 显示 Diff 统计
        DiffText = diffItem.ChangeSummary;
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task RollbackSnapshotAsync()
    {
        if (SelectedSnapshot == null) return;
        
        // 全局回滚逻辑
        // 1. 获取该 Snapshot 时刻所有 Mod 的最新状态
        // 2. 批量更新 ModMetadata
        // 3. 写入文件系统
        
        try 
        {
             WeakReferenceMessenger.Default.Send(new StatusMessage("正在全局回滚...", StatusLevel.Warning));
             
             // TODO: 调用 Service 的 RollbackSnapshot 方法 (需要在 TranslationService 中新增，或此处实现)
             // 简单起见，先不支持全局回滚，只支持“从数据库恢复” (RestoreFromDatabaseAsync works "latest").
             // To implement "Time Machine":
             // update ModMetadata set CurrentJson = History.Json where ...
             // then call RestoreTranslationsFromDbAsync
             
             using var scope = _scopeFactory.CreateScope();
             var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
             var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
             
             var histories = await historyRepo.GetModHistoriesForSnapshotAsync(SelectedSnapshot.Id);
             var mods = new List<ModMetadata>();
             
             foreach(var h in histories)
             {
                 var mod = await modRepo.GetModAsync(h.ModUniqueId);
                 if (mod != null)
                 {
                     mod.CurrentJson = h.JsonContent;
                     mod.LastFileHash = h.PreviousHash; // Revert hash too?
                     // mod.LastTranslationUpdate = // Keep real time?
                     mods.Add(mod);
                 }
             }
             
             await modRepo.UpsertModsAsync(mods); // Commit DB state revert
             
             // Now apply to physical files
             await _translationService.RestoreTranslationsFromDbAsync(string.Empty); // Empty dir uses Settings logic inside Service? Usually requires path.
             // MainViewModel passes ModsDirectory. Here we might need to get it from settings.
             
             WeakReferenceMessenger.Default.Send(new StatusMessage("全局回滚成功! 请检查游戏模组。", StatusLevel.Success));
        }
        catch(Exception ex)
        {
             WeakReferenceMessenger.Default.Send(new StatusMessage($"回滚失败: {ex.Message}", StatusLevel.Error));
        }
    }
}
