using Microsoft.EntityFrameworkCore;
using SMTMS.Data.Entities;

namespace SMTMS.Data.Context;

public class AppDbContext : DbContext
{
    public DbSet<ModMetadata> ModMetadata { get; set; }
    public DbSet<TranslationMemory> TranslationMemory { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Default usage for design-time tools if needed
            optionsBuilder.UseSqlite("Data Source=smtms.db");
        }
    }
}
