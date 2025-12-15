using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public class ModService : IModService
{
    private readonly JsonSerializerSettings _jsonSettings;

    public ModService()
    {
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            // Handle comments which are common in SMAPI manifests
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
        };
    }

    public async Task<IEnumerable<ModManifest>> ScanModsAsync(string modsDirectory)
    {
        var manifests = new List<ModManifest>();

        if (!Directory.Exists(modsDirectory))
        {
            return manifests;
        }

        // SMAPI mods are usually in subfolders of Mods/
        // We look for manifest.json in each subfolder
        var subDirectories = Directory.GetDirectories(modsDirectory);

        foreach (var dir in subDirectories)
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                var manifest = await ReadManifestAsync(manifestPath);
                if (manifest != null)
                {
                    manifests.Add(manifest);
                }
            }
        }

        return manifests;
    }

    public async Task<ModManifest?> ReadManifestAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            return JsonConvert.DeserializeObject<ModManifest>(json, _jsonSettings);
        }
        catch (Exception)
        {
            // TODO: Log error or return specific error result
            // For now, return null to indicate failure to parse
            return null;
        }
    }

    public async Task WriteManifestAsync(string manifestPath, ModManifest manifest)
    {
        var json = JsonConvert.SerializeObject(manifest, _jsonSettings);
        await File.WriteAllTextAsync(manifestPath, json);
    }
}
