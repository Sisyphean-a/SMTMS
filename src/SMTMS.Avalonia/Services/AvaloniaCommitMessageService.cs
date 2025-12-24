using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SMTMS.Avalonia.Views;

namespace SMTMS.Avalonia.Services;

public class AvaloniaCommitMessageService : ICommitMessageService
{
    public async Task<string?> ShowDialogAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var dialog = new CommitMessageWindow();
            await dialog.ShowDialog(desktop.MainWindow);
            return dialog.IsConfirmed ? dialog.CommitMessage : null;
        }
        return null;
    }
}
