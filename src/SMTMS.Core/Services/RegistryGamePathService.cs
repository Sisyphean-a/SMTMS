using System;
using System.IO;
using Microsoft.Win32;
using SMTMS.Core.Interfaces;

namespace SMTMS.Core.Services;

public class RegistryGamePathService : IGamePathService
{
    private const string StardewValleyAppId = "413150";

    public string? GetModsPath()
    {
        string? gamePath = GetGameInstallPath(StardewValleyAppId);

        if (!string.IsNullOrEmpty(gamePath))
        {
            return Path.Combine(gamePath, "Mods");
        }

        return null;
    }

    private string? GetGameInstallPath(string appId)
    {
        // 1. Check Uninstall Registry Key
        string registryKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}";
        
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
        string? steamPath = GetSteamPath();
        if (!string.IsNullOrEmpty(steamPath))
        {
            string potentialPath = Path.Combine(steamPath, "steamapps", "common", "Stardew Valley");
            if (Directory.Exists(potentialPath))
            {
                return potentialPath;
            }
        }

        return null;
    }

    private string? GetSteamPath()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key != null)
                {
                    string? path = key.GetValue("SteamPath")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path.Replace("/", "\\");
                    }
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
