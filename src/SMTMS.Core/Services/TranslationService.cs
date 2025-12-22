using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Core.Aspects;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace SMTMS.Core.Services;

[Log]
public partial class TranslationService : ITranslationService
{
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranslationService> _logger;

    // 🔥 正则表达式缓存优化 - 使用 GeneratedRegex (C# 11+)
    [GeneratedRegex(@"[\u4e00-\u9fff]")]
    private static partial Regex ChinesePatternRegex();

    [GeneratedRegex(@"[\u4e00-\u9fa5]")]
    private static partial Regex ChineseSimplifiedRegex();

    [GeneratedRegex(@"""Name""\s*:\s*""[^""]*""")]
    private static partial Regex NameFieldRegex();

    [GeneratedRegex(@"(""Name""\s*:\s*"")[^""]*("")")]
    private static partial Regex NameReplaceRegex();

    [GeneratedRegex(@"""Description""\s*:\s*""[^""]*""")]
    private static partial Regex DescriptionFieldRegex();

    [GeneratedRegex(@"(""Description""\s*:\s*"")[^""]*("")")]
    private static partial Regex DescriptionReplaceRegex();

    public TranslationService(IServiceScopeFactory scopeFactory, ILogger<TranslationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public async Task<(int successCount, int errorCount, string message)> ImportFromLegacyJsonAsync(
        string jsonPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jsonPath))
        {
            return (0, 0, "Backup file not found.");
        }

        int successCount = 0;
        int errorCount = 0;

