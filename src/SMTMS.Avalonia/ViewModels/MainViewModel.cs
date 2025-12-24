using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.Avalonia.Messages;
using SMTMS.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SMTMS.Data.Context;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace SMTMS.Avalonia.ViewModels;

/// <summary>
/// 主视图模型（Shell），负责全局导航和状态管理
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IGamePathService _gamePathService;
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IFolderPickerService _folderPickerService;
    private int _isSyncing;

    [ObservableProperty]
    private string _applicationTitle = "SMTMS - Stardew Mod Translation & Management System";

    [ObservableProperty]
    private string _modsDirectory = string.Empty; // 将在初始化时从 IGamePathService 获取

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private StatusLevel _statusLevel = StatusLevel.Info;

    // 子 ViewModels
    public ModListViewModel ModListViewModel { get; }
    public HistoryViewModel HistoryViewModel { get; }

    public MainViewModel(
        ModListViewModel modListViewModel,
        HistoryViewModel historyViewModel,
        IGamePathService gamePathService,
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory,
        ILogger<MainViewModel> logger,
        IFolderPickerService folderPickerService)
    {
        ModListViewModel = modListViewModel;
        HistoryViewModel = historyViewModel;
        _gamePathService = gamePathService;
        _translationService = translationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _folderPickerService = folderPickerService;

        _logger.LogInformation("MainViewModel 初始化");

        // 订阅状态消息
        WeakReferenceMessenger.Default.Register<StatusMessage>(this, OnStatusMessageReceived);

        // 初始化
        _ = InitializeAsync();
    }

    private void OnStatusMessageReceived(object recipient, StatusMessage message)
    {
        StatusMessage = message.Value; // Message string is in .Value
        StatusLevel = message.Level;
    }

    private async Task InitializeAsync()
    {
        try
        {
            // 1. 加载设置
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();

            // 2. 设置 Mods 目录
            if (!string.IsNullOrEmpty(settings.LastModsDirectory) && Directory.Exists(settings.LastModsDirectory))
            {
                ModsDirectory = settings.LastModsDirectory;
            }
            else
            {
                var detectedPath = _gamePathService.GetModsPath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    ModsDirectory = detectedPath;
                }
            }

            StatusMessage = "初始化完成";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化失败");
            StatusMessage = $"初始化错误: {ex.Message}";
            StatusLevel = StatusLevel.Error;
        }
    }

    partial void OnModsDirectoryChanged(string value)
    {
        // 通知所有订阅者目录已变更
        WeakReferenceMessenger.Default.Send(new ModsDirectoryChangedMessage(value));
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var selectedPath = await _folderPickerService.PickFolderAsync();
        
        if (!string.IsNullOrEmpty(selectedPath))
        {
            ModsDirectory = selectedPath;

            // 保存到数据库
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settingsService.UpdateLastModsDirectoryAsync(ModsDirectory);

            WeakReferenceMessenger.Default.Send(new StatusMessage($"已设置Mods目录: {ModsDirectory}", StatusLevel.Success));
        }
    }

    [RelayCommand]
    private async Task HardResetAsync()
    {
        try
        {
            // 释放数据库锁
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");

            // 2. 删除数据库文件
            var dbPath = Path.Combine(appDataPath, "smtms.db");
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            // 3. 重新创建数据库表
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Database.MigrateAsync();
            }

            WeakReferenceMessenger.Default.Send(new StatusMessage("初始化完成。所有历史和数据已清空。", StatusLevel.Success));

            // 4. 请求刷新
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);
            HistoryViewModel.LoadHistory();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化失败");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"初始化错误: {ex.Message}", StatusLevel.Error));
        }
    }

    [RelayCommand]
    private async Task SyncToDatabaseAsync()
    {
        // 防止重入（并发保护）
        if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) == 1)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage("正在后台同步中，请稍候...", StatusLevel.Warning));
            return;
        }

        try
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage("正在扫描并更新数据库..."));

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. 同步到数据库
            var saveResult = await Task.Run(async () => await _translationService.SaveTranslationsToDbAsync(ModsDirectory));
            
            sw.Stop();

            if (!saveResult.IsSuccess)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"保存失败: {saveResult.Message}", StatusLevel.Error));
                _logger.LogError("保存翻译失败: {Message}", saveResult.Message);
                return;
            }

            // 2. 完成
            WeakReferenceMessenger.Default.Send(new StatusMessage($"同步完成 ({sw.ElapsedMilliseconds}ms)。", StatusLevel.Success));
            
            // 3. 刷新历史和模组列表
            HistoryViewModel.LoadHistory();
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步过程发生异常");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"同步错误: {ex.Message}", StatusLevel.Error));
        }
        finally
        {
            Interlocked.Exchange(ref _isSyncing, 0);
        }
    }

    [RelayCommand]
    private async Task RestoreFromDatabaseAsync()
    {
        WeakReferenceMessenger.Default.Send(new StatusMessage("正在从数据库恢复..."));

        try
        {
            var result = await _translationService.RestoreTranslationsFromDbAsync(ModsDirectory);

            if (result.IsSuccess)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"恢复成功: {result.Message}", StatusLevel.Success));
                _logger.LogInformation("恢复翻译成功: {Message}", result.Message);
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"恢复失败: {result.Message}", StatusLevel.Warning));
                _logger.LogWarning("恢复翻译失败: {Message}", result.Message);
            }

            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复过程发生异常");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"恢复错误: {ex.Message}", StatusLevel.Error));
        }
    }
}
