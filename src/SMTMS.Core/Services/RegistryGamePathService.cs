using Microsoft.Win32;
using SMTMS.Core.Interfaces;

namespace SMTMS.Core.Services;

public class RegistryGamePathService : IGamePathService
{
    private const string StardewValleyAppId = "413150";

    public string? GetModsPath()
    {
        var gamePath = GetGameInstallPath(StardewValleyAppId);

        return !string.IsNullOrEmpty(gamePath) ? Path.Combine(gamePath, "Mods") : null;
    }

    private string? GetGameInstallPath(string appId)
    {
        // 1. Check Uninstall Registry Key
        var registryKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}";
        
        // Handle 64-bit systems naturally via RegistryView if needed, 
        // but typically uninstall keys are in common locations.
        // Dotnet's RegistryKey automatically handles basic view redirection or we can be specific.
        // We'll follow the robust pattern of checking LocalMachine.
        
        try 
        {
            // Try default view first (usually 64-bit on 64-bit OS)
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
            using (var key = baseKey.OpenSubKey(registryKeyPath))
            {
                if (key != null)
                {
                    var location = key.GetValue("InstallLocation")?.ToString();
                    if (!string.IsNullOrEmpty(location) && Directory.Exists(location))
                    {
                        return location;
                    }
                }
            }

            // Try 32-bit view explictly if failed (Steam is 32-bit app)
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = baseKey.OpenSubKey(registryKeyPath))
            {
               if (key != null)
               {
                   var location = key.GetValue("InstallLocation")?.ToString();
                   if (!string.IsNullOrEmpty(location) && Directory.Exists(location))
                   {
                       return location;
                   }
               }
            }
        } 
        catch 
        {
            // Ignore registry access errors
        }

        // 2. Fallback: Check Steam Path
        var steamPath = GetSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return null;
        var potentialPath = Path.Combine(steamPath, "steamapps", "common", "Stardew Valley");
        return Directory.Exists(potentialPath) ? potentialPath : null;
    }

    private string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
            {
                var path = key.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(path))
                {
                    // 规范化路径，让 .NET 自动处理分隔符
                    return Path.GetFullPath(path);
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }
}
