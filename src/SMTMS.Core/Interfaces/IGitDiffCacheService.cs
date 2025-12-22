using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

/// <summary>
/// Git Diff 缓存服务接口
/// 用于缓存 Git Diff 结果，避免重复计算
/// </summary>
public interface IGitDiffCacheService
{
    /// <summary>
    /// 从缓存中获取指定提交的 Diff 数据
    /// </summary>
    /// <param name="commitHash">提交哈希</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>缓存的 Diff 数据，如果不存在则返回 null</returns>
    Task<List<ModDiffModel>?> GetCachedDiffAsync(string commitHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存 Diff 数据到缓存
    /// </summary>
    /// <param name="commitHash">提交哈希</param>
    /// <param name="diffData">Diff 数据</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    Task SaveDiffCacheAsync(string commitHash, List<ModDiffModel> diffData, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清理旧的缓存数据（基于时间）
    /// </summary>
    /// <param name="daysToKeep">保留的天数，默认 30 天</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>清理的缓存数量</returns>
    Task<int> ClearOldCachesAsync(int daysToKeep = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// 🔥 LRU 缓存清理策略：保留最近访问的 N 个缓存，删除其余
    /// </summary>
    /// <param name="maxCacheCount">最大缓存数量，默认 100</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>清理的缓存数量</returns>
    Task<int> ClearLRUCachesAsync(int maxCacheCount = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 🔥 智能缓存清理：结合时间和数量限制
    /// </summary>
    /// <param name="daysToKeep">保留天数，默认 30 天</param>
    /// <param name="maxCacheCount">最大缓存数量，默认 100</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>清理的缓存数量</returns>
    Task<int> SmartClearCachesAsync(int daysToKeep = 30, int maxCacheCount = 100, CancellationToken cancellationToken = default);
}

