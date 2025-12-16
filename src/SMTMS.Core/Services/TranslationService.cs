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

    public async Task<(int appliedCount, int errorCount, string message)> ApplyTranslationsAsync(string modsRootPath)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        var allMods = await modRepo.GetAllModsAsync();
        int appliedCount = 0;
        int errorCount = 0;

        foreach (var mod in allMods)
        {
            // Skip if no translations
            if (string.IsNullOrEmpty(mod.TranslatedName) && string.IsNullOrEmpty(mod.TranslatedDescription))
            {
                continue;
            }
            
            // We need to resolve the path. 
            // If RelativePath is stored, use it. But RelativePath might be just the folder name or empty if not set.
            // If we don't have a reliable path, we might need to scan again or rely on ModService to finding it?
            // For now, assume RelativePath is correct if it exists. 
            // The scan logic should update RelativePath.
            
            if (string.IsNullOrEmpty(mod.RelativePath))
            {
                // Can't apply if we don't know where it is
                // Potentially log this
                continue;
            }

            var manifestPath = Path.Combine(modsRootPath, mod.RelativePath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                // Try searching? Or just error.
                errorCount++;
                continue;
            }

            try
            {
                string content = await File.ReadAllTextAsync(manifestPath);
                bool updated = false;

                // Update Name
                if (!string.IsNullOrEmpty(mod.TranslatedName))
                {
                    // Escape for JSON
                    string escapedName = JsonConvert.ToString(mod.TranslatedName).Trim('"');
                    if (Regex.IsMatch(content, @"""Name""\s*:\s*""[^""]*"""))
                    {
                        string newContent = Regex.Replace(content, @"(""Name""\s*:\s*"")[^""]*("")", $"${{1}}{escapedName}${{2}}");
                        if (content != newContent)
                        {
                            content = newContent;
                            updated = true;
                        }
                    }
                }

                // Update Description
                if (!string.IsNullOrEmpty(mod.TranslatedDescription))
                {
                     string escapedDesc = JsonConvert.ToString(mod.TranslatedDescription).Trim('"');
                     if (Regex.IsMatch(content, @"""Description""\s*:\s*""[^""]*"""))
                    {
                        string newContent = Regex.Replace(content, @"(""Description""\s*:\s*"")[^""]*("")", $"${{1}}{escapedDesc}${{2}}");
                        if (content != newContent)
                        {
                            content = newContent;
                            updated = true;
                        }
                    }
                }

                if (updated)
                {
                    await File.WriteAllTextAsync(manifestPath, content);
                    appliedCount++;
                }
            }
            catch (Exception)
            {
                errorCount++;
            }
        }

        return (appliedCount, errorCount, $"Applied {appliedCount} translations.");
    }
}
