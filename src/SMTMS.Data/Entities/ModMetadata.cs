using System.ComponentModel.DataAnnotations;

namespace SMTMS.Data.Entities;

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
}
