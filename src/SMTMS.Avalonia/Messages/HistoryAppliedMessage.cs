using SMTMS.Core.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class HistoryAppliedMessage : ValueChangedMessage<ModManifest>
{
    public HistoryAppliedMessage(ModManifest manifest) : base(manifest) {}
}
