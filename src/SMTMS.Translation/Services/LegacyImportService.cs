using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Translation.Helpers;

namespace SMTMS.Translation.Services;

/// <summary>
/// 旧版 JSON 翻译导入服务
/// </summary>
public class LegacyImportService(
    ILogger<LegacyImportService> logger,
    IFileSystem fileSystem)
{
    private readonly ILogger<LegacyImportService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    /// 从旧版 JSON 文件导入翻译数据
    /// </summary>
    public async Task<OperationResult> ImportFromLegacyJsonAsync(
        string jsonPath,
        IModRepository modRepo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从旧版 JSON 导入翻译: {JsonPath}", jsonPath);

        if (!_fileSystem.FileExists(jsonPath))
        {
            _logger.LogWarning("备份文件不存在: {JsonPath}", jsonPath);
            return OperationResult.Failure("备份文件不存在");
        }

        try
        {
            var translationsData = await LoadTranslationDataAsync(jsonPath, cancellationToken);
            if (translationsData == null)
            {
                return OperationResult.Failure("备份文件为空或格式无效");
            }

            var (successCount, errorCount, errors) = await ProcessAllLegacyModsAsync(
                translationsData, modRepo, cancellationToken);

            _logger.LogInformation("导入完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

            return CreateOperationResult(successCount, errorCount, errors, "导入");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入失败");
            return OperationResult.Failure($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载翻译数据文件
    /// </summary>
    private async Task<Dictionary<string, TranslationBackupEntry>?> LoadTranslationDataAsync(
        string jsonPath,
        CancellationToken cancellationToken)
    {
        var json = await _fileSystem.ReadAllTextAsync(jsonPath, cancellationToken);
        var translationsData = JsonConvert.DeserializeObject<Dictionary<string, TranslationBackupEntry>>(json);

        if (translationsData != null && translationsData.Count != 0) return translationsData;
        _logger.LogWarning("备份文件为空或格式无效");
        return null;

    }

    /// <summary>
    /// 处理所有旧版模组条目
    /// </summary>
    private async Task<(int successCount, int errorCount, List<string> errors)> ProcessAllLegacyModsAsync(
        Dictionary<string, TranslationBackupEntry> translationsData,
        IModRepository modRepo,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        foreach (var (modName, modData) in translationsData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProcessLegacyModEntryAsync(modName, modData, modRepo, cancellationToken);
            if (result.success)
            {
                successCount++;
            }
            else
            {
                errorCount++;
                if (result.error != null)
                {
                    errors.Add(result.error);
                }
            }
        }

        return (successCount, errorCount, errors);
    }

    /// <summary>
    /// 处理单个旧版模组条目
    /// </summary>
    private async Task<(bool success, string? error)> ProcessLegacyModEntryAsync(
        string modName,
        TranslationBackupEntry modData,
        IModRepository modRepo,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modData.UniqueID))
            {
                return (false, $"模组 {modName} 缺少 UniqueID");
            }

            var mod = await modRepo.GetModAsync(modData.UniqueID, cancellationToken)
                      ?? new ModMetadata { UniqueID = modData.UniqueID };

            var updated = UpdateModTranslations(mod, modData);

            if (updated)
            {
                mod.LastTranslationUpdate = DateTime.Now;
                await modRepo.UpsertModAsync(mod, cancellationToken);
                _logger.LogDebug("成功导入模组翻译: {UniqueId}", modData.UniqueID);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入模组 {ModName} 时出错", modName);
            return (false, $"{modName}: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新模组翻译数据
    /// </summary>
    private bool UpdateModTranslations(ModMetadata mod, TranslationBackupEntry modData)
    {
        return modData.IsChinese
            ? UpdateTranslationsDirectly(mod, modData)
            : UpdateTranslationsWithChineseCheck(mod, modData);
    }

    /// <summary>
    /// 直接更新翻译（已标记为中文）
    /// </summary>
    private bool UpdateTranslationsDirectly(ModMetadata mod, TranslationBackupEntry modData)
    {
        var nameUpdated = TryUpdateTranslatedName(mod, modData.Name);
        var descUpdated = TryUpdateTranslatedDescription(mod, modData.Description);
        return nameUpdated || descUpdated;
    }

    /// <summary>
    /// 检测中文后更新翻译
    /// </summary>
    private bool UpdateTranslationsWithChineseCheck(ModMetadata mod, TranslationBackupEntry modData)
    {
        var nameUpdated = TryUpdateTranslatedNameIfChinese(mod, modData.Name);
        var descUpdated = TryUpdateTranslatedDescriptionIfChinese(mod, modData.Description);
        return nameUpdated || descUpdated;
    }

    /// <summary>
    /// 尝试更新翻译名称
    /// </summary>
    private bool TryUpdateTranslatedName(ModMetadata mod, string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        mod.TranslatedName = name;
        return true;
    }

    /// <summary>
    /// 尝试更新翻译描述
    /// </summary>
    private bool TryUpdateTranslatedDescription(ModMetadata mod, string? description)
    {
        if (string.IsNullOrEmpty(description)) return false;

        mod.TranslatedDescription = description;
        return true;
    }

    /// <summary>
    /// 如果包含中文则更新翻译名称
    /// </summary>
    private bool TryUpdateTranslatedNameIfChinese(ModMetadata mod, string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!ManifestTextReplacer.ContainsChinese(name)) return false;

        mod.TranslatedName = name;
        return true;
    }

    /// <summary>
    /// 如果包含中文则更新翻译描述
    /// </summary>
    private bool TryUpdateTranslatedDescriptionIfChinese(ModMetadata mod, string? description)
    {
        if (string.IsNullOrEmpty(description)) return false;
        if (!ManifestTextReplacer.ContainsChinese(description)) return false;

        mod.TranslatedDescription = description;
        return true;
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

