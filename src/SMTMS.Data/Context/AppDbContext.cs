using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Models;

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
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var smtmsPath = Path.Combine(appDataPath, "SMTMS");
            if (!Directory.Exists(smtmsPath))
            {
                Directory.CreateDirectory(smtmsPath);
            }
            var dbPath = Path.Combine(smtmsPath, "smtms.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
