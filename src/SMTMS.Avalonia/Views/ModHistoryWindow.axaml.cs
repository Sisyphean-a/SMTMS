using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SMTMS.Avalonia.ViewModels;
using System;

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
