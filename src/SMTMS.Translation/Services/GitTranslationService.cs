using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Translation.Services;

/// <summary>
/// Git 翻译服务 - 处理 Git 仓库的翻译导入导出
/// </summary>
public class GitTranslationService(
    ILogger<GitTranslationService> logger,
    IFileSystem fileSystem)
{
    private readonly ILogger<GitTranslationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// 导出翻译到 Git 仓库
    /// </summary>
    public async Task<OperationResult> ExportTranslationsToGitRepoAsync(
        string modDirectory,
        string repoPath,
        IModRepository modRepo,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("开始导出翻译到 Git 仓库: {RepoPath}", repoPath);

        var allMods = (await modRepo.GetAllModsAsync(cancellationToken)).ToList();

        // 确保仓库 Mods 文件夹存在
        var repoModsPath = _fileSystem.Combine(repoPath, "Mods");
        if (!_fileSystem.DirectoryExists(repoModsPath))
        {
            _fileSystem.CreateDirectory(repoModsPath);
        }

        var successCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行导出所有文件
        var tasks = allMods.Select(mod => ExportModToGitRepoAsync(mod, modDirectory, repoPath, cancellationToken)).ToArray();
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

        sw.Stop();
        _logger.LogInformation("导出完成 ({Elapsed}ms): 成功 {SuccessCount}, 失败 {ErrorCount}", sw.ElapsedMilliseconds, successCount, errorCount);

        return CreateOperationResult(successCount, errorCount, errors, "导出");
    }

    /// <summary>
    /// 从 Git 仓库读取翻译并更新数据库（用于回滚后同步）
    /// </summary>
    public async Task<OperationResult> ImportTranslationsFromGitRepoAsync(
        string repoPath,
        IModRepository modRepo,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始从 Git 仓库导入翻译: {RepoPath}", repoPath);

        // Align with Export: Scan from Repo Root
        // var repoModsPath = _fileSystem.Combine(repoPath, "Mods");
        // if (!_fileSystem.DirectoryExists(repoModsPath))
        // {
        //     _logger.LogWarning("Git 仓库 Mods 目录不存在: {RepoModsPath}", repoModsPath);
        //     return OperationResult.Failure("Git 仓库 Mods 目录不存在");
        // }
        
        // Use repoPath directly
        var searchPath = repoPath;

        var modFiles = _fileSystem.GetFiles(searchPath, "manifest.json", SearchOption.AllDirectories);
        _logger.LogInformation("找到 {Count} 个 manifest.json 文件", modFiles.Length);

        var successCount = 0;
        var errorCount = 0;
        var errors = new List<string>();

        // 🔥 性能优化：并行读取和解析所有文件
        var tasks = modFiles.Select(file => ParseModFromGitRepoAsync(file, searchPath, modRepo, cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks);

        // 收集所有成功的 Mod
        var modsToUpdate = results
            .Where(r => r is { success: true, mod: not null })
            .Select(r => r.mod!)
            .ToList();

        // 批量更新数据库
        if (modsToUpdate.Count != 0)
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
            if (success || error == null) continue;
            errorCount++;
            errors.Add(error);
        }

        _logger.LogInformation("导入完成: 成功 {SuccessCount}, 失败 {ErrorCount}", successCount, errorCount);

        return CreateOperationResult(successCount, errorCount, errors, "导入");
    }

    /// <summary>
    /// 导出单个模组到 Git 仓库
    /// </summary>
    private async Task<(bool success, string? error)> ExportModToGitRepoAsync(
        ModMetadata mod,
        string modDirectory,
        string repoPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(mod.RelativePath))
        {
            _logger.LogWarning("跳过导出 [{UniqueId}]: RelativePath 为空", mod.UniqueID);
            return (true, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var sourcePath = _fileSystem.Combine(modDirectory, mod.RelativePath);
            if (!_fileSystem.FileExists(sourcePath))
            {
                return (true, null); // 模组可能已删除
            }

            // 读取源文件
            var json = await _fileSystem.ReadAllTextAsync(sourcePath, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);
            if (manifest == null)
            {
                return (false, $"无法解析: {mod.RelativePath}");
            }

            // 应用数据库中的翻译
            ApplyTranslationsToManifest(manifest, mod);

            // LOGGING DEBUG: Check what we are about to write
            _logger.LogDebug("准备导出 [{UniqueId}]: Name='{Name}'", mod.UniqueID, manifest.Name);

            // 写入到 Git 仓库
            await WriteManifestToGitRepoAsync(manifest, mod.RelativePath, repoPath, cancellationToken);

            _logger.LogDebug("导出翻译: {UniqueId}", mod.UniqueID);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出翻译失败: {RelativePath}", mod.RelativePath);
            return (false, $"{mod.RelativePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 Git 仓库解析单个模组
    /// </summary>
    private async Task<(bool success, string? error, ModMetadata? mod)> ParseModFromGitRepoAsync(
        string file,
        string repoModsPath,
        IModRepository modRepo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(file, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
            {
                return (false, $"无效的 manifest: {_fileSystem.GetFileName(file)}", null);
            }

            var mod = await modRepo.GetModAsync(manifest.UniqueID, cancellationToken) ?? new ModMetadata
            {
                UniqueID = manifest.UniqueID,
                RelativePath = _fileSystem.GetRelativePath(repoModsPath, file)
            };

            // 更新翻译数据
            UpdateModFromGitManifest(mod, manifest);

            return (true, null, mod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取文件失败: {File}", file);
            return (false, $"{_fileSystem.GetFileName(file)}: {ex.Message}", null);
        }
    }

    /// <summary>
    /// 应用翻译到 manifest 对象
    /// </summary>
    private void ApplyTranslationsToManifest(ModManifest manifest, ModMetadata mod)
    {
        if (!string.IsNullOrEmpty(mod.TranslatedName))
        {
            manifest.Name = mod.TranslatedName;
        }

        if (!string.IsNullOrEmpty(mod.TranslatedDescription))
        {
            manifest.Description = mod.TranslatedDescription;
        }
    }

    /// <summary>
    /// 从 Git manifest 更新模组数据
    /// </summary>
    private void UpdateModFromGitManifest(ModMetadata mod, ModManifest manifest)
    {
        mod.TranslatedName = manifest.Name;
        mod.TranslatedDescription = manifest.Description;
        mod.LastTranslationUpdate = DateTime.Now;
    }

    /// <summary>
    /// 写入 manifest 到 Git 仓库
    /// </summary>
    private async Task WriteManifestToGitRepoAsync(
        ModManifest manifest,
        string relativePath,
        string repoPath,
        CancellationToken cancellationToken)
    {
        var targetPath = _fileSystem.Combine(repoPath, relativePath);
        var targetDir = _fileSystem.GetDirectoryName(targetPath);

        if (!string.IsNullOrEmpty(targetDir) && !_fileSystem.DirectoryExists(targetDir))
        {
            _fileSystem.CreateDirectory(targetDir);
        }

        var outputJson = JsonConvert.SerializeObject(manifest, _jsonSettings);

        // 如果文件存在，检查内容是否变更
        if (_fileSystem.FileExists(targetPath))
        {
            var existingJson = await _fileSystem.ReadAllTextAsync(targetPath, cancellationToken);
            if (string.Equals(existingJson, outputJson, StringComparison.Ordinal))
            {
                // 内容一致，无需写入
                // DEBUG: 即使一致也记录一下，确认我们检查了这个文件
                // _logger.LogDebug("文件内容未变更，跳过写入: {RelativePath}", relativePath);
                return;
            }
            else
            {
                 // DEBUG: 记录差异
                 _logger.LogInformation("检测到文件内容差异: {RelativePath}", relativePath);
                 _logger.LogDebug("旧内容长度: {OldLen}, 新内容长度: {NewLen}", existingJson.Length, outputJson.Length);
            }
        }
        else
        {
             _logger.LogInformation("创建新文件: {RelativePath}", relativePath);
        }

        await _fileSystem.WriteAllTextAsync(targetPath, outputJson, cancellationToken);
        _logger.LogDebug("已写入文件: {RelativePath}", relativePath);
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

