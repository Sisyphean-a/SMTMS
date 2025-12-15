using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public class TranslationService : ITranslationService
{
    private readonly JsonSerializerSettings _jsonSettings;

    public TranslationService()
    {
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public async Task<(int successCount, int errorCount, string message)> BackupTranslationsAsync(string modsRootPath, string backupPath)
    {
        if (!Directory.Exists(modsRootPath))
        {
            return (0, 0, "Mods directory not found.");
        }

        var translationsData = new Dictionary<string, TranslationBackupEntry>();
        int successCount = 0;
        int errorCount = 0;

        try
        {
            // Find all manifest.json files recursively
            var manifestFiles = Directory.GetFiles(modsRootPath, "manifest.json", SearchOption.AllDirectories);

            foreach (var manifestPath in manifestFiles)
            {
                try
                {
                    string content = await File.ReadAllTextAsync(manifestPath);
                    
                    // Simple parsing just to get the properties we generally care about.
                    // We can reuse ModManifest model or parse loosely. 
                    // Using ModManifest is safer if the json is valid.
                    // However, some manifests might be technically invalid but work in game (comments etc).
                    // JsonConvert with standard settings might fail on comments if not configured.
                    // But ModService is using specific settings for that. Let's try to parse loosely or use the ModManifest model logic.
                    // Actually, let's use the same logic as the Python script: Regex extraction for robustness against comments? 
                    // Or just use Newtonsoft with comment support (which is standard in Core/Services/ModService.cs).
                    // Let's use a local parsing with comment support.
                    
                    var entry = ExtractTranslationEntry(content, manifestPath, modsRootPath);

                    if (entry != null && (entry.Name != null || entry.Description != null))
                    {
                        translationsData[entry.Path] = entry;
                        successCount++;
                    }
                    else
                    {
                        // Maybe log that no extractable data was found? Not necessarily an error.
                    }
                }
                catch (Exception)
                {
                    errorCount++;
                }
            }

            if (translationsData.Count > 0)
            {
                var json = JsonConvert.SerializeObject(translationsData, _jsonSettings);
                await File.WriteAllTextAsync(backupPath, json);
                return (successCount, errorCount, $"Successfully backed up {successCount} mods to {backupPath}");
            }
            else
            {
                return (0, errorCount, "No translation data found to backup.");
            }
        }
        catch (Exception ex)
        {
            return (successCount, errorCount, $"Backup failed: {ex.Message}");
        }
    }

    private TranslationBackupEntry? ExtractTranslationEntry(string content, string manifestPath, string modsRootPath)
    {
        // Regex patterns matching the Python script
        var namePattern = new Regex(@"""Name""\s*:\s*""([^""]*)""");
        var descPattern = new Regex(@"""Description""\s*:\s*""([^""]*)""");
        var uniqueIdPattern = new Regex(@"""UniqueID""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
        var chinesePattern = new Regex(@"[\u4e00-\u9fff]");
        
        // Remove comments for extraction purposes (Python script does this)
        // Note: Python script regex for comments:
        // content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
        // content = re.sub(r'//.*?$', '', content, flags=re.MULTILINE)
        
        string cleanContent = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
        cleanContent = Regex.Replace(cleanContent, @"//.*?$", "", RegexOptions.Multiline);

        var nameMatch = namePattern.Match(cleanContent);
        string? name = nameMatch.Success ? nameMatch.Groups[1].Value : null;

        var descMatch = descPattern.Match(cleanContent);
        string? description = descMatch.Success ? descMatch.Groups[1].Value : null;

        var uniqueIdMatch = uniqueIdPattern.Match(cleanContent);
        string? uniqueId = uniqueIdMatch.Success ? uniqueIdMatch.Groups[1].Value : null;

        string modFolder = new DirectoryInfo(Path.GetDirectoryName(manifestPath)!).Name;
        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = modFolder;
        }

        bool isChinese = false;
        if (!string.IsNullOrEmpty(name) && chinesePattern.IsMatch(name)) isChinese = true;
        if (!string.IsNullOrEmpty(description) && chinesePattern.IsMatch(description)) isChinese = true;

        // Try to extract nexus ID if possible (from UpdateKeys) - simplified for now or skip if complex
        // Python script has logic for this, let's include it if easy
        string? nurl = null;
        var updateKeysPattern = new Regex(@"""UpdateKeys""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
        var nexusPattern = new Regex(@"""Nexus:(\d+)""");
        
        var updateKeysMatch = updateKeysPattern.Match(cleanContent);
        if (updateKeysMatch.Success)
        {
            var nexusMatch = nexusPattern.Match(updateKeysMatch.Groups[1].Value);
            if (nexusMatch.Success)
            {
                nurl = $"https://www.nexusmods.com/stardewvalley/mods/{nexusMatch.Groups[1].Value}";
            }
        }

        if (name != null || description != null)
        {
            string relativePath = Path.GetRelativePath(modsRootPath, Path.GetDirectoryName(manifestPath)!);
            
            return new TranslationBackupEntry
            {
                UniqueID = uniqueId,
                Name = name,
                Description = description,
                Path = relativePath,
                IsChinese = isChinese,
                Nurl = nurl
            };
        }

        return null;
    }

    public async Task<(int restoredCount, int failedCount, int skippedCount, string message)> RestoreTranslationsAsync(string modsRootPath, string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            return (0, 0, 0, "Backup file not found.");
        }

        int restoredCount = 0;
        int failedCount = 0;
        int skippedCount = 0;

        Dictionary<string, TranslationBackupEntry>? translationsData;

        try
        {
            string json = await File.ReadAllTextAsync(backupPath);
            translationsData = JsonConvert.DeserializeObject<Dictionary<string, TranslationBackupEntry>>(json);
        }
        catch (Exception ex)
        {
             return (0, 0, 0, $"Failed to read backup file: {ex.Message}");
        }

        if (translationsData == null)
        {
             return (0, 0, 0, "Backup file is empty or invalid.");
        }

        var chinesePattern = new Regex(@"[\u4e00-\u9fff]");

        foreach (var kvp in translationsData)
        {
            var modPath = kvp.Key;
            var modData = kvp.Value;
            var manifestPath = Path.Combine(modsRootPath, modPath, "manifest.json");

            if (!File.Exists(manifestPath))
            {
                skippedCount++;
                continue;
            }

            try
            {
                string content = await File.ReadAllTextAsync(manifestPath);

                if (string.IsNullOrWhiteSpace(content))
                {
                    skippedCount++;
                    continue;
                }

                bool shouldUpdate = modData.IsChinese;
                if (!shouldUpdate)
                {
                    // Fallback check if IsChinese not explicitly set or false but obviously has Chinese
                    if ((!string.IsNullOrEmpty(modData.Name) && chinesePattern.IsMatch(modData.Name)) ||
                        (!string.IsNullOrEmpty(modData.Description) && chinesePattern.IsMatch(modData.Description)))
                    {
                        shouldUpdate = true;
                    }
                }

                if (!shouldUpdate)
                {
                   skippedCount++;
                   continue;
                }

                bool updated = false;

                // Update Name
                if (!string.IsNullOrEmpty(modData.Name))
                {
                    string escapedName = modData.Name.Replace(@"\", @"\\").Replace(@"""", @"\""");
                    // Regex replacement to preserve surrounding structure
                    if (Regex.IsMatch(content, @"""Name""\s*:\s*""[^""]*"""))
                    {
                        content = Regex.Replace(content, @"(""Name""\s*:\s*"")[^""]*("")", $"${{1}}{escapedName}${{2}}");
                        updated = true;
                    }
                }

                // Update Description
                if (!string.IsNullOrEmpty(modData.Description))
                {
                    string escapedDesc = modData.Description.Replace(@"\", @"\\").Replace(@"""", @"\""");
                     if (Regex.IsMatch(content, @"""Description""\s*:\s*""[^""]*"""))
                    {
                         content = Regex.Replace(content, @"(""Description""\s*:\s*"")[^""]*("")", $"${{1}}{escapedDesc}${{2}}");
                         updated = true;
                    }
                }

                if (updated)
                {
                    await File.WriteAllTextAsync(manifestPath, content);
                    restoredCount++;
                }
                else
                {
                    failedCount++;
                }

            }
            catch (Exception)
            {
                failedCount++;
            }
        }

        return (restoredCount, failedCount, skippedCount, $"Restore complete. Restored: {restoredCount}, Failed: {failedCount}, Skipped: {skippedCount}");
    }
}
