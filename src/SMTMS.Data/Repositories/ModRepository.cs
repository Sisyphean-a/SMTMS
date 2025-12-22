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

    public async Task<IEnumerable<ModMetadata>> GetAllModsAsync()
    {
        return await _context.ModMetadata.ToListAsync();
    }

    public async Task<ModMetadata?> GetModAsync(string uniqueId)
    {
        return await _context.ModMetadata.FindAsync(uniqueId);
    }

    public async Task UpsertModAsync(ModMetadata mod)
    {
        var existing = await _context.ModMetadata.FindAsync(mod.UniqueID);
        if (existing == null)
        {
            await _context.ModMetadata.AddAsync(mod);
        }
        else
        {
            // Update fields. Be careful not to overwrite user changes with old file data if logic requires.
            // For now, assume this method is called to update data.
            _context.Entry(existing).CurrentValues.SetValues(mod);
        }
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 批量获取多个 Mod 的元数据（性能优化）
    /// </summary>
    public async Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(IEnumerable<string> uniqueIds)
    {
        var idList = uniqueIds.ToList();
        if (!idList.Any())
        {
            return new Dictionary<string, ModMetadata>();
        }

        var mods = await _context.ModMetadata
            .Where(m => idList.Contains(m.UniqueID))
            .ToListAsync();

        return mods.ToDictionary(m => m.UniqueID);
    }

    /// <summary>
    /// 批量插入或更新 Mod 元数据（性能优化版本）
    /// 一次性提交所有变更，避免多次数据库往返
    /// </summary>
    public async Task UpsertModsAsync(IEnumerable<ModMetadata> mods)
    {
        var modList = mods.ToList();
        if (!modList.Any())
        {
            return;
        }

        // 批量获取所有现有的 Mod
        var uniqueIds = modList.Select(m => m.UniqueID).ToList();
        var existingModsList = await _context.ModMetadata
            .Where(m => uniqueIds.Contains(m.UniqueID))
            .ToListAsync();
        var existingMods = existingModsList.ToDictionary(m => m.UniqueID);

        foreach (var mod in modList)
        {
            if (existingMods.TryGetValue(mod.UniqueID, out var existing))
            {
                // 更新现有记录
                _context.Entry(existing).CurrentValues.SetValues(mod);
            }
            else
            {
                // 添加新记录
                await _context.ModMetadata.AddAsync(mod);
            }
        }

        // 一次性保存所有变更
        await _context.SaveChangesAsync();
    }
}
