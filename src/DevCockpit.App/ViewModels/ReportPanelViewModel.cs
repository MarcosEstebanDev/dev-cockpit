using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Reports;

namespace DevCockpit.App.ViewModels;

/// <summary>Una pestaña de resumen (Daily o Semanal): elegir idioma, generar, editar y copiar.</summary>
public sealed partial class ReportPanelViewModel(
    ReportKind kind,
    Func<ReportKind, ReportLanguage, bool, IProgress<string>?, CancellationToken, Task<ReportResult>> build,
    IClipboardService clipboard,
    ReportLanguage defaultLanguage) : ViewModelBase
{
    public ReportKind Kind => kind;
    public string Header => kind == ReportKind.Daily ? "Daily" : "Semanal";
    public ReportLanguage[] Languages { get; } = [ReportLanguage.Es, ReportLanguage.En];

    [ObservableProperty] public partial ReportLanguage Language { get; set; } = defaultLanguage;
    [ObservableProperty] public partial bool UseAi { get; set; } = true;
    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial string? Warning { get; set; }
    [ObservableProperty] public partial bool IsGenerating { get; set; }
    [ObservableProperty] public partial string? CopyStatus { get; set; }

    /// <summary>"Generando… 1:12" mientras corre; null cuando no hay nada generándose.</summary>
    [ObservableProperty] public partial string? Progress { get; set; }

    private int _run;

    // AsyncRelayCommand no permite ejecuciones simultáneas: CanExecute es false mientras corre.
    // IncludeCancelCommand genera GenerateCancelCommand, que cancela el token (botón Cancelar).
    // Los comandos atrapan todo: una excepción que escapa de un comando cierra la app entera.
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateAsync(CancellationToken ct)
    {
        IsGenerating = true;
        CopyStatus = null;
        var run = ++_run;
        var clock = Stopwatch.StartNew();
        Progress = FormatProgress(clock.Elapsed);
        using var ticking = new CancellationTokenSource();
        _ = TickAsync(clock, ticking.Token);

        // Progress<T> captura el hilo de la UI al crearse: cada pedazo se publica en el dispatcher.
        // Si llega después del resultado final (otra ejecución o un pedazo atrasado), se descarta.
        var partial = new Progress<string>(t => { if (run == _run && IsGenerating) Text = t; });
        try
        {
            var result = await build(kind, Language, UseAi, partial, ct);
            IsGenerating = false;
            Text = result.Text;
            Warning = result.Warning;
        }
        catch (OperationCanceledException)
        {
            Warning = "Generación cancelada";
        }
        catch (Exception e)
        {
            Warning = "No se pudo generar: " + e.Message;
        }
        finally
        {
            IsGenerating = false;
            ticking.Cancel();
            Progress = null;
        }
    }

    private static string FormatProgress(TimeSpan elapsed)
        => $"Generando… {(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";

    private async Task TickAsync(Stopwatch clock, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct)) Progress = FormatProgress(clock.Elapsed);
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        try
        {
            await clipboard.SetTextAsync(Text);
            CopyStatus = "Copiado";
        }
        catch (Exception e)
        {
            CopyStatus = "No se pudo copiar: " + e.Message;
        }
    }
}
