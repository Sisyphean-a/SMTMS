using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IModRepository
{
    /// <summary>
    /// 获取所有 Mod 元数据
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IEnumerable<ModMetadata>> GetAllModsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个 Mod 元数据
    /// </summary>
    /// <param name="uniqueId">Mod 唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<ModMetadata?> GetModAsync(string uniqueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入或更新单个 Mod 元数据
    /// </summary>
    /// <param name="mod">Mod 元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertModAsync(ModMetadata mod, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存所有变更
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取多个 Mod 的元数据
    /// </summary>
    /// <param name="uniqueIds">Mod 唯一标识列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(IEnumerable<string> uniqueIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量插入或更新 Mod 元数据（性能优化版本）
    /// </summary>
    /// <param name="mods">Mod 元数据列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertModsAsync(IEnumerable<ModMetadata> mods, CancellationToken cancellationToken = default);
}
