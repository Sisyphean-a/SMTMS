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

    Task<(int successCount, int errorCount, string message)> ImportFromLegacyJsonAsync(string jsonPath);

    Task ExportTranslationsToGitRepo(string modDirectory, string repoPath);

    /// <summary>
    /// Reads translations from the Git repository (AppData) and updates the database.
    /// This is used after a Rollback/Reset to synchronize the DB with the reverted files.
    /// </summary>
    Task ImportTranslationsFromGitRepoAsync(string repoPath);

}
