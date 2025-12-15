using Newtonsoft.Json;

namespace SMTMS.Core.Models;

/// <summary>
/// Represents a translation backup entry, compatible with the Python script's JSON format.
/// </summary>
public class TranslationBackupEntry
{
    [JsonProperty("UniqueID")]
    public string UniqueID { get; set; } = string.Empty;

    [JsonProperty("Name")]
    public string? Name { get; set; }

    [JsonProperty("Description")]
    public string? Description { get; set; }

    [JsonProperty("Path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("IsChinese")]
    public bool IsChinese { get; set; }

    [JsonProperty("Nurl")]
    public string? Nurl { get; set; }
}
