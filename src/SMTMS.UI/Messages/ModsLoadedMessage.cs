using SMTMS.UI.ViewModels;

namespace SMTMS.UI.Messages;

/// <summary>
/// 模组加载完成消息
/// </summary>
public class ModsLoadedMessage
{
    public IReadOnlyList<ModViewModel> Mods { get; }
    public string ModsDirectory { get; }

    public ModsLoadedMessage(IReadOnlyList<ModViewModel> mods, string modsDirectory)
    {
        Mods = mods;
        ModsDirectory = modsDirectory;
    }
}

