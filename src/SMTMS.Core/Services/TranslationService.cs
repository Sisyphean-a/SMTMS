using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Core.Aspects;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

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

        // Pre-load all mods and map by Relative Path for fast lookup
        // We use this to compare unique file fingerprints
        var allMods = await modRepo.GetAllModsAsync();
        var pathMap = allMods
            .Where(m => !string.IsNullOrEmpty(m.RelativePath))
            // Handle potential duplicates by taking the first one or grouping
            .GroupBy(m => m.RelativePath)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var file in modFiles)
        {
            try
            {
                var relativePath = Path.GetRelativePath(modDirectory, file);
                
                // 1. 获取文件指纹
                string currentHash = ComputeMD5(file);
                
                ModMetadata? mod = null;
                bool fastSkip = false;

                // 2. 极速比对
                if (pathMap.TryGetValue(relativePath, out var existingMod))
                {
                    if (existingMod.LastFileHash == currentHash)
                    {
                        fastSkip = true;
                    }
                    mod = existingMod;
                }

                if (fastSkip)
                {
                    // 🔥 直接跳过！不用解析 JSON，不用正则匹配，不用写库
                    continue;
                }

                // 3. 只有指纹变了，才做繁重的脏活
                var json = await File.ReadAllTextAsync(file);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                    continue;

                bool hasChineseName = !string.IsNullOrEmpty(manifest.Name) && chineseRegex.IsMatch(manifest.Name);
                bool hasChineseDesc = !string.IsNullOrEmpty(manifest.Description) && chineseRegex.IsMatch(manifest.Description);

                // If mod was not found by path (New file?) or we want to double check by ID
                if (mod == null)
                {
                    mod = await modRepo.GetModAsync(manifest.UniqueID);
                    if (mod == null)
                    {
                        mod = new ModMetadata
                        {
                            UniqueID = manifest.UniqueID,
                            RelativePath = relativePath,
                            OriginalName = manifest.Name,
                            OriginalDescription = manifest.Description
                        };
                    }
                    else
                    {
                        // Found by ID (Moved file?), update path
                        mod.RelativePath = relativePath;
                    }
                }
                else
                {
                     // Ensure ID matches
                     if (mod.UniqueID != manifest.UniqueID)
                     {
                         // Path collision or ID changed. Re-fetch by ID.
                         mod = await modRepo.GetModAsync(manifest.UniqueID) ?? new ModMetadata
                         {
                             UniqueID = manifest.UniqueID,
                             RelativePath = relativePath,
                             OriginalName = manifest.Name,
                             OriginalDescription = manifest.Description
                         };
                     }
                }

                // Update Logic
                bool updated = false;

                if (mod.RelativePath != relativePath)
                {
                    mod.RelativePath = relativePath;
                    updated = true;
                }

                if (mod.TranslatedName != manifest.Name)
                {
                    mod.TranslatedName = manifest.Name;
                    updated = true;
                }
                if (mod.TranslatedDescription != manifest.Description)
                {
                    mod.TranslatedDescription = manifest.Description;
                    updated = true;
                }

                // 4. 更新指纹
                if (mod.LastFileHash != currentHash)
                {
                    mod.LastFileHash = currentHash;
                    updated = true;
                }

                if (updated || mod.LastTranslationUpdate == null)
                {
                    mod.LastTranslationUpdate = DateTime.Now;
                    await modRepo.UpsertModAsync(mod);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to DB from {file}: {ex.Message}");
            }
        }
    }

    private string ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
    public async Task ExportTranslationsToGitRepo(string modDirectory, string repoPath)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var allMods = await modRepo.GetAllModsAsync();
        
        // Ensure repo/mods folder exists
        var repoModsPath = Path.Combine(repoPath, "Mods");
        if (!Directory.Exists(repoModsPath))
        {
            Directory.CreateDirectory(repoModsPath);
        }

        // Clean up repo folder? 
        // If we want exact mirror, we might want to delete stale files.
        // But for now let's just overwrite existing.

        foreach (var mod in allMods)
        {
            if (string.IsNullOrEmpty(mod.RelativePath)) continue;

            var sourcePath = Path.Combine(modDirectory, mod.RelativePath);
            if (!File.Exists(sourcePath)) continue; // Mod might have been deleted

            // Read source
            var json = await File.ReadAllTextAsync(sourcePath);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json); 
            if (manifest == null) continue;

            // Apply translations from DB
            if (!string.IsNullOrEmpty(mod.TranslatedName)) manifest.Name = mod.TranslatedName;
            if (!string.IsNullOrEmpty(mod.TranslatedDescription)) manifest.Description = mod.TranslatedDescription;

            // Write to Repo
            var destPath = Path.Combine(repoModsPath, mod.RelativePath);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var outputJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            await File.WriteAllTextAsync(destPath, outputJson);
        }
    }
    public async Task ImportTranslationsFromGitRepoAsync(string repoPath)
    {
        var repoModsPath = Path.Combine(repoPath, "Mods");
        if (!Directory.Exists(repoModsPath))
        {
            return;
        }

        var modFiles = Directory.GetFiles(repoModsPath, "manifest.json", SearchOption.AllDirectories);

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

                var mod = await modRepo.GetModAsync(manifest.UniqueID);
                if (mod == null)
                {
                     mod = new ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        RelativePath = Path.GetRelativePath(repoModsPath, file) 
                    };
                }

                bool updated = false;

                // We assume the Repo contains the "Translated" version of Name/Description
                if (mod.TranslatedName != manifest.Name)
                {
                    mod.TranslatedName = manifest.Name;
                    updated = true;
                }
                if (mod.TranslatedDescription != manifest.Description)
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
            catch (Exception ex)
            {
                 Console.WriteLine($"Error importing from Git Repo {file}: {ex.Message}");
            }
        }
    }
}
