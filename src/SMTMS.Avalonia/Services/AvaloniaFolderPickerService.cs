using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using SMTMS.Avalonia.Services;

namespace SMTMS.Avalonia.Services;

public class AvaloniaFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow?.StorageProvider != null)
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = "选择模组文件夹 (Select Mods Directory)",
                    AllowMultiple = false
                };
                
                var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(options);
                if (result.Count > 0)
                {
                    // For local file system, LocalPath is appropriate
                    return result[0].Path.LocalPath;
                }
            }
        }
        return null;
    }
}
