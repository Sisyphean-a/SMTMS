using CommunityToolkit.Mvvm.ComponentModel;
using SMTMS.Core.Interfaces;

namespace SMTMS.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IModService _modService;

    [ObservableProperty]
    private string _applicationTitle = "SMTMS - Stardew Mod Translation & Management System";

    public MainViewModel(IModService modService)
    {
        _modService = modService;
    }
}
