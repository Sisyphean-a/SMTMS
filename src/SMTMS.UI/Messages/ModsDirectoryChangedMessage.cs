namespace SMTMS.UI.Messages;

/// <summary>
/// Mods 目录变更消息
/// </summary>
public class ModsDirectoryChangedMessage
{
    public string NewDirectory { get; }

    public ModsDirectoryChangedMessage(string newDirectory)
    {
        NewDirectory = newDirectory;
    }
}

