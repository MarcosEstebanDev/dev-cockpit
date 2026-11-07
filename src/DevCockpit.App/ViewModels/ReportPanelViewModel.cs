using System;
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
    Func<ReportKind, ReportLanguage, bool, CancellationToken, Task<ReportResult>> build,
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

    // AsyncRelayCommand no permite ejecuciones simultáneas: CanExecute es false mientras corre.
    // IncludeCancelCommand genera GenerateCancelCommand, que cancela el token (botón Cancelar).
    // Los comandos atrapan todo: una excepción que escapa de un comando cierra la app entera.
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task GenerateAsync(CancellationToken ct)
    {
        IsGenerating = true;
        CopyStatus = null;
        try
        {
            var result = await build(kind, Language, UseAi, ct);
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
        }
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
