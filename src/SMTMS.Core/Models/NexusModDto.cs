namespace SMTMS.Core.Models;

public class NexusModDto
{
    public string UniqueID { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PictureUrl { get; set; } = string.Empty;
    public long DownloadCount { get; set; }
    public long EndorsementCount { get; set; }
}
