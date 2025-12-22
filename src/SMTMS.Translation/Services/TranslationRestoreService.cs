using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Translation.Helpers;

namespace SMTMS.Translation.Services;

/// <summary>
/// 翻译恢复服务 - 从数据库恢复翻译到 manifest.json 文件
/// </summary>
public class TranslationRestoreService(
    ILogger<TranslationRestoreService> logger,
    IFileSystem fileSystem)
{
    private readonly ILogger<TranslationRestoreService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    /// 从数据库恢复翻译到 manifest.json 文件
    /// </summary>
    public async Task<OperationResult> RestoreTranslationsFromDbAsync(
        string modDirectory,
        IModRepository modRepo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从数据库恢复翻译: {ModDirectory}", modDirectory);

        if (!_fileSystem.DirectoryExists(modDirectory))
        {
            _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
            return OperationResult.Failure("模组目录不存在");
        }

        var allTranslatedMods = (await modRepo.GetAllModsAsync(cancellationToken))
            .Where(m => !string.IsNullOrEmpty(m.TranslatedName) || !string.IsNullOrEmpty(m.TranslatedDescription))
            .ToList();

        _logger.LogInformation("找到 {Count} 个已翻译的模组", allTranslatedMods.Count);

        if (allTranslatedMods.Count == 0)
        {
            return OperationResult.Success(0, "没有需要恢复的翻译");
        }

        var translationMap = allTranslatedMods.ToDictionary(m => m.UniqueID);
        var modFiles = _fileSystem.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);

        var successCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行处理所有文件
        var tasks = modFiles.Select(file => RestoreTranslationToFileAsync(file, translationMap, cancellationToken)).ToArray();
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

        return CreateOperationResult(successCount, errorCount, errors, "恢复");
    }

    /// <summary>
    /// 恢复翻译到单个文件
    /// </summary>
    private async Task<(bool success, string? error)> RestoreTranslationToFileAsync(
        string file,
        Dictionary<string, ModMetadata> translationMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var content = await _fileSystem.ReadAllTextAsync(file, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(content);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
            {
                return (false, $"无效的 manifest: {_fileSystem.GetFileName(file)}");
            }

            if (!translationMap.TryGetValue(manifest.UniqueID, out var dbMod))
            {
                return (true, null);
            }

            var updatedContent = ApplyTranslationsToContent(content, manifest, dbMod);

            if (updatedContent == content) return (true, null);
            await _fileSystem.WriteAllTextAsync(file, updatedContent, cancellationToken);
            _logger.LogDebug("恢复翻译: {UniqueId}", manifest.UniqueID);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复翻译失败: {File}", file);
            return (false, $"{_fileSystem.GetFileName(file)}: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用翻译到文件内容
    /// </summary>
    private string ApplyTranslationsToContent(string content, ModManifest manifest, ModMetadata dbMod)
    {
        var updatedContent = content;

        // 只在需要时替换 Name
        if (!string.IsNullOrEmpty(dbMod.TranslatedName) && manifest.Name != dbMod.TranslatedName)
        {
            updatedContent = ManifestTextReplacer.ReplaceName(updatedContent, dbMod.TranslatedName);
        }

        // 只在需要时替换 Description
        if (!string.IsNullOrEmpty(dbMod.TranslatedDescription) && manifest.Description != dbMod.TranslatedDescription)
        {
            updatedContent = ManifestTextReplacer.ReplaceDescription(updatedContent, dbMod.TranslatedDescription);
        }

        return updatedContent;
    }

    /// <summary>
    /// 创建操作结果
    /// </summary>
    private static OperationResult CreateOperationResult(
        int successCount,
        int errorCount,
        List<string> errors,
        string operationName)
    {
        if (errorCount == 0)
        {
            return OperationResult.Success(successCount, $"成功{operationName} {successCount} 个翻译");
        }

        return OperationResult.PartialSuccess(successCount, errorCount,
            $"{operationName}完成: {successCount} 成功, {errorCount} 失败", errors);
    }
}

