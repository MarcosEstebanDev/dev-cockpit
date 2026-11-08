using DevCockpit.Core.Domain;
using DevCockpit.Core.Reports;

namespace DevCockpit.Core.Tests.Reports;

public class TemplateReportBuilderTests
{
    private static readonly TimeSpan Ar = TimeSpan.FromHours(-3);
    private static readonly DateOnly Friday = new(2026, 11, 6);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    private static Commit C(string repo, string subject, int hour)
        => new(repo, Guid.NewGuid().ToString("N"), "m@x.com", new DateTimeOffset(2026, 11, 6, hour, 0, 0, Ar), subject);

    private static Note N(string text, int hour, bool achievement = false, string? metric = null, int day = 6)
        => new(Guid.NewGuid(), new DateTimeOffset(2026, 11, day, hour, 0, 0, Ar), text, achievement, metric);

    [Fact]
    public void Daily_es_lists_commits_then_notes()
    {
        var input = new ReportInput(Friday, Saturday,
            [C("helpdesk-api", "fix: timeout del SLA", 10)],
            [N("Revisé el PR de Juan", 12)], []);

        var text = TemplateReportBuilder.Daily(input, ReportLanguage.Es);

        Assert.Equal(
            "**Ayer** (vie 06/11)\n" +
            "- [helpdesk-api] fix: timeout del SLA\n" +
            "- Revisé el PR de Juan\n" +
            "**Hoy**\n" +
            "- \n" +
            "**Bloqueos**\n" +
            "- Ninguno\n", text);
    }

    [Fact]
    public void Daily_orders_commits_by_date_before_notes()
    {
        var input = new ReportInput(Friday, Saturday,
            [C("b", "segundo", 15), C("a", "primero", 9)],
            [N("nota", 8)], []);

        var lines = TemplateReportBuilder.Daily(input, ReportLanguage.Es).Split('\n');

        Assert.Equal("- [a] primero", lines[1]);
        Assert.Equal("- [b] segundo", lines[2]);
        Assert.Equal("- nota", lines[3]);
    }

    [Fact]
    public void Daily_puts_todays_items_under_hoy()
    {
        // Revisión final M1: lo hecho hoy antes de la daily no es "Ayer".
        var today = new Commit("api", "s", "m@x.com", new DateTimeOffset(2026, 11, 7, 9, 0, 0, Ar), "hecho hoy");
        var input = new ReportInput(Friday, Saturday,
            [C("api", "hecho ayer", 10), today],
            [N("nota de hoy", 9, day: 7)], []);

        var text = TemplateReportBuilder.Daily(input, ReportLanguage.Es);

        Assert.Equal(
            "**Ayer** (vie 06/11)\n" +
            "- [api] hecho ayer\n" +
            "**Hoy**\n" +
            "- [api] hecho hoy\n" +
            "- nota de hoy\n" +
            "**Bloqueos**\n" +
            "- Ninguno\n", text);
    }

    [Fact]
    public void Daily_en_uses_english_headings()
    {
        var input = new ReportInput(Friday, Saturday, [C("api", "feat: x", 10)], [], []);

        var text = TemplateReportBuilder.Daily(input, ReportLanguage.En);

        Assert.StartsWith("**Yesterday** (Fri 06/11)\n", text);
        Assert.Contains("**Today**\n", text);
        Assert.EndsWith("**Blockers**\n- None\n", text);
    }

    [Fact]
    public void Daily_without_activity_shows_empty_bullet()
    {
        var text = TemplateReportBuilder.Daily(new ReportInput(Friday, Saturday, [], [], []), ReportLanguage.Es);

        Assert.StartsWith("**Ayer** (vie 06/11)\n- \n**Hoy**\n", text);
    }

    [Fact]
    public void Weekly_groups_by_repo()
    {
        var input = new ReportInput(new DateOnly(2026, 11, 2), Saturday,
            [C("api", "a1", 9), C("web", "w1", 10), C("api", "a2", 11)],
            [N("nota suelta", 12), N("bajó la query", 13, achievement: true, metric: "4 s a 300 ms")], []);

        var text = TemplateReportBuilder.Weekly(input, ReportLanguage.Es);

        Assert.Equal(
            "**Semana del 02/11 al 07/11**\n" +
            "**api**\n" +
            "- a1\n" +
            "- a2\n" +
            "**web**\n" +
            "- w1\n" +
            "**Notas**\n" +
            "- nota suelta\n" +
            "**Logros**\n" +
            "- 2026-11-06: bajó la query (4 s a 300 ms)\n", text);
    }

    [Fact]
    public void Weekly_without_activity_says_so()
    {
        var text = TemplateReportBuilder.Weekly(new ReportInput(new DateOnly(2026, 11, 2), Saturday, [], [], []), ReportLanguage.En);

        Assert.Equal("**Week of 02/11 to 07/11**\n- No recorded activity\n", text);
    }

    [Fact]
    public void Achievements_include_metric_when_present()
    {
        var text = TemplateReportBuilder.Achievements(
            [N("sin número", 9, true, null, day: 3), N("con número", 9, true, "x3", day: 5)], ReportLanguage.Es);

        Assert.Equal(
            "- 2026-11-05: con número (x3)\n" +
            "- 2026-11-03: sin número\n", text);
    }

    [Fact]
    public void RepoErrors_are_listed_at_the_end()
    {
        var input = new ReportInput(Friday, Saturday, [], [], ["roto", "borrado"]);

        Assert.EndsWith("> No se pudo leer: roto, borrado\n", TemplateReportBuilder.Daily(input, ReportLanguage.Es));
        Assert.EndsWith("> Could not read: roto, borrado\n", TemplateReportBuilder.Weekly(input, ReportLanguage.En));
    }

    [Fact]
    public void Multiline_note_stays_inside_its_bullet()
    {
        // Minor 11: una nota con Shift+Enter partía la viñeta y la segunda línea quedaba suelta.
        var input = new ReportInput(Friday, Saturday, [], [N("Migré la base\r\nfaltan los índices", 12)], []);

        var text = TemplateReportBuilder.Daily(input, ReportLanguage.Es);

        Assert.Contains("- Migré la base\n  faltan los índices\n", text);
    }

    [Fact]
    public void Multiline_achievement_stays_inside_its_bullet()
    {
        var text = TemplateReportBuilder.Achievements(
            [N("Bajé la query\nde la home", 12, achievement: true, metric: "4 s a 300 ms")], ReportLanguage.Es);

        Assert.Equal("- 2026-11-06: Bajé la query\n  de la home (4 s a 300 ms)\n", text);
    }

    [Theory]
    [InlineData(ReportLanguage.Es, "Sé breve")]
    [InlineData(ReportLanguage.En, "Be brief")]
    public void Prompt_asks_for_a_short_answer(ReportLanguage lang, string rule)
    {
        Assert.Contains(rule, PromptBuilder.For(ReportKind.Daily, lang, "**Ayer**\n- x\n"));
    }
}
