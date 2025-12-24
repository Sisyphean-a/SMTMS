using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SMTMS.UI.ViewModels;

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

    public ObservableCollection<ModViewModel> Mods { get; } = [];

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

    [RelayCommand]
    public async Task LoadModsAsync()
    {
        if (string.IsNullOrEmpty(ModsDirectory))
        {
            _logger.LogWarning("Mods 目录为空，无法加载模组");
            return;
        }

        WeakReferenceMessenger.Default.Send(new StatusMessage("正在扫描模组..."));
        Mods.Clear();

        try
        {
            // 1. 扫描文件
            var manifests = await _modService.ScanModsAsync(ModsDirectory);
            var manifestList = manifests.ToList();

            if (manifestList.Count == 0)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage("未找到任何模组", StatusLevel.Warning));
                return;
            }

            // 2. 批量查询数据库
            using var scope = _scopeFactory.CreateScope();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            var uniqueIds = manifestList.Select(m => m.UniqueID).ToList();
            var dbMods = await modRepo.GetModsByIdsAsync(uniqueIds);

            // 3. 合并数据并更新
            var modsToUpdate = new List<Core.Models.ModMetadata>();

            foreach (var manifest in manifestList)
            {
                dbMods.TryGetValue(manifest.UniqueID, out var mod);

                if (mod == null)
                {
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

                // 添加到 UI 集合
                var viewModel = new ModViewModel(manifest, mod);
                Mods.Add(viewModel);
            }

            // 批量保存所有变更
            if (modsToUpdate.Count != 0)
            {
                await modRepo.UpsertModsAsync(modsToUpdate);
            }

            WeakReferenceMessenger.Default.Send(new StatusMessage($"已加载 {Mods.Count} 个模组", StatusLevel.Success));
            WeakReferenceMessenger.Default.Send(new ModsLoadedMessage(Mods.ToList(), ModsDirectory));
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
