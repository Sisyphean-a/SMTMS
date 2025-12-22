using SMTMS.Core.Common;

namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{
    /// <summary>
    /// 从旧版 JSON 文件导入翻译数据
    /// </summary>
    Task<OperationResult> ImportFromLegacyJsonAsync(string jsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫描 manifest.json 文件并保存翻译到数据库
    /// </summary>
    Task<OperationResult> SaveTranslationsToDbAsync(string modDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从数据库恢复翻译到 manifest.json 文件
    /// </summary>
    Task<OperationResult> RestoreTranslationsFromDbAsync(string modDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 导出翻译到 Git 仓库
    /// </summary>
    Task<OperationResult> ExportTranslationsToGitRepo(string modDirectory, string repoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从 Git 仓库读取翻译并更新数据库（用于回滚后同步）
    /// </summary>
    Task<OperationResult> ImportTranslationsFromGitRepoAsync(string repoPath, CancellationToken cancellationToken = default);
}
