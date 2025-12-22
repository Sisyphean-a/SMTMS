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
                    options.UseSqlite($"Data Source={dbPath}"));

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
            await dbContext.Database.MigrateAsync();
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

