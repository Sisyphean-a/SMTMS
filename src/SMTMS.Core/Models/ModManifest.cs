using Newtonsoft.Json;

namespace SMTMS.Core.Models;

/// <summary>
/// Represents the manifest.json file structure of a Stardew Valley mod.
/// Using Newtonsoft.Json for better handling of non-standard JSON often found in mods.
/// </summary>
public class ModManifest
{
    [JsonProperty("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Author")]
    public string Author { get; set; } = string.Empty;

    [JsonProperty("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonProperty("Description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("UniqueID")]
    public string UniqueID { get; set; } = string.Empty;

    [JsonProperty("EntryDll")]
    public string? EntryDll { get; set; }

    [JsonProperty("ContentPackFor")]
    public ContentPackFor? ContentPackFor { get; set; }

    [JsonProperty("Dependencies")]
    public ModDependency[]? Dependencies { get; set; }

    [JsonProperty("UpdateKeys")]
    public string[]? UpdateKeys { get; set; }

    [JsonIgnore]
    public string? ManifestPath { get; set; }
}

public class ContentPackFor
{
    [JsonProperty("UniqueID")]
    public string UniqueID { get; set; } = string.Empty;
}

public class ModDependency
{
    [JsonProperty("UniqueID")]
    public string UniqueID { get; set; } = string.Empty;

    [JsonProperty("IsRequired")]
    public bool IsRequired { get; set; } = true;
}
