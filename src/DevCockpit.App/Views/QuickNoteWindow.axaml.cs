using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevCockpit.App.ViewModels;
using DevCockpit.Infrastructure.Windows;

namespace DevCockpit.App.Views;

public partial class QuickNoteWindow : Window
{
    public QuickNoteWindow()
    {
        InitializeComponent();
        // Tunnel: la ventana ve el Enter antes que el TextBox, que si no lo convertiría en salto de línea.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => BringToFront();
    }

    /// <summary>
    /// Al abrirse y también si se aprieta el atajo con la nota ya abierta: Activate() solo no alcanza,
    /// Windows deja el teclado en la app que estabas usando (ver ForegroundWindow).
    /// </summary>
    public void BringToFront()
    {
        Activate();
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
            ForegroundWindow.Bring(handle.Handle);
        NoteText.Focus();
    }

    private QuickNoteViewModel? Vm => DataContext as QuickNoteViewModel;

    private async void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Vm is { } vm)
        {
            e.Handled = true;
            await vm.SaveCommand.ExecuteAsync(null);
        }
    }
}
