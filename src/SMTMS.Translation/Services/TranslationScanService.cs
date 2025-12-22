using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Translation.Services;

/// <summary>
/// 翻译扫描服务 - 扫描 manifest.json 并保存翻译到数据库
/// </summary>
public class TranslationScanService(
    ILogger<TranslationScanService> logger,
    IFileSystem fileSystem)
{
    private readonly ILogger<TranslationScanService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <summary>
    /// 扫描 manifest.json 文件并保存翻译到数据库
    /// </summary>
    public async Task<OperationResult> SaveTranslationsToDbAsync(
        string modDirectory,
        IModRepository modRepo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始保存翻译到数据库: {ModDirectory}", modDirectory);

        if (!_fileSystem.DirectoryExists(modDirectory))
        {
            _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
            return OperationResult.Failure("模组目录不存在");
        }

        var modFiles = _fileSystem.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        _logger.LogInformation("找到 {Count} 个 manifest.json 文件", modFiles.Length);

        var successCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行计算文件 Hash
        var fileHashes = await ComputeFileHashesAsync(modFiles, cancellationToken);

        foreach (var (file, hash, success) in fileHashes)
        {
            if (!success)
            {
                errorCount++;
                errors.Add($"无法读取文件: {_fileSystem.GetFileName(file)}");
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProcessManifestFileAsync(file, hash, modDirectory, modRepo, cancellationToken);
            if (result.success)
            {
                successCount++;
            }
            else if (result.error != null)
            {
                errorCount++;
                errors.Add(result.error);
            }
        }

        _logger.LogInformation("保存完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        return CreateOperationResult(successCount, errorCount, errors, "保存");
    }

    /// <summary>
    /// 并行计算文件 Hash
    /// </summary>
    private async Task<(string file, string hash, bool success)[]> ComputeFileHashesAsync(
        string[] files,
        CancellationToken cancellationToken)
    {
        var fileHashTasks = files.Select(async file =>
        {
            try
            {
                var content = await _fileSystem.ReadAllBytesAsync(file, cancellationToken);
                var hash = Convert.ToBase64String(MD5.HashData(content));
                return (file, hash, success: true);
            }
            catch
            {
                return (file, string.Empty, success: false);
            }
        }).ToArray();

        return await Task.WhenAll(fileHashTasks);
    }

    /// <summary>
    /// 处理单个 manifest.json 文件
    /// </summary>
    private async Task<(bool success, string? error)> ProcessManifestFileAsync(
        string file,
        string hash,
        string modDirectory,
        IModRepository modRepo,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _fileSystem.ReadAllTextAsync(file, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
            {
                _logger.LogWarning("跳过无效的 manifest: {File}", file);
                return (false, null);
            }

            var mod = await modRepo.GetModAsync(manifest.UniqueID, cancellationToken) ?? new ModMetadata
            {
                UniqueID = manifest.UniqueID,
                RelativePath = _fileSystem.GetRelativePath(modDirectory, file)
            };

            var currentRelativePath = _fileSystem.GetRelativePath(modDirectory, file);

            if (mod.RelativePath != currentRelativePath)
            {
                mod.RelativePath = currentRelativePath;
                // 强制更新 LastFileHash 以确保被保存
                mod.LastFileHash = string.Empty; 
            }

            var changes = GetModChanges(mod, manifest);
            if (changes.Count > 0 || mod.LastTranslationUpdate == null || mod.RelativePath != _fileSystem.GetRelativePath(modDirectory, file))
            {
                ApplyChangesToMod(mod, manifest);
                mod.LastTranslationUpdate = DateTime.Now;
                mod.LastFileHash = hash;
                await modRepo.UpsertModAsync(mod, cancellationToken);
                
                foreach (var change in changes)
                {
                    _logger.LogInformation("更新翻译 [{UniqueId}]: {Change}", manifest.UniqueID, change);
                }
                return (true, null);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存翻译失败: {File}", file);
            return (false, $"{_fileSystem.GetFileName(file)}: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取模组变更列表
    /// </summary>
    private List<string> GetModChanges(ModMetadata mod, ModManifest manifest)
    {
        var changes = new List<string>();

        if (mod.TranslatedName != manifest.Name)
        {
            changes.Add($"名称: '{mod.TranslatedName}' -> '{manifest.Name}'");
        }

        if (mod.TranslatedDescription != manifest.Description)
        {
            // 描述可能很长，只记录变更事实
            changes.Add("描述已更新");
        }

        return changes;
    }

    /// <summary>
    /// 应用变更到模组对象
    /// </summary>
    private void ApplyChangesToMod(ModMetadata mod, ModManifest manifest)
    {
        mod.TranslatedName = manifest.Name;
        mod.TranslatedDescription = manifest.Description;
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

