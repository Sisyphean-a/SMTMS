using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Services;
using SMTMS.Data.Context;
using SMTMS.UI.ViewModels;

namespace SMTMS.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static IHost? _host;

    public App()
    {
        // 修复控制台日志中文乱码
        try
        {
            var writer = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true
            };
            Console.SetOut(writer);
        }
        catch (Exception ex)
        {
            // 仅在输出窗口打印，不会崩溃
            System.Diagnostics.Debug.WriteLine($"无法重定向控制台输出: {ex.Message}");
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var smtmsPath = Path.Combine(appDataPath, "SMTMS");
                if (!Directory.Exists(smtmsPath))
                {
                    Directory.CreateDirectory(smtmsPath);
                }
                var dbPath = Path.Combine(smtmsPath, "smtms.db");

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={dbPath}");
                    // 仅在开发环境或需要调试 SQL 时开启，避免控制台刷屏
                    // options.EnableSensitiveDataLogging(); 
                    
                    // 忽略 SQL 执行日志，防止控制台刷屏
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted));
                });

                // Infrastructure
                services.AddSingleton<IFileSystem, PhysicalFileSystem>();

                services.AddSingleton<IGitService, GitProvider.Services.GitService>();
                services.AddSingleton<IModService, ModService>();
                services.AddSingleton<IGamePathService, RegistryGamePathService>();
                services.AddScoped<IModRepository, Data.Repositories.ModRepository>(); // Scoped for EF

                // Translation Services
                services.AddSingleton<Translation.Services.LegacyImportService>();
                services.AddSingleton<Translation.Services.TranslationScanService>();
                services.AddSingleton<Translation.Services.TranslationRestoreService>();
                services.AddSingleton<Translation.Services.GitTranslationService>();
                services.AddSingleton<ITranslationService, Translation.Services.TranslationService>();

                services.AddScoped<ISettingsService, Data.Services.SettingsService>();
                services.AddScoped<IGitDiffCacheService, Data.Services.GitDiffCacheService>(); // Scoped for EF

                // ViewModels - 注册子 ViewModels 为 Singleton
                services.AddSingleton<ModListViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host!.StartAsync();

        // Apply database migrations
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // MigrateAsync will apply all pending migrations
            try
            {
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                // 如果是 "table already exists" 错误，说明数据库可能通过 EnsureCreated 创建，
                // 导致缺少 __EFMigrationsHistory 表。这里忽略异常以允许程序启动。
                // 真正的修复是使用 HardReset（已在 MainViewModel 中修复）。
                if (!ex.Message.Contains("already exists") && !ex.ToString().Contains("already exists"))
                {
                    throw; // 其他严重错误仍然抛出
                }
            }
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host!.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}

