using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Reports;
using DevCockpit.Core.Time;

namespace DevCockpit.Core.Tests.Reports;

public class ReportServiceTests
{
    private static readonly TimeSpan Ar = TimeSpan.FromHours(-3);
    private static readonly DateTimeOffset Monday9 = new(2026, 11, 9, 9, 0, 0, Ar);
    private readonly CancellationToken _ct = CancellationToken.None;

    // ---------- fakes ----------

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class FakeGit : IGitLog
    {
        public Dictionary<string, GitLogResult> ByRepo { get; } = [];
        public List<(DateTimeOffset From, DateTimeOffset To)> Calls { get; } = [];

        public Task<GitLogResult> GetCommitsAsync(WatchedRepo repo, IReadOnlyCollection<string> emails,
            DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            Calls.Add((from, to));
            return Task.FromResult(ByRepo.GetValueOrDefault(repo.Name, new GitLogResult([], null)));
        }
    }

    private sealed class FakeNotes : INoteStore
    {
        public List<Note> Notes { get; } = [];
        public Task AddAsync(Note n, CancellationToken ct) { Notes.Add(n); return Task.CompletedTask; }
        public Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.CreatedAt >= from && n.CreatedAt <= to).ToList());
        public Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Note>>(Notes.Where(n => n.IsAchievement).ToList());
        public Task DeleteAsync(Guid id, CancellationToken ct) { Notes.RemoveAll(n => n.Id == id); return Task.CompletedTask; }
    }

    private sealed class FakeSettings(AppSettings initial) : ISettingsStore
    {
        public AppSettings Current { get; private set; } = initial;
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Current);
        public Task SaveAsync(AppSettings s, CancellationToken ct) { Current = s; return Task.CompletedTask; }
    }

    private sealed class FakeLlm(bool isLocal) : ILlmProvider
    {
        public string? LastPrompt { get; private set; }
        public Exception? Throw { get; set; }
        public string Answer { get; set; } = "texto de la IA";
        public string DisplayName => isLocal ? "Ollama (test)" : "Claude (test)";
        public bool IsLocal => isLocal;

        public Task<string> CompleteAsync(string prompt, CancellationToken ct)
        {
            LastPrompt = prompt;
            return Throw is null ? Task.FromResult(Answer) : Task.FromException<string>(Throw);
        }
    }

    private sealed class FakeConfirm(ConfirmationResult answer) : ICloudSendConfirmation
    {
        public int Calls { get; private set; }
        public string? LastPayload { get; private set; }
        public IReadOnlyList<string>? LastRepos { get; private set; }

        public Task<ConfirmationResult> ConfirmAsync(string provider, string payload, IReadOnlyList<string> repos)
        {
            Calls++;
            LastPayload = payload;
            LastRepos = repos;
            return Task.FromResult(answer);
        }
    }

    // ---------- armado ----------

    private readonly FakeGit _git = new();
    private readonly FakeNotes _notes = new();
    private readonly FakeSettings _settings = new(new AppSettings
    {
        Repos = [new WatchedRepo(@"C:\api", "api"), new WatchedRepo(@"C:\web", "web")],
        AuthorEmails = ["yo@mail.com"],
    });

    private ReportService Service(ILlmProvider? llm = null, FakeConfirm? confirm = null, DateTimeOffset? now = null,
        Func<AppSettings, ILlmProvider?>? factory = null)
        => new(_git, _notes, _settings, factory ?? (_ => llm), confirm ?? new FakeConfirm(new(true, false)),
            new FakeClock(now ?? Monday9));

    private void CommitIn(string repo, string subject)
        => _git.ByRepo[repo] = new GitLogResult(
            [new Commit(repo, Guid.NewGuid().ToString("N"), "yo@mail.com", Monday9.AddDays(-3).AddHours(5), subject)], null);

    // ---------- tests ----------

    [Fact]
    public async Task Daily_on_monday_reads_friday()
    {
        await Service().BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: false, _ct);

        Assert.All(_git.Calls, c =>
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 6, 0, 0, 0, Ar), c.From);
            Assert.Equal(Monday9, c.To);
        });
    }

    [Fact]
    public async Task Weekly_starts_on_monday_of_this_week()
    {
        var thursday = new DateTimeOffset(2026, 11, 12, 18, 0, 0, Ar);

        await Service(now: thursday).BuildAsync(ReportKind.Weekly, ReportLanguage.Es, useAi: false, _ct);

        Assert.All(_git.Calls, c => Assert.Equal(new DateTimeOffset(2026, 11, 9, 0, 0, 0, Ar), c.From));
    }

    [Fact]
    public async Task Without_ai_returns_template()
    {
        CommitIn("api", "fix: algo");
        var llm = new FakeLlm(isLocal: true);

        var result = await Service(llm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: false, _ct);

        Assert.False(result.UsedAi);
        Assert.Null(result.Warning);
        Assert.Contains("- [api] fix: algo", result.Text);
        Assert.Null(llm.LastPrompt);
    }

    [Fact]
    public async Task Local_provider_never_asks_confirmation()
    {
        CommitIn("api", "fix: algo");
        var confirm = new FakeConfirm(new(false, false));

        var result = await Service(new FakeLlm(isLocal: true), confirm)
            .BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.Equal(0, confirm.Calls);
        Assert.True(result.UsedAi);
        Assert.Equal("texto de la IA", result.Text);
    }

    [Fact]
    public async Task Cloud_provider_asks_with_exact_payload()
    {
        CommitIn("api", "fix: algo");
        var llm = new FakeLlm(isLocal: false);
        var confirm = new FakeConfirm(new(true, false));

        await Service(llm, confirm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.Equal(1, confirm.Calls);
        Assert.Equal(llm.LastPrompt, confirm.LastPayload); // lo que se muestra es exactamente lo que se envía
        Assert.Equal(["api"], confirm.LastRepos);
    }

    [Fact]
    public async Task Cancelled_confirmation_returns_template_with_warning()
    {
        CommitIn("api", "fix: algo");
        var llm = new FakeLlm(isLocal: false);

        var result = await Service(llm, new FakeConfirm(new(false, false)))
            .BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.Null(llm.LastPrompt); // no se mandó nada
        Assert.False(result.UsedAi);
        Assert.Equal("Envío cancelado", result.Warning);
        Assert.Contains("- [api] fix: algo", result.Text);
    }

    [Fact]
    public async Task DontAskAgain_persists_repos()
    {
        CommitIn("api", "fix: algo");
        var confirm = new FakeConfirm(new(true, true));
        var service = Service(new FakeLlm(isLocal: false), confirm);

        await service.BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);
        await service.BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.Equal(["api"], _settings.Current.CloudConfirmedRepos);
        Assert.Equal(1, confirm.Calls);
    }

    [Fact]
    public async Task New_repo_asks_again_even_if_others_were_confirmed()
    {
        await _settings.SaveAsync(_settings.Current with { CloudConfirmedRepos = ["api"] }, _ct);
        CommitIn("api", "a");
        CommitIn("web", "w");
        var confirm = new FakeConfirm(new(true, false));

        await Service(new FakeLlm(isLocal: false), confirm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, true, _ct);

        Assert.Equal(["web"], confirm.LastRepos);
    }

    [Fact]
    public async Task Notes_require_confirmation_even_if_all_repos_were_approved()
    {
        // Revisión final I3: las notas no pertenecen a ningún repo; aprobar "api" no aprueba mandarlas.
        await _settings.SaveAsync(_settings.Current with { CloudConfirmedRepos = ["api"] }, _ct);
        CommitIn("api", "fix: algo");
        _notes.Notes.Add(new Note(Guid.NewGuid(), Monday9.AddDays(-3).AddHours(8), "pass temporal en ticket 4411", false, null));
        var confirm = new FakeConfirm(new(false, false));
        var llm = new FakeLlm(isLocal: false);

        await Service(llm, confirm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, true, _ct);

        Assert.Equal(1, confirm.Calls);
        Assert.Null(llm.LastPrompt);
    }

    [Fact]
    public async Task Approved_repos_without_notes_do_not_ask()
    {
        await _settings.SaveAsync(_settings.Current with { CloudConfirmedRepos = ["api"] }, _ct);
        CommitIn("api", "fix: algo");
        var confirm = new FakeConfirm(new(true, false));

        await Service(new FakeLlm(isLocal: false), confirm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, true, _ct);

        Assert.Equal(0, confirm.Calls);
    }

    [Fact]
    public async Task Cancelling_during_ai_returns_template_with_warning()
    {
        // Revisión final I6: con Ollama en frío la IA puede tardar minutos; cancelar tiene que devolver la plantilla.
        CommitIn("api", "fix: algo");
        using var cts = new CancellationTokenSource();
        var llm = new FakeLlm(isLocal: true) { Throw = new OperationCanceledException(cts.Token) };
        await cts.CancelAsync();

        var result = await Service(llm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, cts.Token);

        Assert.False(result.UsedAi);
        Assert.Equal("Generación cancelada", result.Warning);
        Assert.Contains("- [api] fix: algo", result.Text);
    }

    [Fact]
    public async Task Llm_failure_falls_back_to_template_with_warning()
    {
        CommitIn("api", "fix: algo");
        var llm = new FakeLlm(isLocal: true) { Throw = new LlmException("No se pudo conectar con Ollama") };

        var result = await Service(llm).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.False(result.UsedAi);
        Assert.Equal("No se pudo usar la IA: No se pudo conectar con Ollama", result.Warning);
        Assert.Contains("- [api] fix: algo", result.Text);
    }

    [Fact]
    public async Task Provider_creation_failure_falls_back_to_template()
    {
        var result = await Service(factory: _ => throw new LlmException("Falta la API key de Claude"))
            .BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.False(result.UsedAi);
        Assert.Equal("No se pudo usar la IA: Falta la API key de Claude", result.Warning);
    }

    [Fact]
    public async Task Ai_requested_without_provider_warns()
    {
        var result = await Service(llm: null).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.False(result.UsedAi);
        Assert.Equal("No hay un proveedor de IA elegido en Configuración", result.Warning);
    }

    [Fact]
    public async Task Broken_repo_is_reported_and_others_included()
    {
        CommitIn("api", "fix: algo");
        _git.ByRepo["web"] = new GitLogResult([], "La carpeta no existe");

        var result = await Service().BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: false, _ct);

        Assert.Contains("- [api] fix: algo", result.Text);
        Assert.EndsWith("> No se pudo leer: web\n", result.Text);
    }

    [Fact]
    public async Task Notes_in_window_are_included()
    {
        _notes.Notes.Add(new Note(Guid.NewGuid(), Monday9.AddDays(-3).AddHours(8), "revisé el PR", false, null));
        _notes.Notes.Add(new Note(Guid.NewGuid(), Monday9.AddDays(-10), "vieja", false, null));

        var result = await Service().BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: false, _ct);

        Assert.Contains("- revisé el PR", result.Text);
        Assert.DoesNotContain("vieja", result.Text);
    }

    [Fact]
    public void Prompt_contains_draft_and_language_instruction()
    {
        var es = PromptBuilder.For(ReportKind.Daily, ReportLanguage.Es, "**Ayer** (vie 06/11)\n- x\n");
        var en = PromptBuilder.For(ReportKind.Weekly, ReportLanguage.En, "**Week of 02/11 to 07/11**\n");

        Assert.EndsWith("**Ayer** (vie 06/11)\n- x\n", es);
        Assert.Contains("en español", es);
        Assert.Contains("No inventes", es);
        Assert.Contains("in English", en);
        Assert.Contains("Do not invent", en);
        Assert.Contains("weekly summary", en);
    }

    [Fact]
    public void Prompt_forbids_translating_repo_names()
    {
        // Visto en la prueba real: Ollama tradujo el repo "fantasma" a "ghost".
        Assert.Contains("Do not translate repository names", PromptBuilder.For(ReportKind.Weekly, ReportLanguage.En, "x"));
        Assert.Contains("No traduzcas los nombres de los repos", PromptBuilder.For(ReportKind.Weekly, ReportLanguage.Es, "x"));
    }

    [Fact]
    public async Task Warns_when_there_are_repos_but_no_emails()
    {
        // Minor 4: sin emails, git no devuelve commits y la daily salía vacía sin explicación.
        await _settings.SaveAsync(_settings.Current with { AuthorEmails = [] }, _ct);

        var result = await Service().BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: false, _ct);

        Assert.Equal("No hay emails de git en Configuración: no se incluyeron commits", result.Warning);
    }

    [Fact]
    public async Task Email_warning_is_kept_when_ai_also_warns()
    {
        await _settings.SaveAsync(_settings.Current with { AuthorEmails = [] }, _ct);

        var result = await Service(llm: null).BuildAsync(ReportKind.Daily, ReportLanguage.Es, useAi: true, _ct);

        Assert.Equal("No hay emails de git en Configuración: no se incluyeron commits. "
            + "No hay un proveedor de IA elegido en Configuración", result.Warning);
    }
}
