using DevCockpit.Core.Input;

namespace DevCockpit.Core.Tests.Input;

public class HotkeyParserTests
{
    [Fact]
    public void Parses_ctrl_alt_n()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+N", out var hk));
        Assert.Equal(new Hotkey(Ctrl: true, Alt: true, Shift: false, Key: "N"), hk);
    }

    [Theory]
    [InlineData("ctrl + alt + n")]
    [InlineData("CONTROL+ALT+n")]
    [InlineData(" Alt+Ctrl+N ")]
    public void Parse_is_case_order_and_space_insensitive(string text)
    {
        Assert.True(HotkeyParser.TryParse(text, out var hk));
        Assert.Equal(new Hotkey(true, true, false, "N"), hk);
    }

    [Theory]
    [InlineData("Ctrl+Shift+F9", true, false, true, "F9")]
    [InlineData("Alt+7", false, true, false, "7")]
    public void Parses_function_keys_and_digits(string text, bool ctrl, bool alt, bool shift, string key)
    {
        Assert.True(HotkeyParser.TryParse(text, out var hk));
        Assert.Equal(new Hotkey(ctrl, alt, shift, key), hk);
    }

    [Theory]
    [InlineData("N")]          // sin modificador: se dispararía al escribir
    [InlineData("Shift+N")]    // Shift solo: igual, es escribir una mayúscula
    [InlineData("Ctrl+Alt")]   // sin tecla
    [InlineData("Ctrl+N+M")]   // dos teclas
    [InlineData("Ctrl+Ñ")]     // tecla no soportada
    [InlineData("Ctrl+F13")]
    [InlineData("cualquier cosa")]
    [InlineData("")]
    public void Rejects_invalid(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _));
    }

    [Fact]
    public void Default_is_ctrl_alt_n()
    {
        Assert.Equal(new Hotkey(true, true, false, "N"), HotkeyParser.Default);
    }
}
