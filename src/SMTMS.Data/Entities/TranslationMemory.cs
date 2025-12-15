using System.ComponentModel.DataAnnotations;

namespace SMTMS.Data.Entities;

public class TranslationMemory
{
    [Key]
    public string SourceHash { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty; // e.g., "DeepL", "Google"
    public DateTime Timestamp { get; set; }
}
