using SMTMS.UI.ViewModels;

namespace SMTMS.UI.Messages;

/// <summary>
/// 模组加载完成消息
/// </summary>
public class ModsLoadedMessage(IReadOnlyList<ModViewModel> mods, string modsDirectory)
{
    public IReadOnlyList<ModViewModel> Mods { get; } = mods;
    public string ModsDirectory { get; } = modsDirectory;
}

