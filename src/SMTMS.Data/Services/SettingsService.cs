using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Data.Context;

namespace SMTMS.Data.Services;

public class SettingsService(AppDbContext context) : ISettingsService
{
    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = await context.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings != null) return settings;
        settings = new AppSettings();
        context.AppSettings.Add(settings);
        await context.SaveChangesAsync();

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var existing = await context.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.LastModsDirectory = settings.LastModsDirectory;
            existing.WindowWidth = settings.WindowWidth;
            existing.WindowHeight = settings.WindowHeight;
            existing.AutoScanOnStartup = settings.AutoScanOnStartup;
            existing.IsDarkMode = settings.IsDarkMode;
        }
        else
        {
            context.AppSettings.Add(settings);
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateLastModsDirectoryAsync(string directory)
    {
        var settings = await GetSettingsAsync();
        settings.LastModsDirectory = directory;
        await SaveSettingsAsync(settings);
    }
}
