using Avalonia.Data.Converters;
using DevCockpit.Core.Domain;

namespace DevCockpit.App.Views;

/// <summary>Textos en español para valores del dominio que se muestran en combos.</summary>
public static class Labels
{
    public static readonly FuncValueConverter<AppTheme, string> Theme = new(t => t switch
    {
        AppTheme.Light => "Claro",
        AppTheme.Dark => "Oscuro",
        _ => "Sistema (igual que Windows)",
    });
}
