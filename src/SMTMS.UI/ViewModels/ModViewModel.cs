using CommunityToolkit.Mvvm.ComponentModel;
using SMTMS.Core.Models;
using SMTMS.Data.Entities;

namespace SMTMS.UI.ViewModels;

public partial class ModViewModel : ObservableObject
{
    private readonly ModManifest _manifest;
    private readonly ModMetadata? _metadata;

    public ModViewModel(ModManifest manifest, ModMetadata? metadata = null)
    {
        _manifest = manifest;
        _metadata = metadata;
    }

    public string Name => _manifest.Name;
    public string Author => _manifest.Author;
    public string Version => _manifest.Version;
    public string Description => _manifest.Description;
    public string UniqueID => _manifest.UniqueID;

    // TODO: Add properties for translation status, etc.
    [ObservableProperty]
    private string _status = "Unknown";
}
