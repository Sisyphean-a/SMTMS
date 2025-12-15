using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SMTMS.Core.Interfaces;

namespace SMTMS.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;

    [ObservableProperty]
    private string _applicationTitle = "SMTMS - Stardew Mod Translation & Management System";

    [ObservableProperty]
    private string _modsDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods"; // Default path, can be configurable

    public ObservableCollection<ModViewModel> Mods { get; } = new();

    public MainViewModel(IModService modService)
    {
        _modService = modService;
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        Mods.Clear();
        var manifests = await _modService.ScanModsAsync(ModsDirectory);
        
        foreach (var manifest in manifests)
        {
            Mods.Add(new ModViewModel(manifest));
        }
    }
}
