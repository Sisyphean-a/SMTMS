using System.Collections.Generic;
using SMTMS.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class ModsLoadedMessage : ValueChangedMessage<List<ModViewModel>>
{
    public string Directory { get; }
    public ModsLoadedMessage(List<ModViewModel> mods, string directory) : base(mods) 
    {
        Directory = directory;
    }
}
