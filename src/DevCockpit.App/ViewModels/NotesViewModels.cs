using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;
using DevCockpit.Core.Time;

namespace DevCockpit.App.ViewModels;

/// <summary>Una nota lista para mostrar en una lista.</summary>
public sealed record NoteRow(Note Note)
{
    public string When => Note.CreatedAt.ToString("ddd dd/MM HH:mm", CultureInfo.GetCultureInfo("es-AR"));
    public string Text => Note.IsAchievement && !string.IsNullOrWhiteSpace(Note.Metric)
        ? $"{Note.Text} ({Note.Metric})"
        : Note.Text;
    public bool IsAchievement => Note.IsAchievement;
}

/// <summary>Pestaña Notas: las de los últimos 14 días, con opción de borrar.</summary>
public sealed partial class NotesViewModel(INoteStore notes, IClock clock) : ViewModelBase
{
    public ObservableCollection<NoteRow> Items { get; } = [];

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var now = clock.Now;
        var list = await notes.ListAsync(now.AddDays(-14), now, ct);
        Items.Clear();
        for (var i = list.Count - 1; i >= 0; i--) Items.Add(new NoteRow(list[i])); // más nuevas arriba
    }

    [RelayCommand]
    private async Task DeleteAsync(NoteRow row)
    {
        await notes.DeleteAsync(row.Note.Id, CancellationToken.None);
        Items.Remove(row);
    }
}

/// <summary>Pestaña Logros: lista completa y exportación a Markdown.</summary>
public sealed partial class AchievementsViewModel(INoteStore notes, IFileSaver saver) : ViewModelBase
{
    private Note[] _all = [];

    public ObservableCollection<NoteRow> Items { get; } = [];

    [ObservableProperty] public partial string? Status { get; set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _all = [.. await notes.ListAchievementsAsync(ct)];
        Items.Clear();
        foreach (var n in _all) Items.Add(new NoteRow(n));
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var markdown = "# Logros\n\n" + TemplateReportBuilder.Achievements(_all, ReportLanguage.Es);
        var name = $"logros-{DateTime.Now:yyyy-MM-dd}.md";
        Status = await saver.SaveTextAsync(name, markdown) ? "Exportado" : null;
    }
}
