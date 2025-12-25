namespace SMTMS.Avalonia.Messages;

public class OpenHistoryRequestMessage(string modUniqueId)
{
    public string ModUniqueId { get; } = modUniqueId;
}
