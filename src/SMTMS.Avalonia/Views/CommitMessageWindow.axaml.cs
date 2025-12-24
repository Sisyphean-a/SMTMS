using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace SMTMS.Avalonia.Views;

public partial class CommitMessageWindow : Window
{
    public string CommitMessage { get; private set; } = string.Empty;
    public bool IsConfirmed { get; private set; } = false;

    public CommitMessageWindow()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        var messageInput = this.FindControl<TextBox>("MessageInput");
        CommitMessage = messageInput?.Text ?? string.Empty;
        IsConfirmed = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}
