using SMTMS.Core.Primitives;

namespace SMTMS.Core.Interfaces;

/// <summary>
/// 翻译服务接口
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// 从旧版 JSON 文件导入翻译数据
    /// </summary>
    Task<OperationResult> ImportFromLegacyJsonAsync(string jsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫描 manifest.json 文件并保存翻译到数据库
    /// </summary>
    Task<OperationResult> SaveTranslationsToDbAsync(string modDirectory, string? commitMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从数据库恢复翻译到 manifest.json 文件
    /// </summary>
    Task<OperationResult> RestoreTranslationsFromDbAsync(string modDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚到指定的快照
    /// </summary>
    Task<OperationResult> RollbackSnapshotAsync(int snapshotId, string modDirectory, CancellationToken cancellationToken = default);
}
