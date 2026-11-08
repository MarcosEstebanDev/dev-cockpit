using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Time;

namespace DevCockpit.Core.Reports;

/// <summary>
/// Arma la daily o el semanal: junta commits y notas, genera la plantilla y, si se pidió,
/// la hace reescribir por la IA. Si la IA falla o se cancela, siempre devuelve la plantilla.
/// </summary>
public sealed class ReportService(
    IGitLog git,
    INoteStore notes,
    ISettingsStore settings,
    Func<AppSettings, ILlmProvider?> llmFactory,
    ICloudSendConfirmation confirmation,
    IClock clock)
{
    public Task<ReportResult> BuildAsync(ReportKind kind, ReportLanguage lang, bool useAi, CancellationToken ct)
        => BuildAsync(kind, lang, useAi, null, ct);

    /// <param name="partial">Recibe el texto de la IA a medida que se genera (null: solo el final).</param>
    public async Task<ReportResult> BuildAsync(ReportKind kind, ReportLanguage lang, bool useAi,
        IProgress<string>? partial, CancellationToken ct)
    {
        var s = await settings.LoadAsync(ct);
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now.DateTime);
        var fromDate = kind == ReportKind.Daily ? Workdays.PreviousWorkday(today) : Workdays.WeekStart(today);
        var from = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), now.Offset);

        var commits = new List<Commit>();
        var errors = new List<string>();
        foreach (var repo in s.Repos)
        {
            var result = await git.GetCommitsAsync(repo, s.AuthorEmails, from, now, ct);
            if (result.Error is not null) errors.Add(repo.Name);
            commits.AddRange(result.Commits);
        }
        var windowNotes = await notes.ListAsync(from, now, ct);

        var input = new ReportInput(fromDate, today, commits, windowNotes, errors);
        var draft = kind == ReportKind.Daily
            ? TemplateReportBuilder.Daily(input, lang)
            : TemplateReportBuilder.Weekly(input, lang);

        // Sin emails, git no devuelve commits: sin este aviso la daily sale vacía sin explicación.
        var setupWarning = s.Repos.Count > 0 && s.AuthorEmails.Count == 0
            ? "No hay emails de git en Configuración: no se incluyeron commits"
            : null;
        var report = await BuildWithAiAsync(useAi, s, kind, lang, draft, commits, windowNotes, partial, ct);
        return setupWarning is null ? report
            : report with { Warning = report.Warning is null ? setupWarning : $"{setupWarning}. {report.Warning}" };
    }

    private async Task<ReportResult> BuildWithAiAsync(bool useAi, AppSettings s, ReportKind kind, ReportLanguage lang,
        string draft, List<Commit> commits, IReadOnlyList<Note> windowNotes, IProgress<string>? partial, CancellationToken ct)
    {
        if (!useAi) return new ReportResult(draft, false, null);

        ILlmProvider? llm;
        try
        {
            llm = llmFactory(s);
        }
        catch (LlmException e)
        {
            return AiFailed(draft, e);
        }
        if (llm is null) return new ReportResult(draft, false, "No hay un proveedor de IA elegido en Configuración");

        var prompt = PromptBuilder.For(kind, lang, draft);

        if (!llm.IsLocal)
        {
            var pending = commits.Select(c => c.RepoName).Distinct()
                .Except(s.CloudConfirmedRepos, StringComparer.OrdinalIgnoreCase)
                .ToList();
            // Las notas no pertenecen a ningún repo: aprobar repos no aprueba mandarlas.
            if (pending.Count > 0 || windowNotes.Count > 0 || commits.Count == 0)
            {
                var answer = await confirmation.ConfirmAsync(llm.DisplayName, prompt, pending);
                if (!answer.Send) return new ReportResult(draft, false, "Envío cancelado");
                if (answer.DontAskAgain && pending.Count > 0)
                {
                    await settings.SaveAsync(
                        s with { CloudConfirmedRepos = [.. s.CloudConfirmedRepos, .. pending] }, ct);
                }
            }
        }

        try
        {
            return new ReportResult(await llm.CompleteAsync(prompt, partial, ct), true, null);
        }
        catch (LlmException e)
        {
            return AiFailed(draft, e);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new ReportResult(draft, false, "Generación cancelada");
        }
    }

    private static ReportResult AiFailed(string draft, LlmException e)
        => new(draft, false, "No se pudo usar la IA: " + e.Message);
}
