using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DevCockpit.App;

public partial class App : Application
{
    /// <summary>La instancia única creada en Program.Main (null en el diseñador visual).</summary>
    public static SingleInstance? Instance { get; set; }

    public static ServiceProvider Services { get; private set; } = null!;

    private MainWindow? _window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = Composition.Build();
            // La app vive en la bandeja: cerrar la ventana no la termina.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Services.Dispose();

            _window = new MainWindow { DataContext = Services.GetRequiredService<MainWindowViewModel>() };
            SetupTray(desktop);
            Instance?.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));

            // Con --minimized (para el inicio de Windows) arranca solo en la bandeja.
            if (Array.IndexOf(desktop.Args ?? [], "--minimized") < 0) ShowMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowMainWindow()
    {
        if (_window is null) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Abrir");
        open.Click += (_, _) => ShowMainWindow();

        var exit = new NativeMenuItem("Salir");
        exit.Click += (_, _) =>
        {
            if (_window is not null) _window.AllowClose = true;
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DevCockpit.App/Assets/avalonia-logo.ico"))),
            ToolTipText = "dev-cockpit",
            Menu = menu,
        };
        tray.Clicked += (_, _) => ShowMainWindow();
        TrayIcon.SetIcons(this, [tray]);
    }
}
