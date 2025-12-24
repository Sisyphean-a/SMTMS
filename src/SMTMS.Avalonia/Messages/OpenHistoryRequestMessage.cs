namespace SMTMS.Avalonia.Messages;

public class OpenHistoryRequestMessage 
{
    public string ModUniqueId { get; }
    public OpenHistoryRequestMessage(string modUniqueId) => ModUniqueId = modUniqueId;
}
