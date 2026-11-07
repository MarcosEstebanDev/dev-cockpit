using System;
using Avalonia.Threading;
using DevCockpit.Core.Input;
using SharpHook;
using SharpHook.Data;

namespace DevCockpit.App.Services;

/// <summary>
/// Atajo de teclado de todo el sistema (funciona con el foco en cualquier app).
/// SharpHook escucha el teclado en un hilo propio; el callback se pasa al hilo de la interfaz.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private readonly SimpleGlobalHook _hook = new();
    private readonly Action _onPressed;
    private Hotkey _hotkey;
    private KeyCode _keyCode;

    /// <param name="onFailed">Si el hook no se puede instalar o se cae (por ejemplo, por un antivirus).</param>
    public GlobalHotkey(Hotkey hotkey, Action onPressed, Action<Exception> onFailed)
    {
        _onPressed = onPressed;
        _hotkey = hotkey;
        _keyCode = ToKeyCode(hotkey);
        _hook.KeyPressed += OnKeyPressed;
        _ = ErrorLog.Observe(_hook.RunAsync(), "atajo global",
            e => Dispatcher.UIThread.Post(() => onFailed(e)));
    }

    public void Change(Hotkey hotkey)
    {
        _hotkey = hotkey;
        _keyCode = ToKeyCode(hotkey);
    }

    private static KeyCode ToKeyCode(Hotkey hk)
        => Enum.TryParse<KeyCode>("Vc" + hk.Key, ignoreCase: false, out var code) ? code : KeyCode.VcN;

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode != _keyCode) return;
        var mask = e.RawEvent.Mask;
        if (mask.HasCtrl() != _hotkey.Ctrl || mask.HasAlt() != _hotkey.Alt || mask.HasShift() != _hotkey.Shift) return;

        e.SuppressEvent = true; // que la "N" no llegue a la app que tiene el foco
        Dispatcher.UIThread.Post(_onPressed);
    }

    public void Dispose()
    {
        _hook.KeyPressed -= OnKeyPressed;
        _hook.Dispose();
    }
}
