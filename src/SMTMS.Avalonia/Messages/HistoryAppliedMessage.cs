using SMTMS.Core.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SMTMS.Avalonia.Messages;

public class HistoryAppliedMessage(ModManifest manifest) : ValueChangedMessage<ModManifest>(manifest);
