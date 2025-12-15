namespace SMTMS.Core.Interfaces;

public interface IGamePathService
{
    /// <summary>
    /// Attempts to locate the Stardew Valley "Mods" directory.
    /// </summary>
    /// <returns>The full path to the Mods folder, or null if not found.</returns>
    string? GetModsPath();
}
