using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{
    /// <summary>
    /// Scans mods for Chinese translations and backs them up to a JSON file.
    /// </summary>
    /// <param name="modsRootPath">The root directory containing mods.</param>
    /// <param name="backupPath">The path where the backup JSON file should be saved.</param>
    /// <returns>A result summary of the backup operation.</returns>
    Task<(int successCount, int errorCount, string message)> BackupTranslationsAsync(string modsRootPath, string backupPath);

    /// <summary>
    /// Restores translations from a backup JSON file to the mods' manifest.json files.
    /// Uses Regex to preserve comments and formatting.
    /// </summary>
    /// <param name="modsRootPath">The root directory containing mods.</param>
    /// <param name="backupPath">The path to the backup JSON file.</param>
    /// <returns>A result summary of the restore operation.</returns>
    Task<(int restoredCount, int failedCount, int skippedCount, string message)> RestoreTranslationsAsync(string modsRootPath, string backupPath);
}
