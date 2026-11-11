using Avalonia.Controls;
using DevCockpit.App.Views;

namespace DevCockpit.App.Tests;

public class ClosePolicyTests
{
    [Fact]
    public void The_X_button_hides_the_window_to_the_tray()
    {
        Assert.True(ClosePolicy.ShouldHideToTray(allowClose: false, WindowCloseReason.WindowClosing));
    }

    [Theory]
    [InlineData(WindowCloseReason.OSShutdown)]
    [InlineData(WindowCloseReason.ApplicationShutdown)]
    public void Shutdown_is_never_blocked(WindowCloseReason reason)
    {
        // Cancelar acá hace que Windows avise "DevCockpit.App.exe intentó impedir el cierre".
        Assert.False(ClosePolicy.ShouldHideToTray(allowClose: false, reason));
    }

    [Fact]
    public void Exit_from_the_tray_menu_closes_for_real()
    {
        Assert.False(ClosePolicy.ShouldHideToTray(allowClose: true, WindowCloseReason.WindowClosing));
    }
}
