using System.Globalization;
using System.Text;
using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Reports;

/// <summary>Arma los resúmenes sin IA. Su salida también es el borrador que recibe la IA.</summary>
public static class TemplateReportBuilder
{
    private sealed record Texts(
        string Yesterday, string Today, string Blockers, string None,
        string WeekOf, string To, string Notes, string Achievements,
        string NoActivity, string CouldNotRead, string[] Days);

    private static readonly Texts Es = new(
        "Ayer", "Hoy", "Bloqueos", "Ninguno",
        "Semana del", "al", "Notas", "Logros",
        "Sin actividad registrada", "No se pudo leer",
        ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"]);

    private static readonly Texts En = new(
        "Yesterday", "Today", "Blockers", "None",
        "Week of", "to", "Notes", "Achievements",
        "No recorded activity", "Could not read",
        ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]);

    private static Texts For(ReportLanguage lang) => lang == ReportLanguage.En ? En : Es;

    private static string DayMonth(DateOnly d) => d.ToString("dd'/'MM", CultureInfo.InvariantCulture);

    public static string Daily(ReportInput input, ReportLanguage lang)
    {
        var t = For(lang);
        var sb = new StringBuilder();
        sb.Append($"**{t.Yesterday}** ({t.Days[(int)input.From.DayOfWeek]} {DayMonth(input.From)})\n");

        // Lo hecho hoy (antes de la daily) va en "Hoy"; el resto es del día hábil anterior.
        bool IsToday(DateTimeOffset when) => DateOnly.FromDateTime(when.DateTime) == input.To;
        AppendBullets(sb,
            input.Commits.Where(c => !IsToday(c.When)).ToList(),
            input.Notes.Where(n => !IsToday(n.CreatedAt)).ToList());

        sb.Append($"**{t.Today}**\n");
        AppendBullets(sb,
            input.Commits.Where(c => IsToday(c.When)).ToList(),
            input.Notes.Where(n => IsToday(n.CreatedAt)).ToList());

        sb.Append($"**{t.Blockers}**\n- {t.None}\n");
        AppendErrors(sb, input, t);
        return sb.ToString();
    }

    public static string Weekly(ReportInput input, ReportLanguage lang)
    {
        var t = For(lang);
        var sb = new StringBuilder();
        sb.Append($"**{t.WeekOf} {DayMonth(input.From)} {t.To} {DayMonth(input.To)}**\n");

        var byRepo = input.Commits
            .GroupBy(c => c.RepoName)
            .OrderBy(g => g.Min(c => c.When));
        foreach (var group in byRepo)
        {
            sb.Append($"**{group.Key}**\n");
            foreach (var c in group.OrderBy(c => c.When)) sb.Append($"- {c.Subject}\n");
        }

        var plain = input.Notes.Where(n => !n.IsAchievement).OrderBy(n => n.CreatedAt).ToList();
        if (plain.Count > 0)
        {
            sb.Append($"**{t.Notes}**\n");
            foreach (var n in plain) sb.Append($"- {Indent(n.Text)}\n");
        }

        var achievements = input.Notes.Where(n => n.IsAchievement).ToList();
        if (achievements.Count > 0)
        {
            sb.Append($"**{t.Achievements}**\n");
            sb.Append(Achievements(achievements, lang));
        }

        if (input.Commits.Count == 0 && input.Notes.Count == 0)
            sb.Append($"- {t.NoActivity}\n");

        AppendErrors(sb, input, t);
        return sb.ToString();
    }

    public static string Achievements(IReadOnlyList<Note> notes, ReportLanguage lang)
    {
        var sb = new StringBuilder();
        foreach (var n in notes.OrderByDescending(n => n.CreatedAt))
        {
            sb.Append("- ").Append(n.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(": ").Append(Indent(n.Text));
            if (!string.IsNullOrWhiteSpace(n.Metric)) sb.Append($" ({n.Metric})");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void AppendBullets(StringBuilder sb, List<Commit> commits, List<Note> notes)
    {
        var lines = commits.OrderBy(c => c.When).Select(c => $"- [{c.RepoName}] {c.Subject}")
            .Concat(notes.OrderBy(n => n.CreatedAt).Select(n => $"- {Indent(n.Text)}"))
            .ToList();
        if (lines.Count == 0) lines.Add("- ");
        foreach (var line in lines) sb.Append(line).Append('\n');
    }

    /// <summary>Las líneas extra de una nota (Shift+Enter) van sangradas para seguir dentro de su viñeta.</summary>
    private static string Indent(string text)
        => string.Join("\n  ", text.ReplaceLineEndings("\n").Split('\n'));

    private static void AppendErrors(StringBuilder sb, ReportInput input, Texts t)
    {
        if (input.RepoErrors.Count > 0)
            sb.Append($"> {t.CouldNotRead}: {string.Join(", ", input.RepoErrors)}\n");
    }
}
