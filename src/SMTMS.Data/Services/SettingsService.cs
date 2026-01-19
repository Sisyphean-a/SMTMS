using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Core.Configuration;
using SMTMS.Data.Context;

namespace SMTMS.Data.Services;

public class SettingsService(AppDbContext context) : ISettingsService
{
    public async Task<AppSettings> GetSettingsAsync()
    {
        var settings = await context.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings != null)
        {
            // 确保翻译配置有默认值
            if (string.IsNullOrEmpty(settings.TranslationApiType))
                settings.TranslationApiType = Constants.Translation.ProviderGoogle;
            if (string.IsNullOrEmpty(settings.TranslationSourceLang))
                settings.TranslationSourceLang = Constants.Translation.DefaultSourceLang;
            if (string.IsNullOrEmpty(settings.TranslationTargetLang))
                settings.TranslationTargetLang = Constants.Translation.DefaultTargetLang;

            // 如果有空值，保存默认值到数据库
            if (string.IsNullOrEmpty(settings.TranslationApiType) ||
                string.IsNullOrEmpty(settings.TranslationSourceLang) ||
                string.IsNullOrEmpty(settings.TranslationTargetLang))
            {
                await context.SaveChangesAsync();
            }

            return settings;
        }

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
            existing.TranslationApiType = settings.TranslationApiType;
            existing.TranslationSourceLang = settings.TranslationSourceLang;
            existing.TranslationTargetLang = settings.TranslationTargetLang;
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
