using Microsoft.EntityFrameworkCore;
using SMTMS.Core.Models;

namespace SMTMS.Data.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ModMetadata> ModMetadata { get; set; }
    public DbSet<TranslationMemory> TranslationMemory { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<HistorySnapshot> HistorySnapshots { get; set; }
    public DbSet<ModTranslationHistory> ModTranslationHistories { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var smtmsPath = Path.Combine(appDataPath, "SMTMS");
        if (!Directory.Exists(smtmsPath))
        {
            Directory.CreateDirectory(smtmsPath);
        }
        var dbPath = Path.Combine(smtmsPath, "smtms.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 为 ModMetadata 添加索引以加速查询
        modelBuilder.Entity<ModMetadata>()
            .HasIndex(m => m.LastTranslationUpdate);

        modelBuilder.Entity<ModMetadata>()
            .HasIndex(m => m.RelativePath);

        // 为 TranslationMemory 添加索引
        modelBuilder.Entity<TranslationMemory>()
            .HasIndex(t => t.Engine);

        modelBuilder.Entity<TranslationMemory>()
            .HasIndex(t => t.Timestamp);

        // HistorySnapshot 索引
        modelBuilder.Entity<HistorySnapshot>()
            .HasIndex(h => h.Timestamp);

        // ModTranslationHistory 索引
        modelBuilder.Entity<ModTranslationHistory>()
            .HasIndex(h => h.SnapshotId);

        modelBuilder.Entity<ModTranslationHistory>()
            .HasIndex(h => h.ModUniqueId);
    }
}
