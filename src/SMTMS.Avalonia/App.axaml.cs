using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Services;
using SMTMS.Data.Context;
using SMTMS.Data.Repositories;
using SMTMS.Translation.Services;
using SMTMS.Avalonia.ViewModels;
using SMTMS.Avalonia.Services;
using SMTMS.Avalonia.Views;
using System;
using System.IO;

namespace SMTMS.Avalonia;

public partial class App : Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // 创建 Host 和 服务容器
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // App Data Path
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var smtmsPath = Path.Combine(appDataPath, "SMTMS");
                if (!Directory.Exists(smtmsPath))
                {
                    Directory.CreateDirectory(smtmsPath);
                }
                var dbPath = Path.Combine(smtmsPath, "smtms.db");

                // Database
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={dbPath};Pooling=False");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted));
                });

                // Infrastructure
                services.AddSingleton<IFileSystem, PhysicalFileSystem>();
                services.AddSingleton<IModService, ModService>();
                
                // Game Path Detection (OS Specific)
                if (OperatingSystem.IsWindows())
                {
                    services.AddSingleton<IGamePathService, RegistryGamePathService>();
                }
                else 
                {
                    // Fallback for Mac/Linux
                    services.AddSingleton<IGamePathService, ManualGamePathService>(); 
                }
                
                // Repositories
                services.AddScoped<IModRepository, ModRepository>();
                services.AddScoped<IHistoryRepository, HistoryRepository>();

                // Domain Services
                services.AddSingleton<IDiffService, DiffService>();
                services.AddSingleton<LegacyImportService>();
                services.AddSingleton<TranslationScanService>();
                services.AddSingleton<TranslationRestoreService>();
                services.AddSingleton<ITranslationService, TranslationService>();
                services.AddScoped<ISettingsService, Data.Services.SettingsService>();
                
                // Translation API Service
                services.AddSingleton<ITranslationApiService, GoogleTranslationService>();

                // UI Core Services
                services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
                services.AddSingleton<ICommitMessageService, AvaloniaCommitMessageService>();

                // ViewModels
                services.AddSingleton<ModListViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();
                
                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // Configure Global IOC (Optional, helpful for View CodeBehind accessing services if needed)
        CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.ConfigureServices(_host.Services);

        // Apply Migrations
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try 
            {
                await dbContext.Database.MigrateAsync();
            }
            catch { /* Ignore if exists/migrated */ }

            // Load theme setting from database
            try
            {
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                SetTheme(settings.IsDarkMode);
            }
            catch { /* Ignore if failed to load theme */ }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            
            desktop.Exit += async (sender, args) => 
            {
                await _host.StopAsync();
                _host.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 设置应用程序主题
    /// </summary>
    /// <param name="isDarkMode">是否启用黑夜模式</param>
    public void SetTheme(bool isDarkMode)
    {
        // 设置全局主题变体
        RequestedThemeVariant = isDarkMode
            ? global::Avalonia.Styling.ThemeVariant.Dark
            : global::Avalonia.Styling.ThemeVariant.Light;

        // 为所有窗口添加/移除 theme-dark 类
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                if (isDarkMode)
                {
                    if (!window.Classes.Contains("theme-dark"))
                        window.Classes.Add("theme-dark");
                }
                else
                {
                    window.Classes.Remove("theme-dark");
                }
            }
        }
    }
}
