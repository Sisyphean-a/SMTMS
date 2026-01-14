using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SMTMS.Core.Common;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Translation.Services;

/// <summary>
/// 翻译服务协调器 - 负责协调各个专门的翻译服务
/// </summary>
public class TranslationService(
    IServiceScopeFactory scopeFactory,
    LegacyImportService legacyImportService,
    TranslationScanService scanService,
    TranslationRestoreService restoreService)
    : ITranslationService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly LegacyImportService _legacyImportService = legacyImportService ?? throw new ArgumentNullException(nameof(legacyImportService));
    private readonly TranslationScanService _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
    private readonly TranslationRestoreService _restoreService = restoreService ?? throw new ArgumentNullException(nameof(restoreService));

    /// <summary>
    /// 从旧版 JSON 文件导入翻译数据
    /// </summary>
    public async Task<OperationResult> ImportFromLegacyJsonAsync(
        string jsonPath,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        return await _legacyImportService.ImportFromLegacyJsonAsync(jsonPath, modRepo, cancellationToken);
    }

    /// <summary>
    /// 扫描 manifest.json 文件并保存翻译到数据库（自动创建快照）
    /// </summary>
    public async Task<OperationResult> SaveTranslationsToDbAsync(
        string modDirectory,
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        
        return await _scanService.SaveTranslationsToDbAsync(modDirectory, modRepo, historyRepo, commitMessage, cancellationToken);
    }

    /// <summary>
    /// 从数据库恢复翻译到 manifest.json 文件
    /// </summary>
    public async Task<OperationResult> RestoreTranslationsFromDbAsync(
        string modDirectory,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        return await _restoreService.RestoreTranslationsFromDbAsync(modDirectory, modRepo, cancellationToken);
    }

    /// <summary>
    /// 回滚到指定的快照
    /// </summary>
    public async Task<OperationResult> RollbackSnapshotAsync(
        int snapshotId,
        string modDirectory,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();

        // 1. 获取该 Snapshot 时刻所有 Mod 的最新状态
        // GetModHistoriesForSnapshotAsync 应该返回每个 Mod 在该时间点（或之前）的最新状态
        var histories = await historyRepo.GetModHistoriesForSnapshotAsync(snapshotId, cancellationToken);

        if (histories.Count == 0)
        {
             return OperationResult.Failure("未找到该快照的历史记录或历史记录为空。");
        }

        var modsToUpdate = new List<ModMetadata>();
        var errors = new List<string>();
        int successCount = 0;

        foreach(var h in histories)
        {
            try
            {
                var mod = await modRepo.GetModAsync(h.ModUniqueId, cancellationToken);
                if (mod != null)
                {
                    // 恢复元数据
                    mod.CurrentJson = h.JsonContent;
                    mod.LastFileHash = h.PreviousHash;
                    mod.LastTranslationUpdate = DateTime.Now; // 标记为刚刚更新，或者应该保持历史时间？一般回滚视为一次新的变更

                    // 解析 JsonContent 以更新 TranslatedName 和 TranslatedDescription
                    if (!string.IsNullOrEmpty(h.JsonContent))
                    {
                        try
                        {
                            var manifest = JsonConvert.DeserializeObject<ModManifest>(h.JsonContent);
                            if (manifest != null)
                            {
                                mod.TranslatedName = manifest.Name;
                                mod.TranslatedDescription = manifest.Description;
                            }
                        }
                        catch
                        {
                            // 忽略解析错误，只恢复内容
                        }
                    }

                    modsToUpdate.Add(mod);
                    successCount++;
                }
                else
                {
                    // 如果 Mod 不存在于当前 DB（可能被删除了），我们要重新创建吗？
                    // 这是一个策略问题。简单起见，如果 DB 里没有这个 Mod（意味着可能也没有对应的文件夹），我们可能无法恢复文件。
                    // 但如果文件夹还在，只是 DB 记录丢了，我们应该 Upsert。
                    // 鉴于 Metadata 表通常由 Scan 填充，如果这里新建一个 ModMetadata，可能缺少 Path 信息。
                    // 历史记录里有 Path 吗？ ModMetadata 包含 RelativePath。
                    // ModTranslationHistory 关联了 ModMetadata，但如果 ModMetadata 没了，我们需要看 GetModHistoriesForSnapshotAsync 是怎么返回的。
                    // 如果 ModMetadata 是 null，我们丢失了 Path 信息，无法写入文件。
                    // 假设用户没有删除 Mod 文件夹，只是想回滚翻译。
                    errors.Add($"Mod {h.ModUniqueId} 在当前数据库中未找到，跳过恢复。");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"处理 Mod {h.ModUniqueId} 失败: {ex.Message}");
            }
        }

        // 2. 批量更新 ModMetadata
        if (modsToUpdate.Count > 0)
        {
            await modRepo.UpsertModsAsync(modsToUpdate, cancellationToken);
        }

        // 3. 写入文件系统 (复用 RestoreService)
        var restoreResult = await _restoreService.RestoreTranslationsFromDbAsync(modDirectory, modRepo, cancellationToken);

        if (!restoreResult.IsSuccess)
        {
             return OperationResult.Failure(restoreResult.ErrorCount, "数据库更新成功，但文件恢复失败: " + restoreResult.Message, restoreResult.Details);
        }

        return OperationResult.Success(successCount, $"成功回滚 {successCount} 个模组到快照 {snapshotId}。");
    }
}
