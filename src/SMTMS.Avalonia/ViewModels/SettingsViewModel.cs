using Avalonia;
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
using System;

namespace SMTMS.Avalonia.ViewModels;

/// <summary>
/// 设置视图模型
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IFolderPickerService _folderPickerService;

    [ObservableProperty]
    private string _modsDirectory = string.Empty;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private string _translationApiType = "Google";

    [ObservableProperty]
    private string _translationSourceLang = "auto";

    [ObservableProperty]
    private string _translationTargetLang = "zh-CN";

    public SettingsViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<SettingsViewModel> logger,
        IFolderPickerService folderPickerService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _folderPickerService = folderPickerService;

        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();

            ModsDirectory = settings.LastModsDirectory ?? string.Empty;
            IsDarkMode = settings.IsDarkMode;
            TranslationApiType = settings.TranslationApiType;
            TranslationSourceLang = settings.TranslationSourceLang;
            TranslationTargetLang = settings.TranslationTargetLang;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载设置失败");
        }
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        // 立即应用主题
        if (Application.Current is App app)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => app.SetTheme(value));
        }

        // 保存到数据库
        _ = SaveDarkModeSettingAsync(value);
    }

    private async Task SaveDarkModeSettingAsync(bool isDarkMode)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();
            settings.IsDarkMode = isDarkMode;
            await settingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存黑夜模式设置失败");
        }
    }

    partial void OnModsDirectoryChanged(string value)
    {
        // 通知其他组件目录已变更
        WeakReferenceMessenger.Default.Send(new ModsDirectoryChangedMessage(value));

        // 保存到数据库
        _ = SaveModsDirectoryAsync(value);
    }

    private async Task SaveModsDirectoryAsync(string directory)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settingsService.UpdateLastModsDirectoryAsync(directory);
            
            WeakReferenceMessenger.Default.Send(new StatusMessage($"已设置Mods目录: {directory}", StatusLevel.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存Mods目录失败");
        }
    }

    partial void OnTranslationApiTypeChanged(string value)
    {
        _ = SaveTranslationSettingsAsync();
    }

    partial void OnTranslationSourceLangChanged(string value)
    {
        _ = SaveTranslationSettingsAsync();
    }

    partial void OnTranslationTargetLangChanged(string value)
    {
        _ = SaveTranslationSettingsAsync();
    }

    private async Task SaveTranslationSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.GetSettingsAsync();
            settings.TranslationApiType = TranslationApiType;
            settings.TranslationSourceLang = TranslationSourceLang;
            settings.TranslationTargetLang = TranslationTargetLang;
            await settingsService.SaveSettingsAsync(settings);
            
            WeakReferenceMessenger.Default.Send(new StatusMessage("翻译配置已保存", StatusLevel.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存翻译配置失败");
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var selectedPath = await _folderPickerService.PickFolderAsync();
        
        if (!string.IsNullOrEmpty(selectedPath))
        {
            ModsDirectory = selectedPath;
        }
    }

    [RelayCommand]
    private async Task HardResetAsync()
    {
        try
        {
            // 释放数据库锁并等待系统回收
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            for (int i = 0; i < 2; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100); 
            }

            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SMTMS");

            // 删除数据库文件
            var dbPath = Path.Combine(appDataPath, "smtms.db");
            if (File.Exists(dbPath))
            {
                // 重试机制,防止文件短暂占用
                int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        File.Delete(dbPath);
                        break; 
                    }
                    catch (IOException)
                    {
                        if (i == maxRetries - 1) throw; 
                        await Task.Delay(500); // 异步等待
                    }
                }
            }

            // 重新创建数据库表
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Database.MigrateAsync();
            }

            WeakReferenceMessenger.Default.Send(new StatusMessage("初始化完成。所有历史和数据已清空。", StatusLevel.Success));

            // 请求刷新
            WeakReferenceMessenger.Default.Send(RefreshModsRequestMessage.Instance);
            
            // 重新加载设置
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化失败");
            WeakReferenceMessenger.Default.Send(new StatusMessage($"初始化错误: {ex.Message}", StatusLevel.Error));
        }
    }
}
