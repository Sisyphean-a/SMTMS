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
        IHistoryRepository historyRepo,
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

        // 3. 创建快照 (即使没有变更也创建，作为检查点)
        // 使用 "手动同步" 或自动生成的消息
        var snapshotId = await historyRepo.CreateSnapshotAsync($"Sync {modFiles.Length} mods", modFiles.Length, cancellationToken);

        // 4. 计算文件 Hash
        var (fileHashes, hashElapsed) = await ComputeFileHashesWithTimingAsync(modFiles, cancellationToken);

        // 5. 加载数据库数据
        var (keyPathMap, keyIdMap, dbLoadElapsed) = await LoadDatabaseModsAsync(modRepo, cancellationToken);

        // 6. 处理文件 (生成 ModsUpsert 和 HistoryInsert)
        var processResult = await ProcessManifestFilesAsync(
            modDirectory,
            fileHashes,
            keyPathMap,
            keyIdMap,
            snapshotId,
            cancellationToken);

        // 7. 批量保存 Mods
        var saveModsElapsed = await SaveModsToDbAsync(modRepo, processResult.ModsToUpsert, cancellationToken);
        
        // 8. 批量保存 History (如有变更)
        long saveHistoryElapsed = 0;
        if (processResult.HistoriesToInsert.Count > 0)
        {
            var swHistory = System.Diagnostics.Stopwatch.StartNew();
            await historyRepo.SaveModHistoriesAsync(processResult.HistoriesToInsert, cancellationToken);
            swHistory.Stop();
            saveHistoryElapsed = swHistory.ElapsedMilliseconds;
            _logger.LogInformation("已保存 {Count} 条历史记录", processResult.HistoriesToInsert.Count);
        }

        // 9. 记录总结
        swTotal.Stop();
        LogProcessingSummary(swTotal.ElapsedMilliseconds, scanElapsed, hashElapsed, dbLoadElapsed,
            processResult.ProcessElapsed, processResult.ParseCount, saveModsElapsed, 
            processResult.ModsToUpsert.Count, processResult.SkipCount, saveHistoryElapsed);

        return CreateOperationResult(processResult.SuccessCount, processResult.ErrorCount, 
            processResult.Errors, "保存");
    }

    private OperationResult ValidateModDirectory(string modDirectory)
    {
        if (_fileSystem.DirectoryExists(modDirectory)) return OperationResult.Success(0, string.Empty);
        _logger.LogWarning("模组目录不存在: {ModDirectory}", modDirectory);
        return OperationResult.Failure("模组目录不存在");
    }

    private async Task<(string[] files, long elapsed)> ScanManifestFilesAsync(string modDirectory)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var modFiles = _fileSystem.GetFiles(modDirectory, "manifest.json", SearchOption.AllDirectories);
        sw.Stop();
        
        _logger.LogInformation("扫描文件完成 ({Elapsed}ms): 找到 {Count} 个 manifest.json 文件", 
            sw.ElapsedMilliseconds, modFiles.Length);
        
        return await Task.FromResult((modFiles, sw.ElapsedMilliseconds));
    }

    private async Task<((string file, string hash, bool success)[] hashes, long elapsed)> 
        ComputeFileHashesWithTimingAsync(string[] files, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileHashes = await ComputeFileHashesAsync(files, cancellationToken);
        sw.Stop();
        _logger.LogInformation("计算Hash完成 ({Elapsed}ms)", sw.ElapsedMilliseconds);
        return (fileHashes, sw.ElapsedMilliseconds);
    }

    private async Task<(
        Dictionary<string, ModMetadata> keyPathMap,
        Dictionary<string, ModMetadata> keyIdMap,
        long elapsed)> LoadDatabaseModsAsync(IModRepository modRepo, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var allDbMods = await modRepo.GetAllModsAsync(cancellationToken);
        
        var modMetadatas = allDbMods.ToList();
        
        var keyPathMap = modMetadatas
            .Where(m => !string.IsNullOrEmpty(m.RelativePath))
            .ToDictionary(m => m.RelativePath!.Replace('\\', '/'), m => m, StringComparer.OrdinalIgnoreCase);
        
        var keyIdMap = modMetadatas.ToDictionary(m => m.UniqueID, m => m);
        
        sw.Stop();
        return (keyPathMap, keyIdMap, sw.ElapsedMilliseconds);
    }

    private async Task<ProcessResult> ProcessManifestFilesAsync(
        string modDirectory,
        (string file, string hash, bool success)[] fileHashes,
        Dictionary<string, ModMetadata> keyPathMap,
        Dictionary<string, ModMetadata> keyIdMap,
        int snapshotId,
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
            
            // Check if skip is possible (Only if EXACT match)
            // But we need to be careful: if we want to ensure history is correct, 
            // we rely on ModMetadata.LastFileHash being accurate.
            if (ShouldSkipFile(relativePath, hash, keyPathMap))
            {
                result.SkipCount++;
                continue;
            }

            result.ParseCount++;
            await ProcessSingleManifestAsync(file, hash, relativePath, keyIdMap, snapshotId, result, cancellationToken);
        }

        sw.Stop();
        result.ProcessElapsed = sw.ElapsedMilliseconds;
        return result;
    }

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

    private async Task ProcessSingleManifestAsync(
        string file,
        string hash,
        string relativePath,
        Dictionary<string, ModMetadata> keyIdMap,
        int snapshotId,
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
            
            // Handle New Mod - Set OriginalJson
            if (string.IsNullOrEmpty(mod.OriginalJson))
            {
                mod.OriginalJson = json;
            }

            UpdateModPath(mod, relativePath);

            // Always update CurrentJson if we are here (because we passed ShouldSkipFile check)
            mod.CurrentJson = json;
            
            var changes = GetModChanges(mod, manifest);
            
            // Always update metadata if we are processing
            ApplyChangesToMod(mod, manifest);
            mod.LastTranslationUpdate = DateTime.Now;
            mod.LastFileHash = hash;
            
            result.ModsToUpsert.Add(mod);
            if (changes.Count > 0) LogModChanges(manifest.UniqueID, changes);
            result.SuccessCount++;
            
            // Create History Record (Incremental)
            // We save a history record because the file has changed (hash mismatch caught by ShouldSkipFile)
            var history = new ModTranslationHistory
            {
                SnapshotId = snapshotId,
                ModUniqueId = mod.UniqueID,
                JsonContent = json,
                PreviousHash = hash // Using current hash as the "Signature" of this history
            };
            result.HistoriesToInsert.Add(history);
        }
        catch (Exception ex)
        {
            result.ErrorCount++;
            _logger.LogError(ex, "处理失败: {File}", file);
            result.Errors.Add($"{_fileSystem.GetFileName(file)}: {ex.Message}");
        }
    }

    private static ModMetadata GetOrCreateMod(string uniqueId, Dictionary<string, ModMetadata> keyIdMap)
    {
        if (keyIdMap.TryGetValue(uniqueId, out var existingMod))
        {
            return existingMod;
        }
        return new ModMetadata { UniqueID = uniqueId };
    }

    private static void UpdateModPath(ModMetadata mod, string relativePath)
    {
        if (string.Equals(mod.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)) return;
        mod.RelativePath = relativePath;
        mod.LastFileHash = string.Empty; // Force update
    }

    private List<string> GetModChanges(ModMetadata mod, ModManifest manifest)
    {
        var changes = new List<string>();
        if (mod.TranslatedName != manifest.Name) changes.Add($"名称: '{mod.TranslatedName}' -> '{manifest.Name}'");
        if (mod.TranslatedDescription != manifest.Description) changes.Add("描述已更新");
        return changes;
    }

    private void ApplyChangesToMod(ModMetadata mod, ModManifest manifest)
    {
        mod.TranslatedName = manifest.Name;
        mod.TranslatedDescription = manifest.Description;
    }

    private void LogModChanges(string uniqueId, List<string> changes)
    {
        _logger.LogInformation("模组 {UniqueId} 变更: {Changes}", uniqueId, string.Join(", ", changes));
    }

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

    private void LogProcessingSummary(
        long totalMs, long scanMs, long hashMs, long dbLoadMs,
        long processMs, int parseCount, long dbSaveMs, int savedCount, int skipCount, long objHistoryMs)
    {
        _logger.LogInformation(
            "✅ 同步完成 Total: {Total}ms | Scan: {Scan}ms | Hash: {Hash}ms | DB Load: {DbLoad}ms | " +
            "Process: {Process}ms (Parsed: {Parsed}) | Mods Save: {DbSave}ms ({Saved}) | Hist Save: {Hist}ms | Skipped: {Skipped}",
            totalMs, scanMs, hashMs, dbLoadMs, processMs, parseCount, dbSaveMs, savedCount, objHistoryMs, skipCount);
    }

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

    private class ProcessResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int SkipCount { get; set; }
        public int ParseCount { get; set; }
        public long ProcessElapsed { get; set; }
        public List<string> Errors { get; } = new();
        public List<ModMetadata> ModsToUpsert { get; } = new();
        public List<ModTranslationHistory> HistoriesToInsert { get; } = new();
    }

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
}
