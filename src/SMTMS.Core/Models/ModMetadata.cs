using System.ComponentModel.DataAnnotations;

namespace SMTMS.Core.Models;

public class ModMetadata
{
    [Key]
    public string UniqueID { get; set; } = string.Empty;

    public string? UserCategory { get; set; }

    // NexusId 标记字段
    /// <summary>
    /// 标记此模组的 NexusId 是否是用户手动添加的（而非模组自带）
    /// 用于在同步时判断是否需要保留用户的修改
    /// </summary>
    public bool IsNexusIdUserAdded { get; set; }
    
    // 来自 Nexus 的缓存数据
    public string? NexusSummary { get; set; }
    public string? NexusDescription { get; set; }
    public string? NexusImageUrl { get; set; }
    public long? NexusDownloadCount { get; set; }
    public long? NexusEndorsementCount { get; set; }
    public DateTime? LastNexusCheck { get; set; }

    // 翻译数据
    public string? OriginalName { get; set; } // 首次扫描时的原始名称（通常为英文）
    public string? OriginalDescription { get; set; }
    
    public string? TranslatedName { get; set; }
    public string? TranslatedDescription { get; set; }
    
    public string? RelativePath { get; set; } // 相对于 Mods 文件夹的路径
    public bool IsMachineTranslated { get; set; }
    public DateTime? LastTranslationUpdate { get; set; }

    // NexusId 同步状态
    public string? NexusId { get; set; }
    
    // 内容指纹
    public string? LastFileHash { get; set; }

    // JSON 内容版本控制
    /// <summary>
    /// 首次扫描到的原始 JSON 内容（用于对比或重置）
    /// </summary>
    public string? OriginalJson { get; set; }

    /// <summary>
    /// 当前编辑/保存后的 JSON 内容
    /// </summary>
    public string? CurrentJson { get; set; }
}
