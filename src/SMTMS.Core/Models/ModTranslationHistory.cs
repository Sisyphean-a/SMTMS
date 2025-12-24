using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SMTMS.Core.Models;

/// <summary>
/// 存储 Mod 在某个快照时刻的历史状态（增量存储）
/// </summary>
public class ModTranslationHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 关联的快照 ID
    /// </summary>
    public int SnapshotId { get; set; }

    [ForeignKey(nameof(SnapshotId))]
    public HistorySnapshot? Snapshot { get; set; }

    /// <summary>
    /// Mod 的唯一标识符
    /// </summary>
    [Required]
    public string ModUniqueId { get; set; } = string.Empty;

    // 为了避免强外键约束导致删除 ModMetadata 时级联删除历史，这里可以只存 ID，或者设为可空外键。
    // 根据需求，保留历史比保留外键约束更重要，且 ModUniqueId 是字符串主键。
    // 这里选择显式关联，以便后续查询。
    [ForeignKey(nameof(ModUniqueId))]
    public ModMetadata? ModMetadata { get; set; }

    /// <summary>
    /// 这一时刻该 Mod 的完整 JSON 内容
    /// </summary>
    [Required]
    public string JsonContent { get; set; } = string.Empty;

    /// <summary>
    /// 内容的 Hash 值（用于快速比对去重）
    /// </summary>
    [Required]
    public string PreviousHash { get; set; } = string.Empty;
}
