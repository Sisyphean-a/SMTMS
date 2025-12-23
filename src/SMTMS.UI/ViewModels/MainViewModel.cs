using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

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
    private int _isSyncing = 0;

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

            // 4. (已通过属性变更通知 OnModsDirectoryChanged 自动发送消息，此处无需重复发送)
            
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
                await context.Database.MigrateAsync();
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
        // 防止重入（并发保护）
        if (Interlocked.CompareExchange(ref _isSyncing, 1, 0) == 1)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage("正在后台同步中，请稍候...", StatusLevel.Warning));
            return;
        }

        try
        {
            var dialog = new Views.CommitDialog($"Scan & Update {DateTime.Now:yyyy/MM/dd HH:mm}");
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var commitMessage = dialog.CommitMessage;
            WeakReferenceMessenger.Default.Send(new StatusMessage("正在扫描并更新数据库..."));

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. 同步到数据库 (快速操作 - 前台等待)
            var saveResult = await _translationService.SaveTranslationsToDbAsync(ModsDirectory);
            if (!saveResult.IsSuccess)
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage($"保存失败: {saveResult.Message}", StatusLevel.Error));
                _logger.LogError("保存翻译失败: {Message}", saveResult.Message);
                return;
            }

            sw.Stop();
            var dbTime = sw.ElapsedMilliseconds;

            // UI 立即响应：数据库已完成
            WeakReferenceMessenger.Default.Send(new StatusMessage($"数据库更新完成 ({dbTime}ms)。正在后台备份到 Git...", StatusLevel.Info));
            
            // 刷新历史和模组列表 (让用户立刻看到变动)
            HistoryViewModel.LoadHistory();
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);

            // 2. Git 备份 (耗时操作 - 后台异步)
            _ = Task.Run(async () =>
            {
                try
                {
                   var swGit = System.Diagnostics.Stopwatch.StartNew();
                   var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");
                   
                   // 2.1 导出文件
                   var exportResult = await _translationService.ExportTranslationsToGitRepo(ModsDirectory, appDataPath);
                   if (!exportResult.IsSuccess)
                   {
                       WeakReferenceMessenger.Default.Send(new StatusMessage($"Git 导出失败: {exportResult.Message}", StatusLevel.Error));
                       return;
                   }

                   // 2.2 提交 (CPU/IO 密集型)
                   // _gitService.CommitAll 是同步方法，但在 Task.Run 中运行不会卡 UI
                   _gitService.CommitAll(appDataPath, commitMessage);
                   
                   swGit.Stop();
                   _logger.LogInformation("✅ [Git Background Sync] {Elapsed}ms.", swGit.ElapsedMilliseconds);
                   
                   // 完成通知
                   WeakReferenceMessenger.Default.Send(new StatusMessage($"同步全部完成 (DB: {dbTime}ms, Git: {swGit.ElapsedMilliseconds}ms)", StatusLevel.Success));
                   
                   // 再次刷新历史以显示新提交
                   // HistoryViewModel.LoadHistory(); // 需要切回主线程，简单起见这里省略或通过消息通知，StatusMessage 已足够
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "后台 Git 提交失败");
                     WeakReferenceMessenger.Default.Send(new StatusMessage($"后台备份失败: {ex.Message}", StatusLevel.Error));
                }
                finally
                {
                    Interlocked.Exchange(ref _isSyncing, 0);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步过程发生异常");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"同步错误: {ex.Message}", StatusLevel.Error));
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
