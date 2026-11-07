using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace DevCockpit.App.Services;

/// <summary>Implementaciones con Avalonia de los puertos de interfaz. Necesitan la ventana activa.</summary>
public sealed class AvaloniaUiServices(Func<TopLevel?> topLevel) : IClipboardService, IFileSaver, IFolderPicker
{
    private TopLevel Top => topLevel() ?? throw new InvalidOperationException("La ventana no está abierta");

    public async Task SetTextAsync(string text)
    {
        if (Top.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    public async Task<bool> SaveTextAsync(string suggestedName, string content)
    {
        var file = await Top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
        });
        if (file is null) return false;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        return true;
    }

    public async Task<string?> PickFolderAsync()
    {
        var folders = await Top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Elegí la carpeta del repo",
            AllowMultiple = false,
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
