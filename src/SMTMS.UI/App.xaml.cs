using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Services;
using SMTMS.Data.Context;
using SMTMS.Data.Repositories;
using SMTMS.Translation.Services;
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
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted));
                });

                // Infrastructure
                services.AddSingleton<IFileSystem, PhysicalFileSystem>();
                // Removed GitService
                services.AddSingleton<IModService, ModService>();
                services.AddSingleton<IGamePathService, RegistryGamePathService>();
                
                // Repositories
                services.AddScoped<IModRepository, ModRepository>();
                services.AddScoped<IHistoryRepository, HistoryRepository>(); // New History Repo

                // Core Services
                services.AddSingleton<IDiffService, DiffService>(); // New Diff Service

                // Translation Services
                services.AddSingleton<LegacyImportService>();
                services.AddSingleton<TranslationScanService>();
                services.AddSingleton<TranslationRestoreService>();
                // Removed GitTranslationService
                services.AddSingleton<ITranslationService, TranslationService>();

                services.AddScoped<ISettingsService, Data.Services.SettingsService>();
                // Removed GitDiffCacheService

                // ViewModels
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
        
        // Configure CommunityToolkit IOC
        CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.ConfigureServices(_host.Services);

        // Apply database migrations
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("already exists") && !ex.ToString().Contains("already exists"))
                {
                    throw; 
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
