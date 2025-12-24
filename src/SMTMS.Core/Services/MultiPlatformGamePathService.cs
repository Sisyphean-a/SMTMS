using System;
using System.IO;
using System.Runtime.InteropServices;
using SMTMS.Core.Interfaces;

namespace SMTMS.Core.Services;

/// <summary>
/// 多平台游戏路径服务，支持 Windows、Linux 和 macOS
/// </summary>
public class MultiPlatformGamePathService : IGamePathService
{
    private const string StardewValleyAppId = "413150";

    public string? GetModsPath()
    {
        string? gamePath = GetGameInstallPath();

        if (!string.IsNullOrEmpty(gamePath))
        {
            return Path.Combine(gamePath, "Mods");
        }

        return null;
    }

    private string? GetGameInstallPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsGamePath();
        }
        else if (OperatingSystem.IsLinux())
        {
            return GetLinuxGamePath();
        }
        else if (OperatingSystem.IsMacOS())
        {
            return GetMacOSGamePath();
        }

        return null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private string? GetWindowsGamePath()
    {
        // 1. 尝试从注册表读取（仅在 Windows 上可用）
        try
        {
            // 使用反射调用 RegistryGamePathService 的逻辑
            // 这样可以避免在非 Windows 平台上引用 Microsoft.Win32.Registry
            var registryType = Type.GetType("Microsoft.Win32.Registry, Microsoft.Win32.Registry");
            if (registryType != null)
            {
                var path = TryGetFromWindowsRegistry();
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
        }
        catch
        {
            // 忽略注册表访问错误
        }

        // 2. 检查常见的 Steam 安装路径
        var commonPaths = new[]
        {
            Path.Combine("C:", "Program Files (x86)", "Steam", "steamapps", "common", "Stardew Valley"),
            Path.Combine("C:", "Program Files", "Steam", "steamapps", "common", "Stardew Valley"),
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        // 3. 检查用户目录下的 Steam
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userSteamPath = Path.Combine(userProfile, "Steam", "steamapps", "common", "Stardew Valley");
        if (Directory.Exists(userSteamPath))
        {
            return userSteamPath;
        }

        return null;
    }

    private string? GetLinuxGamePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // 常见的 Linux Steam 路径
        var linuxPaths = new[]
        {
            Path.Combine(home, ".steam", "steam", "steamapps", "common", "Stardew Valley"),
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Stardew Valley"),
            Path.Combine(home, "Steam", "steamapps", "common", "Stardew Valley"),
        };

        foreach (var path in linuxPaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private string? GetMacOSGamePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // macOS Steam 路径
        var macPaths = new[]
        {
            Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "Stardew Valley"),
            Path.Combine(home, ".steam", "steam", "steamapps", "common", "Stardew Valley"),
        };

        foreach (var path in macPaths)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private string? TryGetFromWindowsRegistry()
    {
        // 这个方法只在 Windows 上调用
        // 为了避免在其他平台上引用 Windows 特定的 API，我们使用条件编译
#if WINDOWS
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
            {
                string? steamPath = key.GetValue("SteamPath")?.ToString();
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var gamePath = Path.Combine(steamPath, "steamapps", "common", "Stardew Valley");
                    if (Directory.Exists(gamePath))
                    {
                        return gamePath;
                    }
                }
            }
        }
        catch
        {
            // 忽略
        }
#endif
        return null;
    }
}

