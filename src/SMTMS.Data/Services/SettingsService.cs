using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Data.Context;

namespace SMTMS.Data.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = await _context.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AppSettings();
            _context.AppSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var existing = await _context.AppSettings.FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.LastModsDirectory = settings.LastModsDirectory;
            existing.WindowWidth = settings.WindowWidth;
            existing.WindowHeight = settings.WindowHeight;
            existing.AutoScanOnStartup = settings.AutoScanOnStartup;
        }
        else
        {
            _context.AppSettings.Add(settings);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateLastModsDirectoryAsync(string directory)
    {
        var settings = await GetSettingsAsync();
        settings.LastModsDirectory = directory;
        await SaveSettingsAsync(settings);
    }
}
