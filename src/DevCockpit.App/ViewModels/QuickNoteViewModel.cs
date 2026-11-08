using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Time;

namespace DevCockpit.App.ViewModels;

/// <summary>La ventanita del atajo global: escribir, Enter, listo.</summary>
public sealed partial class QuickNoteViewModel(INoteStore notes, IClock clock) : ViewModelBase
{
    /// <summary>Se dispara cuando la nota se guardó y la ventana tiene que cerrarse.</summary>
    public event Action? CloseRequested;

    /// <summary>Se dispara después de guardar, para refrescar las listas de la ventana principal.</summary>
    public event Action? Saved;

    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial bool IsAchievement { get; set; }
    [ObservableProperty] public partial string Metric { get; set; } = "";
    [ObservableProperty] public partial string? Error { get; set; }

    private bool _saving;

    [RelayCommand]
    private async Task SaveAsync()
    {
        // El Enter se maneja a mano en la ventana y ExecuteAsync no mira CanExecute:
        // un segundo Enter mientras SQLite guarda duplicaría la nota.
        if (_saving) return;
        var text = Text.Trim();
        if (text.Length == 0) return;
        _saving = true;
        try
        {
            await SaveCoreAsync(text);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task SaveCoreAsync(string text)
    {

        var metric = IsAchievement && !string.IsNullOrWhiteSpace(Metric) ? Metric.Trim() : null;
        try
        {
            await notes.AddAsync(new Note(Guid.NewGuid(), clock.Now, text, IsAchievement, metric), CancellationToken.None);
        }
        catch (Exception e)
        {
            // La ventana queda abierta con el texto: no se pierde lo escrito.
            Error = "No se pudo guardar: " + e.Message;
            return;
        }

        Error = null;
        Text = "";
        IsAchievement = false;
        Metric = "";
        Saved?.Invoke();
        CloseRequested?.Invoke();
    }
}
