using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.UI.Messages;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

namespace SMTMS.UI.ViewModels;

public partial class ModHistoryViewModel : ObservableObject
{
    private readonly string _modUniqueId;
    private readonly IServiceScopeFactory _scopeFactory;
    
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ModHistoryItemViewModel? _selectedHistoryItem;

    public ObservableCollection<ModHistoryItemViewModel> HistoryItems { get; } = [];
    
    // 点击“应用”时调用的 Action
    // 可以传回选中的 Manifest 数据（名称、描述等）
    public event Action<ModManifest>? OnApplyHistory;
    public event Action? OnCloseRequest;

    public ModHistoryViewModel(string modUniqueId, IServiceScopeFactory scopeFactory)
    {
        _modUniqueId = modUniqueId;
        _scopeFactory = scopeFactory;
        
        _ = LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
            
            // 获取该 Mod 的所有历史记录
            var histories = await historyRepo.GetHistoryForModAsync(_modUniqueId);
            
            // 按时间正序排序以便计算差异
            var sortedHistories = histories.OrderBy(h => h.SnapshotId).ToList();
            
            var viewModels = new List<ModHistoryItemViewModel>();
            
            for (int i = 0; i < sortedHistories.Count; i++)
            {
                var current = sortedHistories[i];
                var prev = i > 0 ? sortedHistories[i - 1] : null;
                
                viewModels.Add(new ModHistoryItemViewModel(current, prev));
            }
            
            // UI 通常把最新的显示在最上面
            viewModels.Reverse();
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                HistoryItems.Clear();
                foreach (var vm in viewModels) HistoryItems.Add(vm);
            });
        }
        catch (Exception ex)
        {
             WeakReferenceMessenger.Default.Send(new StatusMessage($"无法加载历史记录: {ex.Message}", StatusLevel.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Apply()
    {
        if (SelectedHistoryItem?.Manifest == null) return;
        
        OnApplyHistory?.Invoke(SelectedHistoryItem.Manifest);
        OnCloseRequest?.Invoke();
    }
    
    [RelayCommand]
    public void Close()
    {
        OnCloseRequest?.Invoke();
    }
}
