using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IHistoryRepository
{
    /// <summary>
    /// 创建一个新的历史快照
    /// </summary>
    /// <param name="message">快照信息</param>
    /// <param name="modCount">包含的 Mod 数量</param>
    /// <param name="cancellationToken"></param>
    /// <returns>创建的快照 ID</returns>
    Task<int> CreateSnapshotAsync(string message, int modCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量保存 Mod 的历史记录
    /// </summary>
    /// <param name="histories">历史记录列表</param>
    /// <param name="cancellationToken"></param>
    Task SaveModHistoriesAsync(IEnumerable<ModTranslationHistory> histories, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有快照列表（按时间倒序）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<HistorySnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某个 Mod 的所有历史版本记录
    /// </summary>
    /// <param name="modUniqueId">Mod 唯一 ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<ModTranslationHistory>> GetHistoryForModAsync(string modUniqueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某个快照下所有 Mod 的具体历史记录（用于全局回滚）
    /// 需要找到 <= snapshotId 的最新一条记录
    /// </summary>
    /// <param name="snapshotId">快照 ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<ModTranslationHistory>> GetModHistoriesForSnapshotAsync(int snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某个快照中实际发生变更的 Mod 历史记录（用于查看变更详情）
    /// 仅返回 SnapshotId 严格匹配的记录
    /// </summary>
    Task<List<ModTranslationHistory>> GetSnapshotChangesAsync(int snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定快照ID之后的所有快照和历史记录（用于 Reset 模式的回滚）
    /// </summary>
    Task DeleteSnapshotsAfterAsync(int snapshotId, CancellationToken cancellationToken = default);
}
