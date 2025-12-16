using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Core.Aspects;
using Microsoft.Extensions.DependencyInjection;

namespace SMTMS.Core.Services;

[Log]
public class TranslationService : ITranslationService
{
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly IServiceScopeFactory _scopeFactory;

    public TranslationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public async Task<(int successCount, int errorCount, string message)> ImportFromLegacyJsonAsync(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            return (0, 0, "Backup file not found.");
        }

        int successCount = 0;
        int errorCount = 0;

        try
        {
            string json = await File.ReadAllTextAsync(jsonPath);
            var translationsData = JsonConvert.DeserializeObject<Dictionary<string, TranslationBackupEntry>>(json);

            if (translationsData == null || !translationsData.Any())
            {
                return (0, 0, "Backup file is empty or invalid.");
            }

            using var scope = _scopeFactory.CreateScope();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            foreach (var kvp in translationsData)
            {
                try
                {
                    var modData = kvp.Value;
                    if (string.IsNullOrEmpty(modData.UniqueID)) continue;

                    var mod = await modRepo.GetModAsync(modData.UniqueID);
                    if (mod == null)
                    {
                        mod = new ModMetadata
                        {
                            UniqueID = modData.UniqueID,
                            RelativePath = kvp.Key // Use the key which is the folder name usually
                        };
                    }

                    // Only import if we have translation data
                    // Note: Legacy JSON has "Name" and "Description" as the *translated* values if IsChinese is true
                    // Or sometimes mixed.
                    // If IsChinese is true, we assume Name/Description are the Chinese versions.
                    
                    bool updated = false;
                    if (modData.IsChinese)
                    {
                        if (!string.IsNullOrEmpty(modData.Name))
                        {
                            mod.TranslatedName = modData.Name;
                            updated = true;
                        }
                        if (!string.IsNullOrEmpty(modData.Description))
                        {
                            mod.TranslatedDescription = modData.Description;
                            updated = true;
                        }
                    }
                    else
                    {
                        // Fallback heuristic: check if content contains chinese
                        var chinesePattern = new Regex(@"[\u4e00-\u9fff]");
                         if (!string.IsNullOrEmpty(modData.Name) && chinesePattern.IsMatch(modData.Name))
                        {
                            mod.TranslatedName = modData.Name;
                            updated = true;
                        }
                        if (!string.IsNullOrEmpty(modData.Description) && chinesePattern.IsMatch(modData.Description))
                        {
                            mod.TranslatedDescription = modData.Description;
                            updated = true;
                        }
                    }

                    if (updated)
                    {
                        mod.LastTranslationUpdate = DateTime.Now;
                        await modRepo.UpsertModAsync(mod);
                        successCount++;
                    }
                }
                catch (Exception)
                {
                    errorCount++;
                }
            }
            
            return (successCount, errorCount, $"Imported {successCount} translations.");
        }
        catch (Exception ex)
        {
            return (0, 0, $"Import failed: {ex.Message}");
        }
    }

    public async Task SaveTranslationsToDbAsync(string modDirectory)
    {
        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        var chineseRegex = new Regex(@"[\u4e00-\u9fa5]");

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        foreach (var file in modFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                    continue;

                bool hasChineseName = !string.IsNullOrEmpty(manifest.Name) && chineseRegex.IsMatch(manifest.Name);
                bool hasChineseDesc = !string.IsNullOrEmpty(manifest.Description) && chineseRegex.IsMatch(manifest.Description);

                if (hasChineseName || hasChineseDesc)
                {
                    var mod = await modRepo.GetModAsync(manifest.UniqueID);
                     if (mod == null)
                    {
                        mod = new ModMetadata
                        {
                            UniqueID = manifest.UniqueID,
                            RelativePath = Path.GetRelativePath(modDirectory, file),
                            OriginalName = manifest.Name, // If it's already Chinese, this might be conceptually wrong but practically okay
                            OriginalDescription = manifest.Description
                        };
                    }

                    bool updated = false;
                    if (hasChineseName && mod.TranslatedName != manifest.Name)
                    {
                        mod.TranslatedName = manifest.Name;
                        updated = true;
                    }
                    if (hasChineseDesc && mod.TranslatedDescription != manifest.Description)
                    {
                        mod.TranslatedDescription = manifest.Description;
                        updated = true;
                    }

                    if (updated)
                    {
                        mod.LastTranslationUpdate = DateTime.Now;
                        await modRepo.UpsertModAsync(mod);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to DB from {file}: {ex.Message}");
            }
        }
    }

    public async Task RestoreTranslationsFromDbAsync(string modDirectory)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var allTranslatedMods = (await modRepo.GetAllModsAsync())
                                .Where(m => !string.IsNullOrEmpty(m.TranslatedName) || !string.IsNullOrEmpty(m.TranslatedDescription))
                                .ToList();

        // Map UniqueID to Metadata for fast lookup
        var translationMap = allTranslatedMods.ToDictionary(m => m.UniqueID);

        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);

        foreach (var file in modFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(content);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                    continue;

                if (translationMap.TryGetValue(manifest.UniqueID, out var dbMod))
                {
                    bool changed = false;

                    // Restore Name
                    if (!string.IsNullOrEmpty(dbMod.TranslatedName) && manifest.Name != dbMod.TranslatedName)
                    {
                        string escapedName = JsonConvert.ToString(dbMod.TranslatedName).Trim('"');
                        if (Regex.IsMatch(content, @"""Name""\s*:\s*""[^""]*"""))
                        {
                            string newContent = Regex.Replace(content, @"(""Name""\s*:\s*"")[^""]*("")", $"${{1}}{escapedName}${{2}}");
                            if (content != newContent)
                            {
                                content = newContent;
                                changed = true;
                            }
                        }
                    }

                    // Restore Description
                    if (!string.IsNullOrEmpty(dbMod.TranslatedDescription) && manifest.Description != dbMod.TranslatedDescription)
                    {
                         string escapedDesc = JsonConvert.ToString(dbMod.TranslatedDescription).Trim('"');
                         if (Regex.IsMatch(content, @"""Description""\s*:\s*""[^""]*"""))
                        {
                            string newContent = Regex.Replace(content, @"(""Description""\s*:\s*"")[^""]*("")", $"${{1}}{escapedDesc}${{2}}");
                            if (content != newContent)
                            {
                                content = newContent;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        await File.WriteAllTextAsync(file, content);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring to {file}: {ex.Message}");
            }
        }
    }
}
