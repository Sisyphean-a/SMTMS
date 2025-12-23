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
        var swTotal = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("[开始] 保存翻译到数据库: {ModDirectory}", modDirectory);

        // 1. 验证目录
        var validationResult = ValidateModDirectory(modDirectory);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // 2. 扫描文件
        var (modFiles, scanElapsed) = await ScanManifestFilesAsync(modDirectory);

        // 3. 计算文件 Hash
        var (fileHashes, hashElapsed) = await ComputeFileHashesWithTimingAsync(modFiles, cancellationToken);

        // 4. 加载数据库数据
        var (keyPathMap, keyIdMap, dbLoadElapsed) = await LoadDatabaseModsAsync(modRepo, cancellationToken);

        // 5. 处理文件
        var processResult = await ProcessManifestFilesAsync(
            modDirectory,
            fileHashes,
            keyPathMap,
            keyIdMap,
            cancellationToken);

        // 6. 批量保存
        var saveElapsed = await SaveModsToDbAsync(modRepo, processResult.ModsToUpsert, cancellationToken);

        // 7. 记录总结
        swTotal.Stop();
        LogProcessingSummary(swTotal.ElapsedMilliseconds, scanElapsed, hashElapsed, dbLoadElapsed,
            processResult.ProcessElapsed, processResult.ParseCount, saveElapsed, 
            processResult.ModsToUpsert.Count, processResult.SkipCount);

        return CreateOperationResult(processResult.SuccessCount, processResult.ErrorCount, 
            processResult.Errors, "保存");
    }

    /// <summary>
    /// 验证模组目录是否存在
    /// </summary>
    private OperationResult ValidateModDirectory(string modDirectory)
    {
        if (_fileSystem.DirectoryExists(modDirectory)) return OperationResult.Success(0, string.Empty);
        _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
        return OperationResult.Failure("模组目录不存在");
    }

    /// <summary>
    /// 扫描 manifest.json 文件
    /// </summary>
    private async Task<(string[] files, long elapsed)> ScanManifestFilesAsync(string modDirectory)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var modFiles = _fileSystem.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        sw.Stop();
        
        _logger.LogInformation("扫描文件完成 ({Elapsed}ms): 找到 {Count} 个 manifest.json 文件", 
            sw.ElapsedMilliseconds, modFiles.Length);
        
        return await Task.FromResult((modFiles, sw.ElapsedMilliseconds));
    }

    /// <summary>
    /// 计算文件 Hash（带计时）
    /// </summary>
    private async Task<((string file, string hash, bool success)[] hashes, long elapsed)> 
        ComputeFileHashesWithTimingAsync(string[] files, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileHashes = await ComputeFileHashesAsync(files, cancellationToken);
        sw.Stop();
        
        _logger.LogInformation("计算Hash完成 ({Elapsed}ms)", sw.ElapsedMilliseconds);
        
        return (fileHashes, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// 加载数据库中的模组数据并构建查找字典
    /// </summary>
    private async Task<(
        Dictionary<string, ModMetadata> keyPathMap,
        Dictionary<string, ModMetadata> keyIdMap,
        long elapsed)> LoadDatabaseModsAsync(IModRepository modRepo, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var allDbMods = await modRepo.GetAllModsAsync(cancellationToken);
        
        var modMetadatas = allDbMods.ToList();
        
        // keyPathMap: RelativePath -> Mod (用于快速检测内容是否未变)
        var keyPathMap = modMetadatas
            .Where(m => !string.IsNullOrEmpty(m.RelativePath))
            .ToDictionary(m => m.RelativePath!.Replace('\\', '/'), m => m, StringComparer.OrdinalIgnoreCase);
        
        // keyIdMap: UniqueID -> Mod (用于处理移动文件或重命名的情况)
        var keyIdMap = modMetadatas.ToDictionary(m => m.UniqueID, m => m);
        
        sw.Stop();
        _logger.LogInformation("数据库加载完成 ({Elapsed}ms): 已加载 {Count} 个 Mod", 
            sw.ElapsedMilliseconds, modMetadatas.Count);
        
        return (keyPathMap, keyIdMap, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// 处理所有 manifest 文件
    /// </summary>
    private async Task<ProcessResult> ProcessManifestFilesAsync(
        string modDirectory,
        (string file, string hash, bool success)[] fileHashes,
        Dictionary<string, ModMetadata> keyPathMap,
        Dictionary<string, ModMetadata> keyIdMap,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ProcessResult();

        foreach (var (file, hash, success) in fileHashes)
        {
            if (!success)
            {
                result.ErrorCount++;
                result.Errors.Add($"无法读取文件: {_fileSystem.GetFileName(file)}");
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = _fileSystem.GetRelativePath(modDirectory, file).Replace('\\', '/');

            // ⚡ 优化核心：如果路径匹配且Hash一致，直接跳过
            if (ShouldSkipFile(relativePath, hash, keyPathMap))
            {
                result.SkipCount++;
                continue;
            }

            // 处理单个文件
            result.ParseCount++;
            await ProcessSingleManifestAsync(file, hash, relativePath, keyIdMap, result, cancellationToken);
        }

        sw.Stop();
        result.ProcessElapsed = sw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// 判断是否应该跳过文件处理
    /// </summary>
    private static bool ShouldSkipFile(
        string relativePath, 
        string hash, 
        Dictionary<string, ModMetadata> keyPathMap)
    {
        if (keyPathMap.TryGetValue(relativePath, out var existingModByPath))
        {
            return existingModByPath.LastFileHash == hash;
        }
        return false;
    }

    /// <summary>
    /// 处理单个 manifest 文件
    /// </summary>
    private async Task ProcessSingleManifestAsync(
        string file,
        string hash,
        string relativePath,
        Dictionary<string, ModMetadata> keyIdMap,
        ProcessResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _fileSystem.ReadAllTextAsync(file, cancellationToken);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
            {
                _logger.LogWarning("跳过无效的 manifest: {File}", file);
                return;
            }

            var mod = GetOrCreateMod(manifest.UniqueID, keyIdMap);
            UpdateModPath(mod, relativePath);

            var changes = GetModChanges(mod, manifest);
            if (ShouldUpdateMod(mod, hash, changes))
            {
                ApplyChangesToMod(mod, manifest);
                mod.LastTranslationUpdate = DateTime.Now;
                mod.LastFileHash = hash;
                
                result.ModsToUpsert.Add(mod);
                LogModChanges(manifest.UniqueID, changes);
                result.SuccessCount++;
            }
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            _logger.LogError(ex, "处理失败: {File}", file);
            result.Errors.Add($"{_fileSystem.GetFileName(file)}: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取或创建模组对象
    /// </summary>
    private static ModMetadata GetOrCreateMod(string uniqueId, Dictionary<string, ModMetadata> keyIdMap)
    {
        if (keyIdMap.TryGetValue(uniqueId, out var existingMod))
        {
            return existingMod;
        }

        return new ModMetadata { UniqueID = uniqueId };
    }

    /// <summary>
    /// 更新模组路径（如果移动了）
    /// </summary>
    private static void UpdateModPath(ModMetadata mod, string relativePath)
    {
        if (string.Equals(mod.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) return;
        mod.RelativePath = relativePath;
        mod.LastFileHash = string.Empty; // 强制更新
    }

    /// <summary>
    /// 判断是否应该更新模组
    /// </summary>
    private static bool ShouldUpdateMod(ModMetadata mod, string hash, List<string> changes)
    {
        return changes.Count > 0 || mod.LastTranslationUpdate == null || mod.LastFileHash != hash;
    }

    /// <summary>
    /// 记录模组变更日志
    /// </summary>
    private void LogModChanges(string uniqueId, List<string> changes)
    {
        foreach (var change in changes)
        {
            _logger.LogInformation("变更 [{UniqueId}]: {Change}", uniqueId, change);
        }
    }

    /// <summary>
    /// 批量保存模组到数据库
    /// </summary>
    private async Task<long> SaveModsToDbAsync(
        IModRepository modRepo,
        List<ModMetadata> modsToUpsert,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (modsToUpsert.Count > 0)
        {
            await modRepo.UpsertModsAsync(modsToUpsert, cancellationToken);
        }
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// 记录处理总结日志
    /// </summary>
    private void LogProcessingSummary(
        long totalMs, long scanMs, long hashMs, long dbLoadMs,
        long processMs, int parseCount, long dbSaveMs, int savedCount, int skipCount)
    {
        _logger.LogInformation(
            "✅ 同步完成 Total: {Total}ms | Scan: {Scan}ms | Hash: {Hash}ms | DB Load: {DbLoad}ms | " +
            "Process: {Process}ms (Parsed: {Parsed}) | DB Save: {DbSave}ms (Saved: {Saved}) | Skipped: {Skipped}",
            totalMs, scanMs, hashMs, dbLoadMs, processMs, parseCount, dbSaveMs, savedCount, skipCount);
    }

    /// <summary>
    /// 处理结果数据类
    /// </summary>
    private class ProcessResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkipCount { get; set; }
        public int ParseCount { get; set; }
        public long ProcessElapsed { get; set; }
        public List<string> Errors { get; } = new();
        public List<ModMetadata> ModsToUpsert { get; } = new();
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

