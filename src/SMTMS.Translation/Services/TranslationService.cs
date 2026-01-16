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

        // 1. 获取快照历史并预处理匹配记录
        var histories = await historyRepo.GetModHistoriesForSnapshotAsync(snapshotId, cancellationToken);
        if (histories.Count == 0) return OperationResult.Failure("未找到该快照的历史记录或历史记录为空。");

        var currentMods = await modRepo.GetModsByIdsAsync(histories.Select(h => h.ModUniqueId).Distinct(), cancellationToken);
        var modsToUpdate = new List<ModMetadata>();
        var now = DateTime.Now;

        // 2. 将历史状态应用到当前 Mod 对象
        foreach (var h in histories)
        {
            if (currentMods.TryGetValue(h.ModUniqueId, out var mod))
            {
                ApplyHistoryToMod(mod, h, now);
                modsToUpdate.Add(mod);
            }
        }

        // 3. 执行数据库批量更新
        if (modsToUpdate.Count > 0) await modRepo.UpsertModsAsync(modsToUpdate, cancellationToken);

        // 4. 同步文件系统并返回最终结果
        var result = await _restoreService.RestoreTranslationsFromDbAsync(modDirectory, modRepo, cancellationToken);
        
        return result.IsSuccess 
            ? OperationResult.Success(modsToUpdate.Count, $"成功将 {modsToUpdate.Count} 个模组回滚到快照 {snapshotId}。")
            : OperationResult.Failure(result.ErrorCount, "数据库更新完毕，但文件同步失败: " + result.Message, result.Details);
    }

    /// <summary>
    /// 将单条历史记录的状态应用到 Mod 元数据
    /// </summary>
    private static void ApplyHistoryToMod(ModMetadata mod, ModTranslationHistory history, DateTime updateTime)
    {
        mod.CurrentJson = history.JsonContent;
        mod.LastFileHash = history.PreviousHash;
        mod.LastTranslationUpdate = updateTime;

        if (string.IsNullOrEmpty(history.JsonContent)) return;

        try
        {
            var manifest = JsonConvert.DeserializeObject<ModManifest>(history.JsonContent);
            if (manifest != null)
            {
                mod.TranslatedName = manifest.Name;
                mod.TranslatedDescription = manifest.Description;
            }
        }
        catch
        {
            // 忽略损坏的 JSON，确保回滚核心元数据的稳定性
        }
    }
}
