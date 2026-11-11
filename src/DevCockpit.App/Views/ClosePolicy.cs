using Avalonia.Controls;

namespace DevCockpit.App.Views;

/// <summary>Decide si cerrar la ventana la oculta en la bandeja o la cierra de verdad.</summary>
public static class ClosePolicy
{
    /// <summary>
    /// Solo la X (o un Close() del código) manda la ventana a la bandeja. Un apagado de Windows o de la
    /// app nunca se cancela: si se cancela, Windows frena el apagado con "intentó impedir el cierre".
    /// </summary>
    public static bool ShouldHideToTray(bool allowClose, WindowCloseReason reason) =>
        !allowClose && reason is not (WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown);
}
