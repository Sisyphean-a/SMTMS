using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace SMTMS.Avalonia.Services;

public class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        var mainWindow = desktop.MainWindow;
        if (mainWindow?.StorageProvider == null) return null;
        var options = new FolderPickerOpenOptions
        {
            Title = "选择模组文件夹 (Select Mods Directory)",
            AllowMultiple = false
        };
                
        var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ?
            // For local file system, LocalPath is appropriate
            result[0].Path.LocalPath : null;
    }
}
