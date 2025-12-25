using System.Collections.Generic;
using SMTMS.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class ModsLoadedMessage(List<ModViewModel> mods, string directory)
    : ValueChangedMessage<List<ModViewModel>>(mods)
{
    public string Directory { get; } = directory;
}
