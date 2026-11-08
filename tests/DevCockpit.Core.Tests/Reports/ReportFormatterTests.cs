using DevCockpit.Core.Reports;

namespace DevCockpit.Core.Tests.Reports;

public class ReportFormatterTests
{
    [Fact]
    public void Heading_splits_title_and_date()
    {
        var line = Assert.Single(ReportFormatter.Parse("**Ayer** (vie 06/11)"));

        Assert.Equal(ReportLineKind.Heading, line.Kind);
        Assert.Equal("Ayer", line.Text);
        Assert.Equal("vie 06/11", line.Meta);
    }

    [Fact]
    public void Template_bullet_with_repo_in_brackets()
    {
        var line = Assert.Single(ReportFormatter.Parse("- [helpdesk-api] fix: timeout del SLA"));

        Assert.Equal(ReportLineKind.Bullet, line.Kind);
        Assert.Equal("helpdesk-api", line.Repo);
        Assert.Equal("fix: timeout del SLA", line.Text);
    }

    [Fact]
    public void Ai_bullet_with_repo_and_colon()
    {
        var line = Assert.Single(ReportFormatter.Parse("- dev-cockpit: módulo Bitácora completo"));

        Assert.Equal("dev-cockpit", line.Repo);
        Assert.Equal("módulo Bitácora completo", line.Text);
    }

    [Theory]
    [InlineData("- Ninguno")]
    [InlineData("- Revisé el PR de Juan: aprobado")]   // "Revisé el PR de Juan" no es un nombre de repo
    [InlineData("- Nota: llamar a soporte")]           // con mayúscula: es texto, no un repo
    public void Plain_bullets_have_no_repo(string text)
    {
        var line = Assert.Single(ReportFormatter.Parse(text));

        Assert.Equal(ReportLineKind.Bullet, line.Kind);
        Assert.Null(line.Repo);
        Assert.Equal(text[2..], line.Text);
    }

    [Fact]
    public void Indented_continuation_joins_its_bullet()
    {
        var line = Assert.Single(ReportFormatter.Parse("- Migré la base\n  faltan los índices"));

        Assert.Equal("Migré la base\nfaltan los índices", line.Text);
    }

    [Fact]
    public void Quote_text_and_blank_lines()
    {
        var lines = ReportFormatter.Parse("Resumen libre\n\n> No se pudo leer: roto\n- ");

        Assert.Equal([ReportLineKind.Text, ReportLineKind.Quote], lines.Select(l => l.Kind));
        Assert.Equal("No se pudo leer: roto", lines[1].Text);
    }

    [Fact]
    public void Ai_bullet_with_repo_in_brackets_and_colon()
    {
        // Así respondieron qwen2.5 3B y 7B en la medición: corchetes y dos puntos a la vez.
        var line = Assert.Single(ReportFormatter.Parse("- [dev-cockpit]: Se implementó el layout"));

        Assert.Equal("dev-cockpit", line.Repo);
        Assert.Equal("Se implementó el layout", line.Text);
    }

    [Fact]
    public void Repo_tag_shows_once_per_run_of_the_same_repo()
    {
        var lines = ReportFormatter.Parse("- [api] uno\n- [api] dos\n- [web] tres\n- [api] cuatro");

        Assert.Equal([false, true, false, false], lines.Select(l => l.IsRepoRepeat));
    }
}
