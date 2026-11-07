using System;
using Avalonia;
using DevCockpit.App.Services;

namespace DevCockpit.App;

internal sealed class Program
{
    // No usar APIs de Avalonia antes de StartWithClassicDesktopLifetime: todavía no están inicializadas.
    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = new SingleInstance();
        if (!instance.IsFirst)
        {
            SingleInstance.SignalFirstInstance();
            return 0;
        }

        App.Instance = instance;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Lo usa también el diseñador visual de Avalonia: no borrar.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
