using Avalonia.Controls;
using SMTMS.Avalonia.ViewModels;

namespace SMTMS.Avalonia.Views;

public partial class ModHistoryWindow : Window
{
    public ModHistoryWindow()
    {
        InitializeComponent();
    }

    public ModHistoryWindow(ModHistoryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        // Listen to VM events
        viewModel.OnCloseRequest += () => Close();
    }
}
