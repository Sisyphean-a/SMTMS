using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SMTMS.Core.Models;

/// <summary>
/// 代表一次全局的历史快照（对应原来的 Git Commit）
/// </summary>
public class HistorySnapshot
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// 快照创建时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 提交信息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 当时包含的 Mod 总数
    /// </summary>
    public int TotalMods { get; set; }
}
