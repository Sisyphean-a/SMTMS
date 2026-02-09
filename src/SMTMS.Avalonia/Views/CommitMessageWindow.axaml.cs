using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using System;

namespace SMTMS.Avalonia.Views;

public partial class CommitMessageWindow : Window
{
    public string CommitMessage { get; private set; } = string.Empty;
    public bool IsConfirmed { get; private set; } = false;

    public CommitMessageWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var messageInput = this.FindControl<TextBox>("MessageInput");
        messageInput?.Focus();
    }

    private void Confirm()
    {
        var messageInput = this.FindControl<TextBox>("MessageInput");
        CommitMessage = messageInput?.Text ?? string.Empty;
        IsConfirmed = true;
        Close();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        Confirm();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            Confirm();
        }
    }

    private void OnBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }
}
