using System.Threading.Tasks;

namespace DevCockpit.App.Services;

// Puertos de la interfaz: los ViewModels los usan sin conocer Avalonia, así se testean sin ventanas.

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

public interface IFileSaver
{
    /// <returns>false si el usuario canceló el diálogo.</returns>
    Task<bool> SaveTextAsync(string suggestedName, string content);
}

public interface IFolderPicker
{
    /// <returns>La ruta elegida, o null si canceló.</returns>
    Task<string?> PickFolderAsync();
}
