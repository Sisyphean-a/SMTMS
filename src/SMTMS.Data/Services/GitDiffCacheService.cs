using MessagePack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Data.Context;

namespace SMTMS.Data.Services;

/// <summary>
/// Git Diff 缓存服务实现
/// 使用 MessagePack 序列化和 SQLite 数据库存储
/// </summary>
public class GitDiffCacheService(AppDbContext context, ILogger<GitDiffCacheService> logger) : IGitDiffCacheService
{
    private const int CurrentFormatVersion = 1;

    /// <summary>
    /// 从缓存中获取指定提交的 Diff 数据
    /// </summary>
    public async Task<List<ModDiffModel>?> GetCachedDiffAsync(string commitHash, CancellationToken cancellationToken = default)
    {
        try
        {
            // 🔥 支持取消令牌
            var cache = await context.GitDiffCache
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommitHash == commitHash, cancellationToken);

            if (cache == null)
            {
                logger.LogDebug("缓存未命中: {CommitHash}", commitHash);
                return null;
            }

            // 检查格式版本
            if (cache.FormatVersion != CurrentFormatVersion)
            {
                logger.LogWarning("缓存格式版本不匹配 (期望: {Expected}, 实际: {Actual})，忽略缓存", 
                    CurrentFormatVersion, cache.FormatVersion);
                return null;
            }

            // 反序列化
            var diffData = MessagePackSerializer.Deserialize<List<ModDiffModel>>(cache.SerializedDiffData);
            logger.LogInformation("缓存命中: {CommitHash}, 包含 {Count} 个模组变更", commitHash, diffData.Count);
            return diffData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取缓存失败: {CommitHash}", commitHash);
            return null;
        }
    }

    /// <summary>
    /// 保存 Diff 数据到缓存
    /// </summary>
    public async Task SaveDiffCacheAsync(string commitHash, List<ModDiffModel> diffData, CancellationToken cancellationToken = default)
    {
        try
        {
            // 序列化
            var serializedData = MessagePackSerializer.Serialize(diffData);

            // 检查是否已存在
            // 🔥 支持取消令牌
            var existingCache = await context.GitDiffCache
                .FirstOrDefaultAsync(c => c.CommitHash == commitHash, cancellationToken);

            if (existingCache != null)
            {
                // 更新现有缓存
                existingCache.SerializedDiffData = serializedData;
                existingCache.ModCount = diffData.Count;
                existingCache.CreatedAt = DateTime.UtcNow;
                existingCache.FormatVersion = CurrentFormatVersion;
            }
            else
            {
                // 创建新缓存
                var cache = new GitDiffCache
                {
                    CommitHash = commitHash,
                    SerializedDiffData = serializedData,
                    ModCount = diffData.Count,
                    CreatedAt = DateTime.UtcNow,
                    FormatVersion = CurrentFormatVersion
                };
                await context.GitDiffCache.AddAsync(cache, cancellationToken);
            }

            // 🔥 支持取消令牌
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("缓存已保存: {CommitHash}, 包含 {Count} 个模组变更, 大小: {Size} bytes",
                commitHash, diffData.Count, serializedData.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "保存缓存失败: {CommitHash}", commitHash);
            // 不抛出异常，避免影响主流程
        }
    }

    /// <summary>
    /// 清理旧的缓存数据（基于时间）
    /// </summary>
    public async Task<int> ClearOldCachesAsync(int daysToKeep = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            // 🔥 EF Core 优化：只读查询使用 AsNoTracking()
            // 🔥 支持取消令牌
            var oldCaches = await context.GitDiffCache
                .AsNoTracking()
                .Where(c => c.CreatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldCaches.Count != 0)
            {
                context.GitDiffCache.RemoveRange(oldCaches);
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("已清理 {Count} 个旧缓存（超过 {Days} 天）", oldCaches.Count, daysToKeep);
                return oldCaches.Count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清理旧缓存失败");
            return 0;
        }
    }

    /// <summary>
    /// 🔥 LRU 缓存清理策略：保留最近访问的 N 个缓存，删除其余
    /// </summary>
    /// <param name="maxCacheCount">最大缓存数量，默认 100</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>清理的缓存数量</returns>
    public async Task<int> ClearLRUCachesAsync(int maxCacheCount = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取当前缓存总数
            // 🔥 支持取消令牌
            var totalCount = await context.GitDiffCache.CountAsync(cancellationToken);

            if (totalCount <= maxCacheCount)
            {
                logger.LogDebug("缓存数量 {Count} 未超过限制 {Max}，无需清理", totalCount, maxCacheCount);
                return 0;
            }

            // 按创建时间降序排序，保留最新的 maxCacheCount 个
            // 🔥 支持取消令牌
            var cachesToDelete = await context.GitDiffCache
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Skip(maxCacheCount)
                .ToListAsync(cancellationToken);

            if (cachesToDelete.Count == 0) return 0;
            context.GitDiffCache.RemoveRange(cachesToDelete);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("LRU 清理完成：删除 {Count} 个旧缓存，保留最新 {Max} 个",
                cachesToDelete.Count, maxCacheCount);
            return cachesToDelete.Count;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LRU 缓存清理失败");
            return 0;
        }
    }

    /// <summary>
    /// 🔥 智能缓存清理：结合时间和数量限制
    /// </summary>
    /// <param name="daysToKeep">保留天数，默认 30 天</param>
    /// <param name="maxCacheCount">最大缓存数量，默认 100</param>
    /// <param name="cancellationToken">🔥 取消令牌</param>
    /// <returns>清理的缓存数量</returns>
    public async Task<int> SmartClearCachesAsync(int daysToKeep = 30, int maxCacheCount = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var totalCleared = 0;

            // 第一步：清理过期缓存
            // 🔥 支持取消令牌
            var oldCleared = await ClearOldCachesAsync(daysToKeep, cancellationToken);
            totalCleared += oldCleared;

            // 第二步：如果仍然超过数量限制，执行 LRU 清理
            // 🔥 支持取消令牌
            var lruCleared = await ClearLRUCachesAsync(maxCacheCount, cancellationToken);
            totalCleared += lruCleared;

            if (totalCleared > 0)
            {
                logger.LogInformation("智能缓存清理完成：共清理 {Count} 个缓存", totalCleared);
            }

            return totalCleared;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "智能缓存清理失败");
            return 0;
        }
    }
}

