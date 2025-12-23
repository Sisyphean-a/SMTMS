using System.ComponentModel.DataAnnotations;

namespace SMTMS.Core.Models;

public class ModMetadata
{
    [Key]
    public string UniqueID { get; set; } = string.Empty;

    public string? UserCategory { get; set; }
    
    // Cached data from Nexus
    public string? NexusSummary { get; set; }
    public string? NexusDescription { get; set; }
    public string? NexusImageUrl { get; set; }
    public long? NexusDownloadCount { get; set; }
    public long? NexusEndorsementCount { get; set; }
    public DateTime? LastNexusCheck { get; set; }

    // Translation Data
    public string? OriginalName { get; set; } // Name from manifest when first scanned (likely English)
    public string? OriginalDescription { get; set; }
    
    public string? TranslatedName { get; set; }
    public string? TranslatedDescription { get; set; }
    
    public string? RelativePath { get; set; } // Path relative to Mods folder
    public bool IsMachineTranslated { get; set; }
    public DateTime? LastTranslationUpdate { get; set; }
    
    // Content Fingerprint
    public string? LastFileHash { get; set; }

    // JSON Content Versioning
    /// <summary>
    /// 首次扫描到的原始 JSON 内容（用于对比或重置）
    /// </summary>
    public string? OriginalJson { get; set; }

    /// <summary>
    /// 当前编辑/保存后的 JSON 内容
    /// </summary>
    public string? CurrentJson { get; set; }
}
