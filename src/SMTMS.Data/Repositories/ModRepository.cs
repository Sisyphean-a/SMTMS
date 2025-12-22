using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Data.Context;
using SMTMS.Core.Models;

namespace SMTMS.Data.Repositories;

public class ModRepository : IModRepository
{
    private readonly AppDbContext _context;

    public ModRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 获取所有 Mod 元数据（只读查询，使用 AsNoTracking 优化性能）
    /// </summary>
    public async Task<IEnumerable<ModMetadata>> GetAllModsAsync(CancellationToken cancellationToken = default)
    {
        // 🔥 EF Core 优化：只读查询使用 AsNoTracking() 减少内存占用
        // 🔥 支持取消令牌
        return await _context.ModMetadata.AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取单个 Mod 元数据（用于更新操作，需要跟踪）
    /// </summary>
    public async Task<ModMetadata?> GetModAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        return await _context.ModMetadata.FindAsync(new object[] { uniqueId }, cancellationToken);
    }

    public async Task UpsertModAsync(ModMetadata mod, CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        var existing = await _context.ModMetadata.FindAsync(new object[] { mod.UniqueID }, cancellationToken);
        if (existing == null)
        {
            await _context.ModMetadata.AddAsync(mod, cancellationToken);
        }
        else
        {
            // Update fields. Be careful not to overwrite user changes with old file data if logic requires.
            // For now, assume this method is called to update data.
            _context.Entry(existing).CurrentValues.SetValues(mod);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批量获取多个 Mod 的元数据（性能优化，只读查询）
    /// </summary>
    public async Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(
        IEnumerable<string> uniqueIds,
        CancellationToken cancellationToken = default)
    {
        var idList = uniqueIds.ToList();
        if (!idList.Any())
        {
            return new Dictionary<string, ModMetadata>();
        }

        // 🔥 EF Core 优化：只读查询使用 AsNoTracking() 减少内存占用
        // 🔥 支持取消令牌
        var mods = await _context.ModMetadata
            .AsNoTracking()
            .Where(m => idList.Contains(m.UniqueID))
            .ToListAsync(cancellationToken);

        return mods.ToDictionary(m => m.UniqueID);
    }

    /// <summary>
    /// 批量插入或更新 Mod 元数据（性能优化版本）
    /// 一次性提交所有变更，避免多次数据库往返
    /// </summary>
    public async Task UpsertModsAsync(IEnumerable<ModMetadata> mods, CancellationToken cancellationToken = default)
    {
        var modList = mods.ToList();
        if (!modList.Any())
        {
            return;
        }

        // 批量获取所有现有的 Mod
        var uniqueIds = modList.Select(m => m.UniqueID).ToList();
        // 🔥 支持取消令牌
        var existingModsList = await _context.ModMetadata
            .Where(m => uniqueIds.Contains(m.UniqueID))
            .ToListAsync(cancellationToken);
        var existingMods = existingModsList.ToDictionary(m => m.UniqueID);

        foreach (var mod in modList)
        {
            // 🔥 检查取消请求
            cancellationToken.ThrowIfCancellationRequested();

            if (existingMods.TryGetValue(mod.UniqueID, out var existing))
            {
                // 更新现有记录
                _context.Entry(existing).CurrentValues.SetValues(mod);
            }
            else
            {
                // 添加新记录
                await _context.ModMetadata.AddAsync(mod, cancellationToken);
            }
        }

        // 一次性保存所有变更
        // 🔥 支持取消令牌
        await _context.SaveChangesAsync(cancellationToken);
    }
}
