namespace SMTMS.UI.Messages;

/// <summary>
/// 状态消息，用于更新全局状态栏
/// </summary>
public class StatusMessage(string message, StatusLevel level = StatusLevel.Info)
{
    public string Message { get; } = message;
    public StatusLevel Level { get; } = level;
}

public enum StatusLevel
{
    Info,
    Success,
    Warning,
    Error
}

