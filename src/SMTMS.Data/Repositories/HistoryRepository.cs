using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Data.Context;

namespace SMTMS.Data.Repositories;

public class HistoryRepository(AppDbContext context, ILogger<HistoryRepository> logger) : IHistoryRepository
{
    public async Task<int> CreateSnapshotAsync(string message, int modCount, CancellationToken cancellationToken = default)
    {
        var snapshot = new HistorySnapshot
        {
            Message = message,
            TotalMods = modCount,
            Timestamp = DateTime.Now
        };
        
        await context.HistorySnapshots.AddAsync(snapshot, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Created Snapshot #{Id}: {Message}", snapshot.Id, message);
        return snapshot.Id;
    }

    public async Task SaveModHistoriesAsync(IEnumerable<ModTranslationHistory> histories, CancellationToken cancellationToken = default)
    {
        await context.ModTranslationHistories.AddRangeAsync(histories, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<HistorySnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        return await context.HistorySnapshots
            .AsNoTracking()
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ModTranslationHistory>> GetHistoryForModAsync(string modUniqueId, CancellationToken cancellationToken = default)
    {
        return await context.ModTranslationHistories
            .AsNoTracking()
            .Where(h => h.ModUniqueId == modUniqueId)
            .Include(h => h.Snapshot)
            .OrderByDescending(h => h.Snapshot!.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ModTranslationHistory>> GetModHistoriesForSnapshotAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        // 逻辑：获取某个 Snapshot 之前（含）的每个 Mod 的最新一条记录
        // 1. 获取目标 snapshot 的时间
        var targetSnapshot = await context.HistorySnapshots.FindAsync([snapshotId], cancellationToken);
        if (targetSnapshot == null) return [];
        
        // 2. 找到所有 <= 该时间的 Snapshot Ids
        // 实际上可以直接用 SnapshostId <= targetId (假设ID自增且时序一致) 或者 Timestamp <= targetTimestamp
        // 这里用 SnapshotId 简单处理
        
        // Complex Query: Group By ModUniqueId to find max ID <= snapshotId
        // EF Core 翻译 GroupBy 可能有限制，改为两步或者 Raw SQL 可能更好，但在 SQLite 上简单 GroupBy Select Max 应该支持
        
        // select * from ModTranslationHistories where Id in (
        //   select max(Id) from ModTranslationHistories 
        //   where SnapshotId <= @snapshotId 
        //   group by ModUniqueId
        // )
        
        var historyIds = await context.ModTranslationHistories
            .Where(h => h.SnapshotId <= snapshotId)
            .GroupBy(h => h.ModUniqueId)
            .Select(g => g.Max(h => h.Id))
            .ToListAsync(cancellationToken);
            
        if (historyIds.Count == 0) return [];

        return await context.ModTranslationHistories
            .AsNoTracking()
            .Where(h => historyIds.Contains(h.Id))
            .Include(h => h.ModMetadata) // 需要元数据来恢复文件路径等
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ModTranslationHistory>> GetSnapshotChangesAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        return await context.ModTranslationHistories
            .AsNoTracking()
            .Where(h => h.SnapshotId == snapshotId)
            .Include(h => h.ModMetadata) 
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteSnapshotsAfterAsync(int snapshotId, CancellationToken cancellationToken = default)
    {
        // 1. 查找所有时间更晚的快照（ID > snapshotId）
        // 假设 ID 是自增的，如果不是，应该用 Timestamp
        var newerSnapshots = await context.HistorySnapshots
            .Where(s => s.Id > snapshotId)
            .ToListAsync(cancellationToken);

        if (newerSnapshots.Count == 0) return;

        var newerSnapshotIds = newerSnapshots.Select(s => s.Id).ToList();

        // 2. 删除关联的 ModHistory
        var historiesToDelete = await context.ModTranslationHistories
            .Where(h => newerSnapshotIds.Contains(h.SnapshotId))
            .ToListAsync(cancellationToken);
            
        context.ModTranslationHistories.RemoveRange(historiesToDelete);
        
        // 3. 删除快照本身
        context.HistorySnapshots.RemoveRange(newerSnapshots);
        
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Deleted {SnapshotCount} snapshots and {HistoryCount} records after ID {Id}", 
            newerSnapshots.Count, historiesToDelete.Count, snapshotId);
    }
}
