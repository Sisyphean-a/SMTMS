using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using System.Text;

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
        string? commitMessage = null,
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

        // 3. 计算文件 Hash 并读取内容
        var (fileHashes, hashElapsed) = await ComputeFileHashesWithTimingAsync(modFiles, cancellationToken);

        // 4. 加载数据库数据
        var (keyPathMap, keyIdMap, dbLoadElapsed) = await LoadDatabaseModsAsync(modRepo, cancellationToken);

        // 5. 处理文件 (生成 ModsUpsert 和 HistoryInsert)
        // 暂时使用 0 作为 snapshotId，稍后如果有需要我们会分配真正的 ID
        var processResult = ProcessManifestFilesAsync(
            modDirectory,
            fileHashes,
            keyPathMap,
            keyIdMap,
            0, 
            cancellationToken);

        // 检查是否需要保存任何内容
        long saveModsElapsed = 0;
        long saveHistoryElapsed = 0;
        int snapshotId;

        if (processResult.HistoriesToInsert.Count > 0 || processResult.ModsToUpsert.Count > 0)
        {
            // 6. 只有存在变更时，才创建快照
            var finalMessage = !string.IsNullOrWhiteSpace(commitMessage) 
                ? commitMessage 
                : $"Sync {modFiles.Length} mods";
            
            snapshotId = await historyRepo.CreateSnapshotAsync(finalMessage, modFiles.Length, cancellationToken);
            
            // 回填 SnapshotId
            foreach (var history in processResult.HistoriesToInsert)
            {
                history.SnapshotId = snapshotId;
            }

            // 7. 批量保存 Mods
            saveModsElapsed = await SaveModsToDbAsync(modRepo, processResult.ModsToUpsert, cancellationToken);
        
            // 8. 批量保存 History
            if (processResult.HistoriesToInsert.Count > 0)
            {
                var swHistory = System.Diagnostics.Stopwatch.StartNew();
                await historyRepo.SaveModHistoriesAsync(processResult.HistoriesToInsert, cancellationToken);
                swHistory.Stop();
                saveHistoryElapsed = swHistory.ElapsedMilliseconds;
                _logger.LogInformation("已保存 {Count} 条历史记录 (Snapshot {SnapshotId})", processResult.HistoriesToInsert.Count, snapshotId);
            }
        }
        else
        {
            _logger.LogInformation("没有检测到任何变更，跳过保存和快照生成。");
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
        
        // 与 ModService 逻辑匹配：仅扫描一级子目录下的 manifest.json
        // 这确保了你在 UI (ModService) 中看到的内容与同步到数据库 (ScanService) 的内容一致
        string[] modFiles = [];
        
        if (_fileSystem.DirectoryExists(modDirectory))
        {
            var subDirectories = _fileSystem.GetDirectories(modDirectory);
            modFiles = subDirectories
                .Select(dir => _fileSystem.Combine(dir, "manifest.json"))
                .Where(path => _fileSystem.FileExists(path))
                .ToArray();
        }

        sw.Stop();
        
        _logger.LogInformation("扫描文件完成 ({Elapsed}ms): 找到 {Count} 个 manifest.json 文件 (Shallow Scan)", 
            sw.ElapsedMilliseconds, modFiles.Length);
        
        return await Task.FromResult((modFiles, sw.ElapsedMilliseconds));
    }

    private async Task<((string file, string hash, string? content, bool success)[] hashes, long elapsed)>
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

    private ProcessResult ProcessManifestFilesAsync(
        string modDirectory,
        (string file, string hash, string? content, bool success)[] fileHashes,
        Dictionary<string, ModMetadata> keyPathMap,
        Dictionary<string, ModMetadata> keyIdMap,
        int snapshotId,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ProcessResult();

        foreach (var (file, hash, content, success) in fileHashes)
        {
            if (!success || content == null)
            {
                result.ErrorCount++;
                result.Errors.Add($"无法读取文件: {_fileSystem.GetFileName(file)}");
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = _fileSystem.GetRelativePath(modDirectory, file).Replace('\\', '/');
            
            // 检查是否可以跳过（仅基于 Hash 比对）
            if (ShouldSkipFile(relativePath, hash, keyPathMap))
            {
                result.SkipCount++;
                continue;
            }

            result.ParseCount++;

            ProcessSingleManifestAsync(file, hash, content, relativePath, keyIdMap, snapshotId, result);
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

    private void ProcessSingleManifestAsync(
        string file,
        string hash,
        string jsonContent,
        string relativePath,
        Dictionary<string, ModMetadata> keyIdMap,
        int snapshotId,
        ProcessResult result)
    {
        try
        {
            var json = jsonContent;

            var manifest = JsonConvert.DeserializeObject<ModManifest>(json);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.UniqueID))
            {
                _logger.LogWarning("跳过无效的 manifest: {File}", file);
                return;
            }

            var mod = GetOrCreateMod(manifest.UniqueID, keyIdMap);
            
            // 处理新 Mod - 设置 OriginalJson
            if (string.IsNullOrEmpty(mod.OriginalJson))
            {
                mod.OriginalJson = json;
            }

            UpdateModPath(mod, relativePath);

            // 如果能执行到这里，说明一定需要更新 CurrentJson（因为没通过 ShouldSkipFile 检查）
            mod.CurrentJson = json;
            
            var changes = GetModChanges(mod, manifest);
            
            // 只要在处理中，就始终更新元数据
            ApplyChangesToMod(mod, manifest);
            mod.LastTranslationUpdate = DateTime.Now;
            mod.LastFileHash = hash;
            
            result.ModsToUpsert.Add(mod);
            if (changes.Count > 0) LogModChanges(manifest.UniqueID, changes);
            result.SuccessCount++;
            
            // 创建历史记录（增量）
            // 因为文件已更改（ShouldSkipFile 捕获到 Hash 不匹配），所以保存一条历史记录
            var history = new ModTranslationHistory
            {
                SnapshotId = snapshotId,
                ModUniqueId = mod.UniqueID,
                JsonContent = json,
                PreviousHash = hash // 使用当前 Hash 作为此历史记录的“特征指纹”
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
        mod.LastFileHash = string.Empty; // 强制更新
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

    private async Task<(string file, string hash, string? content, bool success)[]> ComputeFileHashesAsync(
        string[] files,
        CancellationToken cancellationToken)
    {
        var fileHashTasks = files.Select(async file =>
        {
            try
            {
                var contentBytes = await _fileSystem.ReadAllBytesAsync(file, cancellationToken);
                var hash = Convert.ToBase64String(MD5.HashData(contentBytes));

                // Optimization: Decode content here to avoid reading file again later
                // Assuming manifest.json is UTF8
                var contentString = Encoding.UTF8.GetString(contentBytes);

                return (file, hash, (string?)contentString, true);
            }
            catch
            {
                return (file, string.Empty, (string?)null, false);
            }
        }).ToArray();
        return await Task.WhenAll(fileHashTasks);
    }
}
