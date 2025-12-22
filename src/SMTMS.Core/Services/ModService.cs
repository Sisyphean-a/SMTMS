using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public class ModService : IModService
{
    private readonly JsonSerializerSettings _jsonSettings;
    private readonly IFileSystem _fileSystem;

    public ModService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

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
        if (!_fileSystem.DirectoryExists(modsDirectory))
        {
            return new List<ModManifest>();
        }

        // SMAPI mods are usually in subfolders of Mods/
        // We look for manifest.json in each subfolder
        var subDirectories = _fileSystem.GetDirectories(modsDirectory);

        // 🔥 并行读取所有 manifest.json 文件
        var tasks = subDirectories
            .Select(dir => _fileSystem.Combine(dir, "manifest.json"))
            .Where(path => _fileSystem.FileExists(path))
            .Select(manifestPath => ReadManifestAsync(manifestPath))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // 过滤掉解析失败的（null）
        return results.Where(m => m != null).Cast<ModManifest>().ToList();
    }

    public async Task<ModManifest?> ReadManifestAsync(string manifestPath)
    {
        if (!_fileSystem.FileExists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(manifestPath);
            var manifest = JsonConvert.DeserializeObject<ModManifest>(json, _jsonSettings);
            if (manifest != null)
            {
                manifest.ManifestPath = manifestPath;
            }
            return manifest;
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
        await _fileSystem.WriteAllTextAsync(manifestPath, json);
    }
}
