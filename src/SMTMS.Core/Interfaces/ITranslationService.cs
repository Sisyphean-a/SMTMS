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
}
