namespace SMTMS.UI.Messages;

/// <summary>
/// Mods 目录变更消息
/// </summary>
public class ModsDirectoryChangedMessage(string newDirectory)
{
    public string NewDirectory { get; } = newDirectory;
}

