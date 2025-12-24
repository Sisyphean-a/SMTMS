using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public class ModService(IFileSystem fileSystem) : IModService
{
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        // 处理 SMAPI 清单中常见的注释
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
    };
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    // 处理 SMAPI 清单中常见的注释

    public async Task<IEnumerable<ModManifest>> ScanModsAsync(string modsDirectory)
    {
        if (!_fileSystem.DirectoryExists(modsDirectory))
        {
            return new List<ModManifest>();
        }

        // SMAPI mods are usually in subfolders of Mods/
        // 我们在每个子文件夹中寻找 manifest.json
        var subDirectories = _fileSystem.GetDirectories(modsDirectory);

        // 并行读取所有 manifest.json 文件
        var tasks = subDirectories
            .Select(dir => _fileSystem.Combine(dir, "manifest.json"))
            .Where(path => _fileSystem.FileExists(path))
            .Select(ReadManifestAsync)
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
            // TODO: 记录错误或返回特定的错误结果
            // 目前返回 null 表示解析失败
            return null;
        }
    }

    public async Task WriteManifestAsync(string manifestPath, ModManifest manifest)
    {
        var json = JsonConvert.SerializeObject(manifest, _jsonSettings);
        await _fileSystem.WriteAllTextAsync(manifestPath, json);
    }

    public async Task UpdateModManifestAsync(string manifestPath, string? newName, string? newDescription)
    {
        if (!_fileSystem.FileExists(manifestPath))
        {
            throw new FileNotFoundException("找不到清单文件", manifestPath);
        }

        // 读取原始内容
        var originalJson = await _fileSystem.ReadAllTextAsync(manifestPath);

        // 使用 Core 中的 ManifestTextReplacer 替换内容，同时保留结构
        var updatedJson = SMTMS.Core.Helpers.ManifestTextReplacer.ReplaceNameAndDescription(originalJson, newName, newDescription);

        // 仅在发生更改时写回
        if (updatedJson != originalJson)
        {
            await _fileSystem.WriteAllTextAsync(manifestPath, updatedJson);
        }
    }
}
