using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IModService
{
    /// <summary>
    /// Scans a directory for mods and returns a list of valid manifests.
    /// </summary>
    Task<IEnumerable<ModManifest>> ScanModsAsync(string modsDirectory);

    /// <summary>
    /// Reads a single manifest file.
    /// </summary>
    Task<ModManifest?> ReadManifestAsync(string manifestPath);

    /// <summary>
    /// Saves changes to a manifest file.
    /// </summary>
    Task WriteManifestAsync(string manifestPath, ModManifest manifest);
}
