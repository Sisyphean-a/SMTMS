using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public enum StatusLevel { Info, Success, Warning, Error }

public class StatusMessage(string value, StatusLevel level = StatusLevel.Info) : ValueChangedMessage<string>(value)
{
    public StatusLevel Level { get; } = level;
}
