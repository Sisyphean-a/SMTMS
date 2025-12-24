using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public enum StatusLevel { Info, Success, Warning, Error }

public class StatusMessage : ValueChangedMessage<string>
{
    public StatusLevel Level { get; }
    public StatusMessage(string value, StatusLevel level = StatusLevel.Info) : base(value)
    {
        Level = level;
    }
}
