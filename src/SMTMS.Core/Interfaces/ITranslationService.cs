using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface ITranslationService
{

    /// <summary>
    /// Scans manifest.json files and saves any Chinese translations to the database.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    /// <param name="cancellationToken">🔥 取消令牌，支持长时间操作的取消</param>
    Task SaveTranslationsToDbAsync(string modDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores translations from the database to the manifest.json files in the mods directory.
    /// Only updates if the database has a translation.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    /// <param name="cancellationToken">🔥 取消令牌，支持长时间操作的取消</param>
    Task RestoreTranslationsFromDbAsync(string modDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports translations from legacy JSON backup file.
    /// </summary>
    /// <param name="jsonPath">Path to the legacy JSON file.</param>
    /// <param name="cancellationToken">🔥 取消令牌，支持长时间操作的取消</param>
    Task<(int successCount, int errorCount, string message)> ImportFromLegacyJsonAsync(string jsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports translations to Git repository.
    /// </summary>
    /// <param name="modDirectory">The root directory containing mods.</param>
    /// <param name="repoPath">Path to the Git repository.</param>
    /// <param name="cancellationToken">🔥 取消令牌，支持长时间操作的取消</param>
    Task ExportTranslationsToGitRepo(string modDirectory, string repoPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads translations from the Git repository (AppData) and updates the database.
    /// This is used after a Rollback/Reset to synchronize the DB with the reverted files.
    /// </summary>
    /// <param name="repoPath">Path to the Git repository.</param>
    /// <param name="cancellationToken">🔥 取消令牌，支持长时间操作的取消</param>
    Task ImportTranslationsFromGitRepoAsync(string repoPath, CancellationToken cancellationToken = default);

}
