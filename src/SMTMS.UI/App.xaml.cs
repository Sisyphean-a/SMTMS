using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Services;
using SMTMS.Data.Context;
using SMTMS.UI.ViewModels;

namespace SMTMS.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IHost? _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options => 
                    options.UseSqlite("Data Source=smtms.db"));
                
                services.AddSingleton<IGitService, SMTMS.GitProvider.Services.GitService>();
                services.AddSingleton<IModService, ModService>();
                services.AddSingleton<IGamePathService, RegistryGamePathService>();
                services.AddSingleton<ITranslationService, TranslationService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host!.StartAsync();
        
        // Initialize ServiceLocator
        SMTMS.Core.Infrastructure.ServiceLocator.Initialize(_host.Services);

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

