using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DevCockpit.Infrastructure.Windows;

/// <summary>
/// Windows no deja que una app en segundo plano le robe el foco a la que se está usando: la ventana
/// aparece pero el teclado sigue yendo a la otra. Verificado con la nota rápida (Ctrl+Alt+N).
/// Para traerla al frente, el hilo propio se engancha un instante a la cola de entrada del hilo
/// que tiene el foco (AttachThreadInput); así SetForegroundWindow está permitido.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ForegroundWindow
{
    public static void Bring(IntPtr hwnd)
    {
        var foreground = GetForegroundWindow();
        if (foreground == hwnd) return;

        var foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
}
