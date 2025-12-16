using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{

    /// <summary>
    /// Scans manifest.json files and saves any Chinese translations to the database.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    Task SaveTranslationsToDbAsync(string modDirectory);

    /// <summary>
    /// Restores translations from the database to the manifest.json files in the mods directory.
    /// Only updates if the database has a translation.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    Task RestoreTranslationsFromDbAsync(string modDirectory);

}
