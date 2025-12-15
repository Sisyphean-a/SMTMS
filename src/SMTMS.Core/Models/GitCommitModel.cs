namespace SMTMS.Core.Models;

public class GitCommitModel
{
    public string ShortHash { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public string FullHash { get; set; } = string.Empty;
}
