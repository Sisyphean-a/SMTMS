using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IModRepository
{
    Task<IEnumerable<ModMetadata>> GetAllModsAsync();
    Task<ModMetadata?> GetModAsync(string uniqueId);
    Task UpsertModAsync(ModMetadata mod);
    Task SaveChangesAsync();

    /// <summary>
    /// 批量获取多个 Mod 的元数据
    /// </summary>
    Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(IEnumerable<string> uniqueIds);

    /// <summary>
    /// 批量插入或更新 Mod 元数据（性能优化版本）
    /// </summary>
    Task UpsertModsAsync(IEnumerable<ModMetadata> mods);
}
