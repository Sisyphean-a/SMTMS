using Microsoft.Extensions.DependencyInjection;
using SMTMS.Core.Common;
using SMTMS.Core.Interfaces;

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
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        var historyRepo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        
        return await _scanService.SaveTranslationsToDbAsync(modDirectory, modRepo, historyRepo, cancellationToken);
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
}