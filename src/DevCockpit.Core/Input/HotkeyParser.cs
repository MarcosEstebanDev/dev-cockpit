namespace DevCockpit.Core.Input;

/// <param name="Key">Una letra A-Z, un dígito 0-9 o F1-F12, en mayúsculas.</param>
public sealed record Hotkey(bool Ctrl, bool Alt, bool Shift, string Key)
{
    public override string ToString()
        => string.Join("+", new[] { Ctrl ? "Ctrl" : null, Alt ? "Alt" : null, Shift ? "Shift" : null, Key }
            .Where(p => p is not null));
}

public static class HotkeyParser
{
    public static Hotkey Default { get; } = new(Ctrl: true, Alt: true, Shift: false, Key: "N");

    /// <summary>
    /// Acepta textos como "Ctrl+Alt+N" (sin importar mayúsculas, espacios ni orden).
    /// Exige Ctrl o Alt: con Shift solo o sin modificador, el atajo se dispararía al escribir.
    /// </summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        bool ctrl = false, alt = false, shift = false;
        string? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": ctrl = true; break;
                case "ALT": alt = true; break;
                case "SHIFT": shift = true; break;
                case var k when IsSupportedKey(k):
                    if (key is not null) return false; // dos teclas
                    key = k;
                    break;
                default: return false;
            }
        }

        if (key is null || !(ctrl || alt)) return false;
        hotkey = new Hotkey(ctrl, alt, shift, key);
        return true;
    }

    private static bool IsSupportedKey(string k) =>
        (k.Length == 1 && (k[0] is >= 'A' and <= 'Z' or >= '0' and <= '9'))
        || (k.Length is 2 or 3 && k[0] == 'F' && int.TryParse(k[1..], out var n) && n is >= 1 and <= 12);
}
