using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Interfaces;
using SMTMS.Data.Context;
using SMTMS.Core.Models;

namespace SMTMS.Data.Repositories;

public class ModRepository : IModRepository
{
    private readonly AppDbContext _context;

    public ModRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ModMetadata>> GetAllModsAsync()
    {
        return await _context.ModMetadata.ToListAsync();
    }

    public async Task<ModMetadata?> GetModAsync(string uniqueId)
    {
        return await _context.ModMetadata.FindAsync(uniqueId);
    }

    public async Task UpsertModAsync(ModMetadata mod)
    {
        var existing = await _context.ModMetadata.FindAsync(mod.UniqueID);
        if (existing == null)
        {
            await _context.ModMetadata.AddAsync(mod);
        }
        else
        {
            // Update fields. Be careful not to overwrite user changes with old file data if logic requires.
            // For now, assume this method is called to update data.
            _context.Entry(existing).CurrentValues.SetValues(mod);
        }
        await _context.SaveChangesAsync();
    }
    
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
