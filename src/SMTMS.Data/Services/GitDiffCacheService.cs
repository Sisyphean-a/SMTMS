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
public class GitDiffCacheService : IGitDiffCacheService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GitDiffCacheService> _logger;
    private const int CurrentFormatVersion = 1;

    public GitDiffCacheService(AppDbContext context, ILogger<GitDiffCacheService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 从缓存中获取指定提交的 Diff 数据
    /// </summary>
    public async Task<List<ModDiffModel>?> GetCachedDiffAsync(string commitHash)
    {
        try
        {
            var cache = await _context.GitDiffCache
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommitHash == commitHash);

            if (cache == null)
            {
                _logger.LogDebug("缓存未命中: {CommitHash}", commitHash);
                return null;
            }

            // 检查格式版本
            if (cache.FormatVersion != CurrentFormatVersion)
            {
                _logger.LogWarning("缓存格式版本不匹配 (期望: {Expected}, 实际: {Actual})，忽略缓存", 
                    CurrentFormatVersion, cache.FormatVersion);
                return null;
            }

            // 反序列化
            var diffData = MessagePackSerializer.Deserialize<List<ModDiffModel>>(cache.SerializedDiffData);
            _logger.LogInformation("缓存命中: {CommitHash}, 包含 {Count} 个模组变更", commitHash, diffData.Count);
            return diffData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取缓存失败: {CommitHash}", commitHash);
            return null;
        }
    }

    /// <summary>
    /// 保存 Diff 数据到缓存
    /// </summary>
    public async Task SaveDiffCacheAsync(string commitHash, List<ModDiffModel> diffData)
    {
        try
        {
            // 序列化
            var serializedData = MessagePackSerializer.Serialize(diffData);

            // 检查是否已存在
            var existingCache = await _context.GitDiffCache
                .FirstOrDefaultAsync(c => c.CommitHash == commitHash);

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
                await _context.GitDiffCache.AddAsync(cache);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("缓存已保存: {CommitHash}, 包含 {Count} 个模组变更, 大小: {Size} bytes", 
                commitHash, diffData.Count, serializedData.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存缓存失败: {CommitHash}", commitHash);
            // 不抛出异常，避免影响主流程
        }
    }

    /// <summary>
    /// 清理旧的缓存数据
    /// </summary>
    public async Task<int> ClearOldCachesAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldCaches = await _context.GitDiffCache
                .Where(c => c.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldCaches.Any())
            {
                _context.GitDiffCache.RemoveRange(oldCaches);
                await _context.SaveChangesAsync();
                _logger.LogInformation("已清理 {Count} 个旧缓存（超过 {Days} 天）", oldCaches.Count, daysToKeep);
                return oldCaches.Count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧缓存失败");
            return 0;
        }
    }
}

