using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMTMS.Core.Interfaces;
using SMTMS.Data.Context;
using SMTMS.Core.Models;

namespace SMTMS.Data.Repositories;

public class ModRepository(AppDbContext context, ILogger<ModRepository> logger) : IModRepository
{
    private readonly ILogger<ModRepository> _logger = logger;

    /// <summary>
    /// 获取所有 Mod 元数据（只读查询，使用 AsNoTracking 优化性能）
    /// </summary>
    public async Task<IEnumerable<ModMetadata>> GetAllModsAsync(CancellationToken cancellationToken = default)
    {
        // 🔥 EF Core 优化：只读查询使用 AsNoTracking() 减少内存占用
        // 🔥 支持取消令牌
        return await context.ModMetadata
            .AsNoTracking()
            .OrderBy(m => m.TranslatedName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 获取单个 Mod 元数据（用于更新操作，需要跟踪）
    /// </summary>
    public async Task<ModMetadata?> GetModAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        return await context.ModMetadata.FindAsync([uniqueId], cancellationToken);
    }

    public async Task UpsertModAsync(ModMetadata mod, CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        var existing = await context.ModMetadata.FindAsync([mod.UniqueID], cancellationToken);
        if (existing == null)
        {
            await context.ModMetadata.AddAsync(mod, cancellationToken);
        }
        else
        {
            // 如果传入的 mod 与数据库查出的 existing 是同一个实例（引用相等），
            // 说明它已经被 Tracked 且属性已在外部被修改，直接 SaveChanges 即可。
            // 只有当它们是不同对象时，才需要从 mod 复制值到 existing。
            if (!ReferenceEquals(existing, mod))
            {
                context.Entry(existing).CurrentValues.SetValues(mod);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 🔥 支持取消令牌
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批量获取多个 Mod 的元数据（性能优化，只读查询）
    /// </summary>
    public async Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(
        IEnumerable<string> uniqueIds,
        CancellationToken cancellationToken = default)
    {
        var idList = uniqueIds.ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<string, ModMetadata>();
        }

        // 🔥 EF Core 优化：只读查询使用 AsNoTracking() 减少内存占用
        // 🔥 支持取消令牌
        var mods = await context.ModMetadata
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
        if (modList.Count == 0)
        {
            return;
        }

        // 批量获取所有现有的 Mod
        var uniqueIds = modList.Select(m => m.UniqueID).ToList();
        // 🔥 支持取消令牌
        var existingModsList = await context.ModMetadata
            .Where(m => uniqueIds.Contains(m.UniqueID))
            .ToListAsync(cancellationToken);
        var existingMods = existingModsList.ToDictionary(m => m.UniqueID);

        var newMods = new List<ModMetadata>();
        var debugCount = 0;

        foreach (var mod in modList)
        {
            // 🔥 检查取消请求
            cancellationToken.ThrowIfCancellationRequested();

            if (existingMods.TryGetValue(mod.UniqueID, out var existing))
            {
                if (debugCount < 5 && existing.RelativePath != mod.RelativePath)
                {
                   _logger.LogInformation("🔄 更新DB路径 [{ID}]: '{Old}' -> '{New}'", mod.UniqueID, existing.RelativePath, mod.RelativePath);
                }

                // Update properties explicitly to ensure they stick
                existing.RelativePath = mod.RelativePath;
                existing.LastFileHash = mod.LastFileHash;
                existing.LastTranslationUpdate = mod.LastTranslationUpdate;
                existing.TranslatedName = mod.TranslatedName;
                existing.TranslatedDescription = mod.TranslatedDescription;
                
                // Fallback to SetValues for any other properties I missed
                context.Entry(existing).CurrentValues.SetValues(mod);
            }
            else
            {
                // 收集新记录以便稍后批量插入
                newMods.Add(mod);
            }
            debugCount++;
        }

        // 批量插入新记录（性能优化：使用 AddRangeAsync 代替循环 AddAsync）
        if (newMods.Count > 0)
        {
            await context.ModMetadata.AddRangeAsync(newMods, cancellationToken);
        }

        // 一次性保存所有变更
        // 🔥 支持取消令牌
        await context.SaveChangesAsync(cancellationToken);
    }
}
