using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.UI.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SMTMS.UI.ViewModels;

/// <summary>
/// Git 历史视图模型，负责提交历史的展示和回滚操作
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly ITranslationService _translationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HistoryViewModel> _logger;
    private readonly string _appDataPath;

    [ObservableProperty]
    private GitCommitModel? _selectedCommit;

    [ObservableProperty]
    private ModDiffModel? _selectedDiffItem;

    [ObservableProperty]
    private string _diffText = "选择一个提交以查看变更";

    [ObservableProperty]
    private bool _isLoadingDiff;

    [ObservableProperty]
    private string _diffLoadingMessage = "";

    public ObservableCollection<GitCommitModel> CommitHistory { get; } = [];
    public ObservableCollection<ModDiffModel> ModDiffChanges { get; } = [];

    public HistoryViewModel(
        IGitService gitService,
        ITranslationService translationService,
        IServiceScopeFactory scopeFactory,
        ILogger<HistoryViewModel> logger)
    {
        _gitService = gitService;
        _translationService = translationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");

        // 订阅消息
        WeakReferenceMessenger.Default.Register<ModsLoadedMessage>(this, (_, _) => LoadHistory());
    }

    [RelayCommand]
    public void LoadHistory()
    {
        try
        {
            if (!_gitService.IsRepository(_appDataPath))
            {
                WeakReferenceMessenger.Default.Send(new StatusMessage("未找到 Git 仓库", StatusLevel.Warning));
                return;
            }

            var history = _gitService.GetHistory(_appDataPath);
            CommitHistory.Clear();
            foreach (var commit in history)
            {
                CommitHistory.Add(commit);
            }

            WeakReferenceMessenger.Default.Send(new HistoryLoadedMessage(CommitHistory.ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载历史时发生错误");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"历史加载错误: {ex.Message}", StatusLevel.Error));
        }
    }

    partial void OnSelectedCommitChanged(GitCommitModel? value)
    {
        // 清空选中的 Diff 项
        SelectedDiffItem = null;

        if (value != null)
        {
            // 异步加载 Diff，避免阻塞 UI
            _ = LoadDiffAsync(value);
        }
        else
        {
            DiffText = "选择一个提交以查看变更";
            ModDiffChanges.Clear();
        }
    }

    private async Task LoadDiffAsync(GitCommitModel commit)
    {
        IsLoadingDiff = true;
        DiffLoadingMessage = "正在加载变更...";
        ModDiffChanges.Clear();

        try
        {
            List<ModDiffModel> structuredDiff;

            // 使用 scope 访问 scoped 服务
            using (var scope = _scopeFactory.CreateScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<IGitDiffCacheService>();

                // 1. 先尝试从缓存读取
                DiffLoadingMessage = "正在检查缓存...";
                var cachedDiff = await cacheService.GetCachedDiffAsync(commit.FullHash);

                if (cachedDiff != null)
                {
                    // 缓存命中
                    DiffLoadingMessage = "从缓存加载...";
                    structuredDiff = cachedDiff;
                }
                else
                {
                    // 缓存未命中，计算 Diff
                    DiffLoadingMessage = "正在计算变更...";
                    structuredDiff = await Task.Run(() => _gitService.GetStructuredDiff(_appDataPath, commit.FullHash).ToList());

                    // 保存到缓存（异步，不阻塞 UI）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var saveScope = _scopeFactory.CreateScope();
                            var saveCacheService = saveScope.ServiceProvider.GetRequiredService<IGitDiffCacheService>();
                            await saveCacheService.SaveDiffCacheAsync(commit.FullHash, structuredDiff);
                        }
                        catch
                        {
                            // 忽略缓存保存失败
                        }
                    });
                }
            }

            // 回到 UI 线程更新集合
            DiffLoadingMessage = "正在更新界面...";
            foreach (var diff in structuredDiff)
            {
                ModDiffChanges.Add(diff);
            }

            DiffText = $"共 {ModDiffChanges.Count} 个模组发生变更";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 Diff 时发生错误");
            DiffText = $"加载变更失败: {ex.Message}";
            ModDiffChanges.Clear();
        }
        finally
        {
            IsLoadingDiff = false;
            DiffLoadingMessage = "";
        }
    }

    [RelayCommand]
    public async Task GitRollbackAsync()
    {
        if (SelectedCommit == null || string.IsNullOrEmpty(SelectedCommit.FullHash))
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage("回滚错误: 未选择有效的提交", StatusLevel.Error));
            return;
        }

        try
        {
            // 释放数据库锁
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await Task.Run(() => _gitService.Reset(_appDataPath, SelectedCommit.FullHash));

            // 同步数据库与回滚后的 Git 仓库状态
            await _translationService.ImportTranslationsFromGitRepoAsync(_appDataPath);

            // 自动同步：将恢复的数据库状态应用到游戏文件
            WeakReferenceMessenger.Default.Send(new StatusMessage(
                $"已回滚到 '{SelectedCommit.ShortHash}'，正在应用到文件..."));

            // 请求刷新模组列表
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);

            LoadHistory();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git 回滚时发生错误");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"回滚错误: {ex.Message}", StatusLevel.Error));
        }
    }
}

