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

    [RelayCommand]
    private async Task SaveAsync()
    {
        var text = Text.Trim();
        if (text.Length == 0) return;

        var metric = IsAchievement && !string.IsNullOrWhiteSpace(Metric) ? Metric.Trim() : null;
        await notes.AddAsync(new Note(Guid.NewGuid(), clock.Now, text, IsAchievement, metric), CancellationToken.None);

        Text = "";
        IsAchievement = false;
        Metric = "";
        Saved?.Invoke();
        CloseRequested?.Invoke();
    }
}
