using System.Collections.Generic;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DevCockpit.App.Views;

/// <summary>Íconos de línea (16x16, se dibujan con trazo) para el menú lateral.</summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["calendar"] = "M3,3.5 H13 A1,1 0 0 1 14,4.5 V13 A1,1 0 0 1 13,14 H3 A1,1 0 0 1 2,13 V4.5 A1,1 0 0 1 3,3.5 Z M2,7 H14 M5,1.5 V5 M11,1.5 V5",
        ["list"] = "M5.5,4 H14 M5.5,8 H14 M5.5,12 H11 M2,4 H3 M2,8 H3 M2,12 H3",
        ["note"] = "M3.5,1.5 H9.5 L12.5,4.5 V14.5 H3.5 Z M9.5,1.5 V4.5 H12.5 M6,8 H10 M6,11 H10",
        ["star"] = "M8,1.8 L9.9,5.8 L14.2,6.3 L11,9.2 L11.9,13.5 L8,11.3 L4.1,13.5 L5,9.2 L1.8,6.3 L6.1,5.8 Z",
        ["sliders"] = "M2,4.5 H14 M2,11.5 H14 M5.5,2.5 V6.5 M10.5,9.5 V13.5",
    };

    public static readonly FuncValueConverter<string, Geometry?> ToGeometry =
        new(key => key is not null && Paths.TryGetValue(key, out var data) ? Geometry.Parse(data) : null);
}
