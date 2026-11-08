using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;
using DevCockpit.Core.Time;

namespace DevCockpit.App.Tests;

/// <summary>Revisión final I4: reabrir la ventana o guardar una nota no puede pisar lo que el usuario está editando.</summary>
public class MainWindowViewModelTests
{
    private sealed class Clock : IClock { public DateTimeOffset Now => new(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(-3)); }
    private sealed class NoGit : IGitLog, IGitIdentity
    {
        public Task<GitLogResult> GetCommitsAsync(WatchedRepo r, IReadOnlyCollection<string> e, DateTimeOffset f, DateTimeOffset t, CancellationToken ct)
            => Task.FromResult(new GitLogResult([], null));
        public Task<string?> GetGlobalEmailAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }
    private sealed class Notes : INoteStore
    {
        public List<Note> Items { get; } = [];
        public Task AddAsync(Note n, CancellationToken ct) { Items.Add(n); return Task.CompletedTask; }
        public Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset f, DateTimeOffset t, CancellationToken ct) => Task.FromResult<IReadOnlyList<Note>>(Items);
        public Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Note>>(Items.Where(n => n.IsAchievement).ToList());
        public Task DeleteAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Settings(AppSettings s) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(s);
        public Task SaveAsync(AppSettings v, CancellationToken ct) { s = v; return Task.CompletedTask; }
    }
    private sealed class Secrets : ISecretStore
    {
        public string? Get(string k) => null;
        public void Set(string k, string v) { }
        public void Delete(string k) { }
    }
    private sealed class Confirm : ICloudSendConfirmation
    {
        public Task<ConfirmationResult> ConfirmAsync(string p, string t, IReadOnlyList<string> r) => Task.FromResult(new ConfirmationResult(false, false));
    }
    private sealed class Ui : IClipboardService, IFileSaver, IFolderPicker
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
        public Task<bool> SaveTextAsync(string n, string c) => Task.FromResult(false);
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }

    private readonly Notes _notes = new();

    private MainWindowViewModel Vm()
    {
        var settings = new Settings(new AppSettings { DefaultLanguage = ReportLanguage.En });
        var git = new NoGit();
        var clock = new Clock();
        var ui = new Ui();
        var reports = new ReportService(git, _notes, settings, _ => null, new Confirm(), clock);
        return new MainWindowViewModel(reports, ui, settings,
            new NotesViewModel(_notes, clock), new AchievementsViewModel(_notes, ui),
            new SettingsViewModel(settings, new Secrets(), git, _ => null, ui));
    }

    [Fact]
    public async Task First_refresh_loads_defaults()
    {
        var vm = Vm();

        await vm.RefreshAsync();

        Assert.Equal(ReportLanguage.En, vm.Daily.Language);
        Assert.Equal("Ctrl+Alt+N", vm.Settings.Hotkey);
    }

    [Fact]
    public async Task Reopening_window_keeps_unsaved_settings_and_panel_choices()
    {
        var vm = Vm();
        await vm.RefreshAsync();
        vm.Settings.Hotkey = "Ctrl+Alt+K";      // sin guardar
        vm.Daily.Language = ReportLanguage.Es;  // elegido a mano

        await vm.RefreshAsync();

        Assert.Equal("Ctrl+Alt+K", vm.Settings.Hotkey);
        Assert.Equal(ReportLanguage.Es, vm.Daily.Language);
    }

    [Fact]
    public async Task Refreshing_lists_shows_new_notes_without_touching_settings()
    {
        var vm = Vm();
        await vm.RefreshAsync();
        vm.Settings.Hotkey = "Ctrl+Alt+K";
        _notes.Items.Add(new Note(Guid.NewGuid(), new Clock().Now, "nueva", true, null));

        await vm.RefreshListsAsync();

        Assert.Single(vm.Notes.Items);
        Assert.Single(vm.Achievements.Items);
        Assert.Equal("Ctrl+Alt+K", vm.Settings.Hotkey);
    }

    [Fact]
    public void Starts_on_daily_with_the_logbook_pages_in_order()
    {
        var vm = Vm();

        Assert.Equal(["Daily", "Semana", "Notas", "Logros"], vm.LogPages.Select(p => p.Title));
        Assert.Same(vm.Daily, vm.CurrentPage.Content);
    }

    [Fact]
    public void Settings_and_logbook_pages_replace_each_other()
    {
        var vm = Vm();

        vm.SelectedLogPage = vm.LogPages[2];
        Assert.Same(vm.Notes, vm.CurrentPage.Content);

        vm.OpenSettingsCommand.Execute(null);
        Assert.Same(vm.Settings, vm.CurrentPage.Content);
        Assert.Null(vm.SelectedLogPage); // ninguna página de la bitácora queda marcada

        vm.SelectedLogPage = vm.LogPages[3];
        Assert.Same(vm.Achievements, vm.CurrentPage.Content);
    }

    [Fact]
    public async Task Panels_start_without_ai_unless_the_setting_says_so()
    {
        var vm = Vm();

        await vm.RefreshAsync();

        Assert.False(vm.Daily.UseAi);
        Assert.False(vm.Weekly.UseAi);
    }
}
