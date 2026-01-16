using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Collections;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.Avalonia.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SMTMS.Avalonia.ViewModels;

/// <summary>
/// 模组列表视图模型，负责模组的加载、显示和单项操作
/// </summary>
public partial class ModListViewModel : ObservableObject
{
    private readonly IModService _modService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModListViewModel> _logger;

    [ObservableProperty]
    private ModViewModel? _selectedMod;

    [ObservableProperty]
    private string _modsDirectory = string.Empty;

    public AvaloniaList<ModViewModel> Mods { get; } = [];

    // Column Visibility
    [ObservableProperty] private bool _showNameColumn = true;
    [ObservableProperty] private bool _showVersionColumn = true;
    [ObservableProperty] private bool _showIdColumn = true;
    [ObservableProperty] private bool _showNexusIdColumn = true;
    [ObservableProperty] private bool _showDescriptionColumn = true;

    // 保存前请求更新绑定的事件
    public event EventHandler? SaveRequested;

    public ModListViewModel(
        IModService modService,
        IServiceScopeFactory scopeFactory,
        ILogger<ModListViewModel> logger)
    {
        _modService = modService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // 订阅消息
        WeakReferenceMessenger.Default.Register<ModsDirectoryChangedMessage>(this, OnModsDirectoryChanged);
        WeakReferenceMessenger.Default.Register<RefreshModsRequestMessage>(this, OnRefreshModsRequest);
    }

    private void OnModsDirectoryChanged(object recipient, ModsDirectoryChangedMessage message)
    {
        ModsDirectory = message.NewDirectory;
        _ = LoadModsAsync();
    }

    private void OnRefreshModsRequest(object recipient, RefreshModsRequestMessage message)
    {
        _ = LoadModsAsync();
    }

    private CancellationTokenSource? _loadCts;

    [RelayCommand]
    public async Task LoadModsAsync()
    {
        // 1. 取消先前的加载任务
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        if (string.IsNullOrEmpty(ModsDirectory))
        {
            _logger.LogWarning("Mods 目录为空，无法加载模组");
            return;
        }

        WeakReferenceMessenger.Default.Send(new StatusMessage("正在扫描模组..."));
        // 注意：不再此处 Clear，避免闪烁和竞态条件下的空白

        try
        {
            // 2. 扫描文件 (异步，可能耗时)
            var manifests = await Task.Run(async () => await _modService.ScanModsAsync(ModsDirectory), token);
            var manifestList = manifests.ToList();

            if (token.IsCancellationRequested) return;

            if (manifestList.Count == 0)
            {
                Mods.Clear(); // 确实没有模组时才清空
                WeakReferenceMessenger.Default.Send(new StatusMessage("未找到任何模组", StatusLevel.Warning));
                return;
            }

            // 3. 批量查询数据库
            using var scope = _scopeFactory.CreateScope();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            var uniqueIds = manifestList.Select(m => m.UniqueID).ToList();
            var dbMods = await modRepo.GetModsByIdsAsync(uniqueIds);

            if (token.IsCancellationRequested) return;

            // 4. 合并数据并更新
            var modsToUpdate = new List<Core.Models.ModMetadata>();
            var viewModels = new List<ModViewModel>(manifestList.Count);

            foreach (var manifest in manifestList)
            {
                dbMods.TryGetValue(manifest.UniqueID, out var mod);
                bool isNew = false; // 标记是否为新发现的模组

                if (mod == null)
                {
                    isNew = true;
                    // 新模组 - 创建元数据记录
                    mod = new Core.Models.ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        OriginalName = manifest.Name,
                        OriginalDescription = manifest.Description,
                        RelativePath = string.IsNullOrEmpty(manifest.ManifestPath) ? string.Empty : Path.GetRelativePath(ModsDirectory, manifest.ManifestPath).Replace('\\', '/'),
                        LastTranslationUpdate = DateTime.UtcNow
                    };
                    modsToUpdate.Add(mod);
                }
                else
                {
                    // 更新路径（如果需要）
                    var currentRelativePath = string.IsNullOrEmpty(manifest.ManifestPath) ? string.Empty : Path.GetRelativePath(ModsDirectory, manifest.ManifestPath).Replace('\\', '/');

                    if (mod.RelativePath != currentRelativePath)
                    {
                        mod.RelativePath = currentRelativePath;
                        modsToUpdate.Add(mod);
                    }
                }

                // 创建 ViewModel
                var viewModel = new ModViewModel(manifest, mod, isNew);
                viewModels.Add(viewModel);
            }

            if (token.IsCancellationRequested) return;

            // 5. 最终 UI 更新 (在主线程)
            // 只有在这里才清空并添加，确保是原子操作般的视觉效果
            Mods.Clear();
            // Sort by Name explicitly
            Mods.AddRange(viewModels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));

            // 批量保存所有变更 (后台操作，不需要阻塞 UI)
            if (modsToUpdate.Count != 0)
            {
                _ = modRepo.UpsertModsAsync(modsToUpdate);
            }

            WeakReferenceMessenger.Default.Send(new StatusMessage($"已加载 {Mods.Count} 个模组", StatusLevel.Success));
            WeakReferenceMessenger.Default.Send(new ModsLoadedMessage(Mods.ToList(), ModsDirectory));
        }
        catch (OperationCanceledException)
        {
            // 忽略取消异常
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载模组时发生错误");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"加载模组失败: {ex.Message}", StatusLevel.Error));
        }
    }

    [RelayCommand]
    public async Task SaveModAsync()
    {
        if (SelectedMod == null)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage("保存错误: 未选择模组", StatusLevel.Error));
            return;
        }

        try
        {
            // 触发保存前事件，强制更新绑定
            SaveRequested?.Invoke(this, EventArgs.Empty);

            // 等待一小段时间确保绑定更新完成
            await Task.Delay(50);

            var manifestPath = SelectedMod.ManifestPath;
            if (string.IsNullOrEmpty(manifestPath))
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"文件路径无效", StatusLevel.Error));
                return;
            }

            // 使用 Service 层更新文件，不再直接操作文件系统
            await _modService.UpdateModManifestAsync(manifestPath, SelectedMod.Name, SelectedMod.Description);

            // 重置 IsDirty 状态
            SelectedMod.ResetDirtyState();
            SelectedMod.UpdateStatus();

            WeakReferenceMessenger.Default.Send(new StatusMessage(
                $"已保存 '{SelectedMod.Name}' (本地)。请点击 '同步到数据库' 以创建版本。",
                StatusLevel.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存模组时发生错误");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"保存错误: {ex.Message}", StatusLevel.Error));
        }
    }
}
