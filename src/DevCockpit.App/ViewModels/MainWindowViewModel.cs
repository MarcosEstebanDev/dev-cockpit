using System.Threading.Tasks;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;

namespace DevCockpit.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsStore _settings;
    private bool _initialized;

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

    /// <summary>
    /// Al mostrar la ventana. La configuración y los valores por defecto se cargan solo la primera vez:
    /// recargarlos cada vez borraba lo que el usuario tenía editado y sin guardar.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (!_initialized)
        {
            var s = await _settings.LoadAsync(default);
            Daily.Language = s.DefaultLanguage;
            Weekly.Language = s.DefaultLanguage;
            Daily.UseAi = Weekly.UseAi = s.Provider != LlmProviderKind.None;
            await Settings.LoadAsync();
            _initialized = true;
        }
        await RefreshListsAsync();
    }

    /// <summary>Solo las listas (después de una nota rápida, por ejemplo).</summary>
    public async Task RefreshListsAsync()
    {
        await Notes.LoadAsync();
        await Achievements.LoadAsync();
    }
}