        try
        {
            // 🔥 支持取消令牌
            string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var translationsData = JsonConvert.DeserializeObject<Dictionary<string, TranslationBackupEntry>>(json);

            if (translationsData == null || !translationsData.Any())
            {
                return (0, 0, "Backup file is empty or invalid.");
            }

            using var scope = _scopeFactory.CreateScope();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            foreach (var kvp in translationsData)
            {
                // 🔥 检查取消请求
                cancellationToken.ThrowIfCancellationRequested();

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
                        // 🔥 使用缓存的正则表达式
                        if (!string.IsNullOrEmpty(modData.Name) && ChinesePatternRegex().IsMatch(modData.Name))
                        {
                            mod.TranslatedName = modData.Name;
                            updated = true;
                        }
                        if (!string.IsNullOrEmpty(modData.Description) && ChinesePatternRegex().IsMatch(modData.Description))
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
                catch (Exception ex)
                {
                    // 🔥 使用 ILogger 替代 Console.WriteLine
                    _logger.LogError(ex, "导入翻译数据失败: {UniqueID}", kvp.Key);
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

    public async Task SaveTranslationsToDbAsync(string modDirectory, CancellationToken cancellationToken = default)
    {
        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        // 🔥 使用缓存的正则表达式，不再每次创建新实例

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        // Pre-load all mods and map by Relative Path for fast lookup
        var allMods = await modRepo.GetAllModsAsync();
        var pathMap = allMods
            .Where(m => !string.IsNullOrEmpty(m.RelativePath))
            .GroupBy(m => m.RelativePath)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 🔥 并行处理所有文件（第一阶段：快速指纹检查）
        var fileInfoTasks = modFiles.Select(async file =>
        {
            // 🔥 检查取消请求
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(modDirectory, file);
            var currentHash = ComputeMD5(file);

            // 快速跳过未变更的文件
            if (pathMap.TryGetValue(relativePath, out var existingMod) &&
                existingMod.LastFileHash == currentHash)
            {
                return (file, relativePath, currentHash, skip: true, mod: existingMod);
            }

            return (file, relativePath, currentHash, skip: false, mod: (ModMetadata?)null);
        }).ToList();

        var fileInfos = await Task.WhenAll(fileInfoTasks);

        // 🔥 并行读取和解析需要处理的文件（第二阶段：JSON 解析）
        var processTasks = fileInfos
            .Where(info => !info.skip)
            .Select(async info =>
            {
                try
                {
                    // 🔥 支持取消令牌
                    var json = await File.ReadAllTextAsync(info.file, cancellationToken);
                    var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                        return null;

                    // 查找或创建 ModMetadata
                    var mod = allMods.FirstOrDefault(m => m.UniqueID == manifest.UniqueID);
                    if (mod == null)
                    {
                        mod = new ModMetadata
                        {
                            UniqueID = manifest.UniqueID,
                            RelativePath = info.relativePath,
                            OriginalName = manifest.Name,
                            OriginalDescription = manifest.Description
                        };
                    }

                    // 更新逻辑
                    bool updated = false;

                    if (mod.RelativePath != info.relativePath)
                    {
                        mod.RelativePath = info.relativePath;
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

                    if (mod.LastFileHash != info.currentHash)
                    {
                        mod.LastFileHash = info.currentHash;
                        updated = true;
                    }

                    if (updated || mod.LastTranslationUpdate == null)
                    {
                        mod.LastTranslationUpdate = DateTime.Now;
                        return mod;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    // 🔥 使用 ILogger 替代 Console.WriteLine
                    _logger.LogError(ex, "保存翻译到数据库失败: {FilePath}", info.file);
                    return null;
                }
            }).ToList();

        var processedMods = await Task.WhenAll(processTasks);

        // 收集所有需要更新的 Mod
        var modsToUpdate = processedMods.Where(m => m != null).Cast<ModMetadata>().ToList();

        // 🔥 批量保存所有变更（一次数据库操作）
        if (modsToUpdate.Any())
        {
            await modRepo.UpsertModsAsync(modsToUpdate);
        }
    }

    private string ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public async Task RestoreTranslationsFromDbAsync(string modDirectory, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var allTranslatedMods = (await modRepo.GetAllModsAsync())
                                .Where(m => !string.IsNullOrEmpty(m.TranslatedName) || !string.IsNullOrEmpty(m.TranslatedDescription))
                                .ToList();

        // Map UniqueID to Metadata for fast lookup
        var translationMap = allTranslatedMods.ToDictionary(m => m.UniqueID);

        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);

        // 🔥 并行处理所有文件的读取、修改和写入
        var tasks = modFiles.Select(async file =>
        {
            try
            {
                // 🔥 支持取消令牌
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(content);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                    return;

                if (translationMap.TryGetValue(manifest.UniqueID, out var dbMod))
                {
                    bool changed = false;

                    // Restore Name
                    if (!string.IsNullOrEmpty(dbMod.TranslatedName) && manifest.Name != dbMod.TranslatedName)
                    {
                        string escapedName = JsonConvert.ToString(dbMod.TranslatedName).Trim('"');
                        // 🔥 使用缓存的正则表达式
                        if (NameFieldRegex().IsMatch(content))
                        {
                            string newContent = NameReplaceRegex().Replace(content, $"${{1}}{escapedName}${{2}}");
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
                         // 🔥 使用缓存的正则表达式
                         if (DescriptionFieldRegex().IsMatch(content))
                        {
                            string newContent = DescriptionReplaceRegex().Replace(content, $"${{1}}{escapedDesc}${{2}}");
                            if (content != newContent)
                            {
                                content = newContent;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        // 🔥 支持取消令牌
                        await File.WriteAllTextAsync(file, content, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // 🔥 使用 ILogger 替代 Console.WriteLine
                _logger.LogError(ex, "恢复翻译失败: {FilePath}", file);
            }
        }).ToList();

        await Task.WhenAll(tasks);
    }
    public async Task ExportTranslationsToGitRepo(string modDirectory, string repoPath, CancellationToken cancellationToken = default)
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

        // 🔥 并行处理所有 Mod 的导出
        var tasks = allMods
            .Where(mod => !string.IsNullOrEmpty(mod.RelativePath))
            .Select(async mod =>
            {
                try
                {
                    // 🔥 检查取消请求
                    cancellationToken.ThrowIfCancellationRequested();

                    var sourcePath = Path.Combine(modDirectory, mod.RelativePath);
                    if (!File.Exists(sourcePath)) return; // Mod might have been deleted

                    // Read source
                    var json = await File.ReadAllTextAsync(sourcePath, cancellationToken);
                    var manifest = JsonConvert.DeserializeObject<ModManifest>(json);
                    if (manifest == null) return;

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
                    await File.WriteAllTextAsync(destPath, outputJson, cancellationToken);
                }
                catch (Exception ex)
                {
                    // 🔥 使用 ILogger 替代 Console.WriteLine
                    _logger.LogError(ex, "导出模组失败: {UniqueID}", mod.UniqueID);
                }
            }).ToList();

        await Task.WhenAll(tasks);
    }
    public async Task ImportTranslationsFromGitRepoAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        var repoModsPath = Path.Combine(repoPath, "Mods");
        if (!Directory.Exists(repoModsPath))
        {
            return;
        }

        var modFiles = Directory.GetFiles(repoModsPath, "manifest.json", SearchOption.AllDirectories);

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        // 🔥 并行读取和解析所有文件
        var parseTasks = modFiles.Select(async file =>
        {
            try
            {
                // 🔥 支持取消令牌
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                    return ((ModManifest?)null, (string?)null);

                return (manifest, relativePath: Path.GetRelativePath(repoModsPath, file));
            }
            catch (Exception ex)
            {
                // 🔥 使用 ILogger 替代 Console.WriteLine
                _logger.LogError(ex, "解析 Git 仓库文件失败: {FilePath}", file);
                return ((ModManifest?)null, (string?)null);
            }
        }).ToList();

        var parsedResults = await Task.WhenAll(parseTasks);
        var validManifests = parsedResults
            .Where(r => r.Item1 != null && r.Item2 != null)
            .Select(r => (manifest: r.Item1!, relativePath: r.Item2!))
            .ToList();

        if (!validManifests.Any())
            return;

        // 批量获取现有的 Mod 数据
        var uniqueIds = validManifests.Select(r => r.manifest.UniqueID).ToList();
        var existingMods = await modRepo.GetModsByIdsAsync(uniqueIds);

        var modsToUpdate = new List<ModMetadata>();

        foreach (var result in validManifests)
        {
            var (manifest, relativePath) = result;

            ModMetadata mod;
            if (!existingMods.TryGetValue(manifest.UniqueID, out mod!))
            {
                mod = new ModMetadata
                {
                    UniqueID = manifest.UniqueID,
                    RelativePath = relativePath
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
                modsToUpdate.Add(mod);
            }
        }

        // 🔥 批量保存所有变更
        if (modsToUpdate.Any())
        {
            await modRepo.UpsertModsAsync(modsToUpdate);
        }
    }
}
