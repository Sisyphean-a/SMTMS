using System.Diagnostics;

namespace SMTMS.Avalonia.Services;

public sealed class ShellPathOpener : IPathOpener
{
    public void Open(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
