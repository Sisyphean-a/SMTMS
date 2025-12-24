using Avalonia;
using System;
using System.IO;

namespace SMTMS.Avalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            var writer = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true
            };
            Console.SetOut(writer);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"无法重定向控制台输出: {ex.Message}");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
