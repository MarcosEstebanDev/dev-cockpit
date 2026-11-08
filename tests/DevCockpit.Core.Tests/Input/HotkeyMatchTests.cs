using DevCockpit.Core.Input;

namespace DevCockpit.Core.Tests.Input;

public class HotkeyMatchTests
{
    private static readonly Hotkey CtrlAltQ = new(Ctrl: true, Alt: true, Shift: false, Key: "Q");

    [Fact]
    public void Left_ctrl_and_left_alt_match()
    {
        Assert.True(CtrlAltQ.Matches(new ModifierKeys(LeftCtrl: true, LeftAlt: true)));
    }

    [Fact]
    public void Right_ctrl_and_right_alt_match()
    {
        Assert.True(CtrlAltQ.Matches(new ModifierKeys(RightCtrl: true, RightAlt: true)));
    }

    [Fact]
    public void AltGr_is_not_ctrl_alt()
    {
        // Minor 8: Windows manda AltGr como Ctrl izquierdo + Alt derecho. Con teclado español,
        // AltGr+Q es "@": si el atajo fuera Ctrl+Alt+Q, se comería la arroba.
        Assert.False(CtrlAltQ.Matches(new ModifierKeys(LeftCtrl: true, RightAlt: true)));
    }

    [Fact]
    public void Missing_or_extra_modifiers_do_not_match()
    {
        Assert.False(CtrlAltQ.Matches(new ModifierKeys(LeftCtrl: true)));
        Assert.False(CtrlAltQ.Matches(new ModifierKeys(LeftCtrl: true, LeftAlt: true, Shift: true)));
    }

    [Fact]
    public void Ctrl_only_hotkey_ignores_altgr()
    {
        var ctrlF9 = new Hotkey(Ctrl: true, Alt: false, Shift: false, Key: "F9");
        Assert.True(ctrlF9.Matches(new ModifierKeys(LeftCtrl: true)));
        Assert.False(ctrlF9.Matches(new ModifierKeys(LeftCtrl: true, RightAlt: true)));
    }
}
