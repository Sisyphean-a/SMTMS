using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IModRepository
{
    Task<IEnumerable<ModMetadata>> GetAllModsAsync();
    Task<ModMetadata?> GetModAsync(string uniqueId);
    Task UpsertModAsync(ModMetadata mod);
    Task SaveChangesAsync();
}
