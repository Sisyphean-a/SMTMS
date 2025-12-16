using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{
    /// <summary>
    /// Imports translations from a legacy backup JSON file into the database.
    /// </summary>
    /// <param name="jsonPath">The path to the backup JSON file.</param>
    /// <returns>A result summary of the import operation.</returns>
    Task<(int successCount, int errorCount, string message)> ImportFromLegacyJsonAsync(string jsonPath);

    /// <summary>
    /// Applies all translations from the database to the actual manifest.json files in the mods directory.
    /// </summary>
    /// <param name="modsRootPath">The root directory containing mods.</param>
    /// <returns>A result summary of the apply operation.</returns>
    Task<(int appliedCount, int errorCount, string message)> ApplyTranslationsAsync(string modsRootPath);

    /// <summary>
    /// Extracts translations from manifest.json files in the mods directory to a backup JSON file.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    /// <param name="outputFilePath">The path to save the backup JSON file.</param>
    Task ExtractTranslationsAsync(string modDirectory, string outputFilePath);

    /// <summary>
    /// Restores translations from a backup JSON file to the manifest.json files in the mods directory.
    /// Checks for version compatibility and overrides if appropriate.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    /// <param name="backupFilePath">The path to the backup JSON file.</param>
    Task RestoreTranslationsAsync(string modDirectory, string backupFilePath);
}
