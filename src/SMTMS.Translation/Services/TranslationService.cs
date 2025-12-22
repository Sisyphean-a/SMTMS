using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Translation.Services;

/// <summary>
/// 翻译服务实现 - 负责翻译数据的提取、恢复和同步
/// </summary>
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
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// 从旧版 JSON 文件导入翻译数据
    /// </summary>
    public async Task<OperationResult> ImportFromLegacyJsonAsync(
        string jsonPath, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从旧版 JSON 导入翻译: {JsonPath}", jsonPath);

        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("备份文件不存在: {JsonPath}", jsonPath);
            return OperationResult.Failure("备份文件不存在");
        }

        int successCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        try
        {
            string json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var translationsData = JsonConvert.DeserializeObject<Dictionary<string, TranslationBackupEntry>>(json);

            if (translationsData == null || !translationsData.Any())
            {
                _logger.LogWarning("备份文件为空或格式无效");
                return OperationResult.Failure("备份文件为空或格式无效");
            }

            using var scope = _scopeFactory.CreateScope();
            var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

            foreach (var (modName, modData) in translationsData)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (string.IsNullOrWhiteSpace(modData.UniqueID))
                    {
                        errors.Add($"模组 {modName} 缺少 UniqueID");
                        errorCount++;
                        continue;
                    }

                    var mod = await modRepo.GetModAsync(modData.UniqueID, cancellationToken);
                    if (mod == null)
                    {
                        mod = new ModMetadata { UniqueID = modData.UniqueID };
                    }

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
                        // Fallback: 检测是否包含中文
                        var chinesePattern = ChinesePatternRegex();
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
                        await modRepo.UpsertModAsync(mod, cancellationToken);
                        successCount++;
                        _logger.LogDebug("成功导入模组翻译: {UniqueId}", modData.UniqueID);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "导入模组 {ModName} 时出错", modName);
                    errors.Add($"{modName}: {ex.Message}");
                    errorCount++;
                }
            }

            _logger.LogInformation("导入完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);
            
            if (errorCount == 0)
            {
                return OperationResult.Success(successCount, $"成功导入 {successCount} 个翻译");
            }
            else
            {
                return OperationResult.PartialSuccess(successCount, errorCount, 
                    $"导入完成: {successCount} 成功, {errorCount} 失败", errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入失败");
            return OperationResult.Failure($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 扫描 manifest.json 文件并保存翻译到数据库
    /// </summary>
    public async Task<OperationResult> SaveTranslationsToDbAsync(
        string modDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始保存翻译到数据库: {ModDirectory}", modDirectory);

        if (!Directory.Exists(modDirectory))
        {
            _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
            return OperationResult.Failure("模组目录不存在");
        }

        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        _logger.LogInformation("找到 {Count} 个 manifest.json 文件", modFiles.Length);

        int successCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        // 🔥 性能优化：并行计算文件 Hash
        var fileHashTasks = modFiles.Select(async file =>
        {
            try
            {
                var content = await File.ReadAllBytesAsync(file, cancellationToken);
                var hash = Convert.ToBase64String(MD5.HashData(content));
                return (file, hash, success: true);
            }
            catch
            {
                return (file, string.Empty, success: false);
            }
        }).ToArray();

        var fileHashes = await Task.WhenAll(fileHashTasks);

        foreach (var (file, hash, success) in fileHashes)
        {
            if (!success)
            {
                errorCount++;
                errors.Add($"无法读取文件: {Path.GetFileName(file)}");
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                {
                    _logger.LogWarning("跳过无效的 manifest: {File}", file);
                    continue;
                }

                var mod = await modRepo.GetModAsync(manifest.UniqueID, cancellationToken);
                if (mod == null)
                {
                    mod = new ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        RelativePath = Path.GetRelativePath(modDirectory, file)
                    };
                }

                // 🔥 性能优化：使用 Hash 快速判断文件是否变更
                if (mod.LastFileHash == hash)
                {
                    continue; // 文件未变更，跳过
                }

                bool updated = false;

                // 保存当前状态到翻译字段
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

                if (updated || mod.LastTranslationUpdate == null)
                {
                    mod.LastTranslationUpdate = DateTime.Now;
                    mod.LastFileHash = hash;
                    await modRepo.UpsertModAsync(mod, cancellationToken);
                    successCount++;
                    _logger.LogDebug("保存翻译: {UniqueId}", manifest.UniqueID);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存翻译失败: {File}", file);
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                errorCount++;
            }
        }

        _logger.LogInformation("保存完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        if (errorCount == 0)
        {
            return OperationResult.Success(successCount, $"成功保存 {successCount} 个翻译");
        }
        else
        {
            return OperationResult.PartialSuccess(successCount, errorCount,
                $"保存完成: {successCount} 成功, {errorCount} 失败", errors);
        }
    }

    /// <summary>
    /// 从数据库恢复翻译到 manifest.json 文件
    /// </summary>
    public async Task<OperationResult> RestoreTranslationsFromDbAsync(
        string modDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从数据库恢复翻译: {ModDirectory}", modDirectory);

        if (!Directory.Exists(modDirectory))
        {
            _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
            return OperationResult.Failure("模组目录不存在");
        }

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var allTranslatedMods = (await modRepo.GetAllModsAsync(cancellationToken))
            .Where(m => !string.IsNullOrEmpty(m.TranslatedName) || !string.IsNullOrEmpty(m.TranslatedDescription))
            .ToList();

        _logger.LogInformation("找到 {Count} 个已翻译的模组", allTranslatedMods.Count);

        if (!allTranslatedMods.Any())
        {
            return OperationResult.Success(0, "没有需要恢复的翻译");
        }

        var translationMap = allTranslatedMods.ToDictionary(m => m.UniqueID);
        var modFiles = Directory.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);

        int successCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行处理所有文件
        var tasks = modFiles.Select(async file =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(content);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                {
                    return (success: false, error: $"无效的 manifest: {Path.GetFileName(file)}");
                }

                if (translationMap.TryGetValue(manifest.UniqueID, out var dbMod))
                {
                    bool changed = false;

                    // 恢复 Name
                    if (!string.IsNullOrEmpty(dbMod.TranslatedName) && manifest.Name != dbMod.TranslatedName)
                    {
                        string escapedName = JsonConvert.ToString(dbMod.TranslatedName).Trim('"');
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

                    // 恢复 Description
                    if (!string.IsNullOrEmpty(dbMod.TranslatedDescription) && manifest.Description != dbMod.TranslatedDescription)
                    {
                        string escapedDesc = JsonConvert.ToString(dbMod.TranslatedDescription).Trim('"');
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
                        await File.WriteAllTextAsync(file, content, cancellationToken);
                        _logger.LogDebug("恢复翻译: {UniqueId}", manifest.UniqueID);
                        return (success: true, error: (string?)null);
                    }
                }

                return (success: true, error: (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "恢复翻译失败: {File}", file);
                return (success: false, error: $"{Path.GetFileName(file)}: {ex.Message}");
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var (success, error) in results)
        {
            if (success)
            {
                successCount++;
            }
            else
            {
                errorCount++;
                if (error != null)
                {
                    errors.Add(error);
                }
            }
        }

        _logger.LogInformation("恢复完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        if (errorCount == 0)
        {
            return OperationResult.Success(successCount, $"成功恢复 {successCount} 个翻译");
        }
        else
        {
            return OperationResult.PartialSuccess(successCount, errorCount,
                $"恢复完成: {successCount} 成功, {errorCount} 失败", errors);
        }
    }

    /// <summary>
    /// 导出翻译到 Git 仓库
    /// </summary>
    public async Task<OperationResult> ExportTranslationsToGitRepo(
        string modDirectory,
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始导出翻译到 Git 仓库: {RepoPath}", repoPath);

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var allMods = (await modRepo.GetAllModsAsync(cancellationToken)).ToList();

        // 确保仓库 Mods 文件夹存在
        var repoModsPath = Path.Combine(repoPath, "Mods");
        if (!Directory.Exists(repoModsPath))
        {
            Directory.CreateDirectory(repoModsPath);
        }

        int successCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行导出所有文件
        var tasks = allMods.Select(async mod =>
        {
            if (string.IsNullOrEmpty(mod.RelativePath))
            {
                return (success: true, error: (string?)null);
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sourcePath = Path.Combine(modDirectory, mod.RelativePath);
                if (!File.Exists(sourcePath))
                {
                    return (success: true, error: (string?)null); // 模组可能已删除
                }

                // 读取源文件
                var json = await File.ReadAllTextAsync(sourcePath, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);
                if (manifest == null)
                {
                    return (success: false, error: $"无法解析: {mod.RelativePath}");
                }

                // 应用数据库中的翻译
                if (!string.IsNullOrEmpty(mod.TranslatedName))
                {
                    manifest.Name = mod.TranslatedName;
                }
                if (!string.IsNullOrEmpty(mod.TranslatedDescription))
                {
                    manifest.Description = mod.TranslatedDescription;
                }

                // 写入到 Git 仓库
                var targetPath = Path.Combine(repoPath, mod.RelativePath);
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                var outputJson = JsonConvert.SerializeObject(manifest, _jsonSettings);
                await File.WriteAllTextAsync(targetPath, outputJson, cancellationToken);

                _logger.LogDebug("导出翻译: {UniqueId}", mod.UniqueID);
                return (success: true, error: (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出翻译失败: {RelativePath}", mod.RelativePath);
                return (success: false, error: $"{mod.RelativePath}: {ex.Message}");
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var (success, error) in results)
        {
            if (success)
            {
                successCount++;
            }
            else
            {
                errorCount++;
                if (error != null)
                {
                    errors.Add(error);
                }
            }
        }

        _logger.LogInformation("导出完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        if (errorCount == 0)
        {
            return OperationResult.Success(successCount, $"成功导出 {successCount} 个翻译");
        }
        else
        {
            return OperationResult.PartialSuccess(successCount, errorCount,
                $"导出完成: {successCount} 成功, {errorCount} 失败", errors);
        }
    }

    /// <summary>
    /// 从 Git 仓库读取翻译并更新数据库（用于回滚后同步）
    /// </summary>
    public async Task<OperationResult> ImportTranslationsFromGitRepoAsync(
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从 Git 仓库导入翻译: {RepoPath}", repoPath);

        var repoModsPath = Path.Combine(repoPath, "Mods");
        if (!Directory.Exists(repoModsPath))
        {
            _logger.LogWarning("Git 仓库 Mods 目录不存在: {RepoModsPath}", repoModsPath);
            return OperationResult.Failure("Git 仓库 Mods 目录不存在");
        }

        var modFiles = Directory.GetFiles(repoModsPath, "manifest.json", SearchOption.AllDirectories);
        _logger.LogInformation("找到 {Count} 个 manifest.json 文件", modFiles.Length);

        int successCount = 0;
        int errorCount = 0;
        var errors = new List<string>();

        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        // 🔥 性能优化：并行读取和解析所有文件
        var tasks = modFiles.Select(async file =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
                {
                    return (success: false, error: $"无效的 manifest: {Path.GetFileName(file)}", mod: (ModMetadata?)null);
                }

                var mod = await modRepo.GetModAsync(manifest.UniqueID, cancellationToken);
                if (mod == null)
                {
                    mod = new ModMetadata
                    {
                        UniqueID = manifest.UniqueID,
                        RelativePath = Path.GetRelativePath(repoModsPath, file)
                    };
                }

                // 更新翻译数据
                mod.TranslatedName = manifest.Name;
                mod.TranslatedDescription = manifest.Description;
                mod.LastTranslationUpdate = DateTime.Now;

                return (success: true, error: (string?)null, mod);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取文件失败: {File}", file);
                return (success: false, error: $"{Path.GetFileName(file)}: {ex.Message}", mod: (ModMetadata?)null);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // 收集所有成功的 Mod
        var modsToUpdate = results
            .Where(r => r.success && r.mod != null)
            .Select(r => r.mod!)
            .ToList();

        // 批量更新数据库
        if (modsToUpdate.Any())
        {
            try
            {
                await modRepo.UpsertModsAsync(modsToUpdate, cancellationToken);
                successCount = modsToUpdate.Count;
                _logger.LogInformation("批量更新了 {Count} 个模组", successCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量更新数据库失败");
                return OperationResult.Failure($"批量更新数据库失败: {ex.Message}");
            }
        }

        // 收集错误
        foreach (var (success, error, _) in results)
        {
            if (!success && error != null)
            {
                errorCount++;
                errors.Add(error);
            }
        }

        _logger.LogInformation("导入完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        if (errorCount == 0)
        {
            return OperationResult.Success(successCount, $"成功导入 {successCount} 个翻译");
        }
        else
        {
            return OperationResult.PartialSuccess(successCount, errorCount,
                $"导入完成: {successCount} 成功, {errorCount} 失败", errors);
        }
    }
}