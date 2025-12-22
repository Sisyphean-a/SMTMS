using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SMTMS.UI.ViewModels;

/// <summary>
/// 主视图模型（Shell），负责全局导航和状态管理
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly IGamePathService _gamePathService;
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MainViewModel> _logger;

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
        IGitService gitService,
        IGamePathService gamePathService,
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory,
        ILogger<MainViewModel> logger)
    {
        ModListViewModel = modListViewModel;
        HistoryViewModel = historyViewModel;
        _gitService = gitService;
        _gamePathService = gamePathService;
        _translationService = translationService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _logger.LogInformation("MainViewModel 初始化");

        // 订阅状态消息
        WeakReferenceMessenger.Default.Register<StatusMessage>(this, OnStatusMessageReceived);

        // 初始化
        _ = InitializeAsync();
    }

    private void OnStatusMessageReceived(object recipient, StatusMessage message)
    {
        StatusMessage = message.Message;
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

            // 3. 初始化 Git 仓库
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var smtmsPath = Path.Combine(appDataPath, "SMTMS");

            if (!Directory.Exists(smtmsPath))
            {
                Directory.CreateDirectory(smtmsPath);
            }

            if (!_gitService.IsRepository(smtmsPath))
            {
                _gitService.Init(smtmsPath);
            }

            // 4. 通知子 ViewModels 目录已设置
            WeakReferenceMessenger.Default.Send(new ModsDirectoryChangedMessage(ModsDirectory));

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
        var dialog = new FolderBrowserDialog
        {
            Description = "选择Stardew Valley的Mods目录",
            ShowNewFolderButton = false,
            SelectedPath = ModsDirectory
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ModsDirectory = dialog.SelectedPath;

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

            // 1. 删除 .git 文件夹
            await Task.Run(() => _gitService.DeleteRepository(appDataPath));

            // 2. 删除数据库文件
            var dbPath = Path.Combine(appDataPath, "smtms.db");
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            // 3. 重新创建数据库表
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<Data.Context.AppDbContext>();
                context.Database.EnsureCreated();
            }

            // 4. 重新初始化 Git
            _gitService.Init(appDataPath);

            WeakReferenceMessenger.Default.Send(new StatusMessage("初始化完成。所有历史和数据已清空。", StatusLevel.Success));

            // 5. 请求刷新
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
        var dialog = new Views.CommitDialog($"Scan & Update {DateTime.Now:yyyy/MM/dd HH:mm}");
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var commitMessage = dialog.CommitMessage;

        WeakReferenceMessenger.Default.Send(new StatusMessage("正在同步到数据库..."));

        try
        {
            // 1. 提取/更新数据库
            var saveResult = await _translationService.SaveTranslationsToDbAsync(ModsDirectory);
            if (!saveResult.IsSuccess)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"保存失败: {saveResult.Message}", StatusLevel.Error));
                _logger.LogError("保存翻译失败: {Message}", saveResult.Message);
                return;
            }

            // 2. 导出到 Git 仓库
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
            var exportResult = await _translationService.ExportTranslationsToGitRepo(ModsDirectory, appDataPath);
            if (!exportResult.IsSuccess)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"导出失败: {exportResult.Message}", StatusLevel.Error));
                _logger.LogError("导出翻译失败: {Message}", exportResult.Message);
                return;
            }

            // 3. 创建 Git 快照
            _gitService.CommitAll(appDataPath, commitMessage);

            WeakReferenceMessenger.Default.Send(new StatusMessage($"同步成功：{saveResult.Message}", StatusLevel.Success));
            _logger.LogInformation("同步成功: {Message}", saveResult.Message);

            // 刷新历史和模组列表
            HistoryViewModel.LoadHistory();
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步过程发生异常");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"同步错误: {ex.Message}", StatusLevel.Error));
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
