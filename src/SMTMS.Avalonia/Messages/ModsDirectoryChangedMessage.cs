using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class ModsDirectoryChangedMessage : ValueChangedMessage<string>
{
    public string NewDirectory => Value;
    public ModsDirectoryChangedMessage(string value) : base(value) { }
}
