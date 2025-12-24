using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using SMTMS.Avalonia.Messages;
using SMTMS.Avalonia.ViewModels;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace SMTMS.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider? _serviceProvider;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider) : this()
    {
        DataContext = viewModel;
        _serviceProvider = serviceProvider;
        WeakReferenceMessenger.Default.Register<OpenHistoryRequestMessage>(this, OnOpenHistoryRequest);
    }

    private void OnOpenHistoryRequest(object recipient, OpenHistoryRequestMessage message)
    {
        if (_serviceProvider == null) return;

        try
        {
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var historyVm = new ModHistoryViewModel(message.ModUniqueId, scopeFactory);
            var window = new ModHistoryWindow(historyVm);
            window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
