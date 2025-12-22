namespace SMTMS.UI.Messages;

/// <summary>
/// 状态消息，用于更新全局状态栏
/// </summary>
public class StatusMessage
{
    public string Message { get; }
    public StatusLevel Level { get; }

    public StatusMessage(string message, StatusLevel level = StatusLevel.Info)
    {
        Message = message;
        Level = level;
    }
}

public enum StatusLevel
{
    Info,
    Success,
    Warning,
    Error
}

