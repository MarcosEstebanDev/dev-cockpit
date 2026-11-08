using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;

namespace DevCockpit.App.ViewModels;

/// <summary>Una página de la ventana: título, para qué sirve, su ícono y el ViewModel que se muestra.</summary>
/// <param name="Icon">Clave de <see cref="Views.Icons"/>.</param>
public sealed record NavPage(string Title, string Description, string Icon, ViewModelBase Content);

public sealed partial class MainWindowViewModel : ViewModelBase
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

        LogPages =
        [
            new("Daily", "Lo que hiciste desde el último día hábil, listo para el stand-up.", "calendar", Daily),
            new("Semana", "Todo lo de esta semana, agrupado por repo.", "list", Weekly),
            new("Notas", "Lo que anotaste con el atajo en los últimos 14 días.", "note", Notes),
            new("Logros", "Lo que vale la pena contar en una evaluación o en el CV.", "star", Achievements),
        ];
        SettingsPage = new("Configuración", "Repos, IA, apariencia y atajo de teclado.", "sliders", Settings);
        _selectedLogPage = LogPages[0];
    }

    /// <summary>Las páginas de la bitácora, en el orden del menú lateral.</summary>
    public IReadOnlyList<NavPage> LogPages { get; }
    public NavPage SettingsPage { get; }

    private NavPage? _selectedLogPage;

    /// <summary>
    /// Página elegida en el menú lateral. Configuración vive aparte (abajo de todo), así que al abrirla
    /// esta queda en null: ninguna página de la bitácora aparece marcada.
    /// </summary>
    public NavPage? SelectedLogPage
    {
        get => _selectedLogPage;
        set
        {
            if (!SetProperty(ref _selectedLogPage, value)) return;
            if (value is not null) IsSettingsOpen = false;
            OnPropertyChanged(nameof(CurrentPage));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    public partial bool IsSettingsOpen { get; set; }

    public NavPage CurrentPage => IsSettingsOpen ? SettingsPage : SelectedLogPage ?? LogPages[0];

    [RelayCommand]
    private void OpenSettings()
    {
        IsSettingsOpen = true;
        SelectedLogPage = null;
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
