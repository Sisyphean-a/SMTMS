using System.ComponentModel.DataAnnotations;

namespace SMTMS.Core.Models;

/// <summary>
/// Git Diff 缓存表，用于加速历史记录查看
/// </summary>
public class GitDiffCache
{
    /// <summary>
    /// 提交哈希（主键）
    /// </summary>
    [Key]
    public string CommitHash { get; set; } = string.Empty;

    /// <summary>
    /// 序列化后的 Diff 数据（使用 MessagePack 或 JSON）
    /// </summary>
    public byte[] SerializedDiffData { get; set; } = [];

    /// <summary>
    /// 变更的模组数量
    /// </summary>
    public int ModCount { get; set; }

    /// <summary>
    /// 缓存创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 数据格式版本（用于兼容性检查）
    /// </summary>
    public int FormatVersion { get; set; } = 1;
}

