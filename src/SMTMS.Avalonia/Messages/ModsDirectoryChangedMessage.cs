using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class ModsDirectoryChangedMessage(string value) : ValueChangedMessage<string>(value)
{
    public string NewDirectory => Value;
}
