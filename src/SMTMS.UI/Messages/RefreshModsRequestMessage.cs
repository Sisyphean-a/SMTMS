namespace SMTMS.UI.Messages;

/// <summary>
/// 请求刷新模组列表消息
/// </summary>
public class RefreshModsRequestMessage
{
    public static RefreshModsRequestMessage Instance { get; } = new();

    private RefreshModsRequestMessage() { }
}

