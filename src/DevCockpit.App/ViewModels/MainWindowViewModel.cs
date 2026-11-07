using System.Threading.Tasks;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;

namespace DevCockpit.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;

    public MainWindowViewModel(ReportService reports, IClipboardService clipboard, ISettingsStore settings,
        NotesViewModel notes, AchievementsViewModel achievements, SettingsViewModel settingsVm)
    {
        _settings = settings;
        Daily = new ReportPanelViewModel(ReportKind.Daily, reports.BuildAsync, clipboard, ReportLanguage.Es);
        Weekly = new ReportPanelViewModel(ReportKind.Weekly, reports.BuildAsync, clipboard, ReportLanguage.Es);
        Notes = notes;
        Achievements = achievements;
        Settings = settingsVm;
    }

    public string Title => "dev-cockpit";
    public ReportPanelViewModel Daily { get; }
    public ReportPanelViewModel Weekly { get; }
    public NotesViewModel Notes { get; }
    public AchievementsViewModel Achievements { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>Recarga listas y configuración cada vez que se muestra la ventana.</summary>
    public async Task RefreshAsync()
    {
        var s = await _settings.LoadAsync(default);
        Daily.Language = s.DefaultLanguage;
        Weekly.Language = s.DefaultLanguage;
        Daily.UseAi = Weekly.UseAi = s.Provider != LlmProviderKind.None;
        await Notes.LoadAsync();
        await Achievements.LoadAsync();
        await Settings.LoadAsync();
    }
}
