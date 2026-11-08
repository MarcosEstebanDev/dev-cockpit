using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.App.Views;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Input;
using DevCockpit.Core.Ports;
using DevCockpit.Infrastructure.Llm;
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
            // Red de seguridad: una excepción no atrapada en la UI se registra y la app sigue viva.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                ErrorLog.Write("UI", e.Exception);
                e.Handled = true;
            };

            Services = Composition.Build(() => _window);
            // La app vive en la bandeja: cerrar la ventana no la termina.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => Services.Dispose();

            _window = new MainWindow { DataContext = Services.GetRequiredService<MainWindowViewModel>() };
            SetupTray(desktop);
            _ = ErrorLog.Observe(ApplyThemeAsync(), "tema");
            _ = ErrorLog.Observe(WarmUpLocalAiAsync(), "precarga de Ollama");
            _ = ErrorLog.Observe(StartHotkeyAsync(), "atajo global", HotkeyFailed);
            Instance?.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));

            // Con --minimized (para el inicio de Windows) arranca solo en la bandeja.
            if (Array.IndexOf(desktop.Args ?? [], "--minimized") < 0) ShowMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowMainWindow()
    {
        if (_window is null) return;
        _ = ErrorLog.Observe(Services.GetRequiredService<MainWindowViewModel>().RefreshAsync(), "refrescar ventana");
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private GlobalHotkey? _hotkey;
    private TrayIcon? _tray;
    private QuickNoteWindow? _quickNote;

    private static Hotkey ParseOrDefault(string text) => HotkeyParser.TryParse(text, out var hk) ? hk : HotkeyParser.Default;

    private async Task StartHotkeyAsync()
    {
        var settings = await Services.GetRequiredService<ISettingsStore>().LoadAsync(default);
        _hotkey = new GlobalHotkey(ParseOrDefault(settings.Hotkey), ShowQuickNote, HotkeyFailed);
        Services.GetRequiredService<SettingsViewModel>().Saved += s => _hotkey.Change(ParseOrDefault(s.Hotkey));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Exit += (_, _) => _hotkey.Dispose();
    }

    /// <summary>Sin atajo la captura rápida no existe: se avisa en el ícono y en Configuración.</summary>
    private void HotkeyFailed(Exception e)
    {
        var message = "Atajo no disponible: " + e.Message;
        if (_tray is not null) _tray.ToolTipText = "dev-cockpit · " + message;
        Services.GetRequiredService<SettingsViewModel>().HotkeyWarning = message;
    }

    public void ShowQuickNote()
    {
        if (_quickNote is not null)
        {
            _quickNote.BringToFront();
            return;
        }
        var vm = Services.GetRequiredService<QuickNoteViewModel>();
        _quickNote = new QuickNoteWindow { DataContext = vm };
        void OnClose() => _quickNote?.Close();
        void OnSaved()
        {
            // Solo las listas: recargar todo pisaba la configuración sin guardar.
            if (_window?.IsVisible == true)
                _ = ErrorLog.Observe(Services.GetRequiredService<MainWindowViewModel>().RefreshListsAsync(), "refrescar listas");
        }
        vm.CloseRequested += OnClose;
        vm.Saved += OnSaved;
        _quickNote.Closed += (_, _) =>
        {
            vm.CloseRequested -= OnClose;
            vm.Saved -= OnSaved;
            _quickNote = null;
        };
        _quickNote.Show();
        _quickNote.Activate();
    }

    private async Task ApplyThemeAsync()
    {
        var settings = await Services.GetRequiredService<ISettingsStore>().LoadAsync(default);
        ApplyTheme(settings.Theme);
        Services.GetRequiredService<SettingsViewModel>().Saved += s => ApplyTheme(s.Theme);
    }

    /// <summary>Cambia todas las ventanas en vivo: Fluent redefine sus colores por variante de tema.</summary>
    private void ApplyTheme(AppTheme theme) => RequestedThemeVariant = theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>Con Ollama, carga el modelo en segundo plano: en un HDD tarda minutos y así no se espera al generar.</summary>
    private static async Task WarmUpLocalAiAsync()
    {
        try
        {
            var settings = await Services.GetRequiredService<ISettingsStore>().LoadAsync(default);
            if (Services.GetRequiredService<LlmProviderFactory>().Create(settings) is OllamaProvider ollama)
                await ollama.WarmUpAsync(default);
        }
        catch (LlmException) { } // sin proveedor válido no hay nada que precargar
    }

    private void SetupTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Abrir");
        open.Click += (_, _) => ShowMainWindow();

        var note = new NativeMenuItem("Nota rápida");
        note.Click += (_, _) => ShowQuickNote();

        var exit = new NativeMenuItem("Salir");
        exit.Click += (_, _) =>
        {
            if (_window is not null) _window.AllowClose = true;
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(note);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);

        var tray = _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DevCockpit.App/Assets/avalonia-logo.ico"))),
            ToolTipText = "dev-cockpit",
            Menu = menu,
        };
        tray.Clicked += (_, _) => ShowMainWindow();
        TrayIcon.SetIcons(this, [tray]);
    }
}
