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
    TranslationRestoreService restoreService,
    GitTranslationService gitService)
    : ITranslationService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly LegacyImportService _legacyImportService = legacyImportService ?? throw new ArgumentNullException(nameof(legacyImportService));
    private readonly TranslationScanService _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
    private readonly TranslationRestoreService _restoreService = restoreService ?? throw new ArgumentNullException(nameof(restoreService));
    private readonly GitTranslationService _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));

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
    /// 扫描 manifest.json 文件并保存翻译到数据库
    /// </summary>
    public async Task<OperationResult> SaveTranslationsToDbAsync(
        string modDirectory,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        return await _scanService.SaveTranslationsToDbAsync(modDirectory, modRepo, cancellationToken);
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
    /// 导出翻译到 Git 仓库
    /// </summary>
    public async Task<OperationResult> ExportTranslationsToGitRepo(
        string modDirectory,
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        return await _gitService.ExportTranslationsToGitRepoAsync(modDirectory, repoPath, modRepo, cancellationToken);
    }

    /// <summary>
    /// 从 Git 仓库读取翻译并更新数据库（用于回滚后同步）
    /// </summary>
    public async Task<OperationResult> ImportTranslationsFromGitRepoAsync(
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var modRepo = scope.ServiceProvider.GetRequiredService<IModRepository>();
        return await _gitService.ImportTranslationsFromGitRepoAsync(repoPath, modRepo, cancellationToken);
    }
}