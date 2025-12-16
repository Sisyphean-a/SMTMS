using CommunityToolkit.Mvvm.ComponentModel;
using SMTMS.Core.Models;


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

    public ModManifest Manifest => _manifest;

    public string? ManifestPath => _manifest.ManifestPath;

    public string Name
    {
        get => _manifest.Name;
        set
        {
            if (_manifest.Name != value)
            {
                _manifest.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Author
    {
        get => _manifest.Author;
        set
        {
            if (_manifest.Author != value)
            {
                _manifest.Author = value;
                OnPropertyChanged();
            }
        }
    }

    public string Version
    {
        get => _manifest.Version;
        set
        {
            if (_manifest.Version != value)
            {
                _manifest.Version = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => _manifest.Description;
        set
        {
            if (_manifest.Description != value)
            {
                _manifest.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public string UniqueID
    {
        get => _manifest.UniqueID;
        set
        {
            if (_manifest.UniqueID != value)
            {
                _manifest.UniqueID = value;
                OnPropertyChanged();
            }
        }
    }

    // TODO: Add properties for translation status, etc.
    [ObservableProperty]
    private string _status = "Unknown";
}
