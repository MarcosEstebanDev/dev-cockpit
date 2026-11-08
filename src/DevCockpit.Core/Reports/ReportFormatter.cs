using System.Text.RegularExpressions;

namespace DevCockpit.Core.Reports;

public enum ReportLineKind { Heading, Bullet, Quote, Text }

/// <summary>Una línea del resumen ya interpretada, para mostrarla con formato en vez de como texto crudo.</summary>
/// <param name="Meta">En un encabezado, lo que va entre paréntesis (la fecha).</param>
/// <param name="Repo">En una viñeta, el repo al que pertenece, si se puede saber.</param>
public sealed record ReportLine(ReportLineKind Kind, string Text, string? Meta = null, string? Repo = null)
{
    public bool IsHeading => Kind == ReportLineKind.Heading;
    public bool IsBullet => Kind == ReportLineKind.Bullet;
    public bool IsQuote => Kind == ReportLineKind.Quote;
    public bool IsText => Kind == ReportLineKind.Text;
    public bool HasRepo => Repo is not null;
    public bool HasMeta => Meta is not null;
}

/// <summary>
/// Lee el formato que arman la plantilla y la IA (encabezados en negrita, viñetas con guion, citas con &gt;).
/// El texto copiado sigue siendo ese formato: esto solo decide cómo se ve en pantalla.
/// </summary>
public static partial class ReportFormatter
{
    // "**Ayer** (vie 06/11)" o "**Semana del 02/11 al 06/11**"
    [GeneratedRegex(@"^\*\*(?<title>.+?)\*\*\s*(\((?<meta>.+)\))?\s*$")]
    private static partial Regex Heading();

    // Plantilla: "[helpdesk-api] fix: algo"
    [GeneratedRegex(@"^\[(?<repo>[^\]]+)\]\s*(?<rest>.*)$")]
    private static partial Regex BracketRepo();

    // IA: "dev-cockpit: algo". Solo nombres con forma de repo (minúsculas y un - . o _), así
    // "Nota: algo" o "fix: algo" quedan como texto común.
    [GeneratedRegex(@"^(?<repo>[a-z0-9]+(?:[-._][a-z0-9]+)+):\s+(?<rest>.+)$")]
    private static partial Regex ColonRepo();

    public static IReadOnlyList<ReportLine> Parse(string? text)
    {
        var lines = new List<ReportLine>();
        if (string.IsNullOrWhiteSpace(text)) return lines;

        foreach (var raw in text.ReplaceLineEndings("\n").Split('\n'))
        {
            // Continuación sangrada de una viñeta (nota de varias líneas).
            if (raw.StartsWith("  ") && raw.Trim().Length > 0 && lines.Count > 0 && lines[^1].IsBullet)
            {
                lines[^1] = lines[^1] with { Text = lines[^1].Text + "\n" + raw.Trim() };
                continue;
            }

            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (Heading().Match(line) is { Success: true } h)
            {
                var meta = h.Groups["meta"].Success ? h.Groups["meta"].Value.Trim() : null;
                lines.Add(new ReportLine(ReportLineKind.Heading, h.Groups["title"].Value.Trim(), Meta: meta));
            }
            else if (line.StartsWith("- ") || line == "-")
            {
                var body = line.Length > 2 ? line[2..].Trim() : "";
                if (body.Length == 0) continue; // viñeta vacía de la plantilla: no aporta nada en pantalla
                lines.Add(Bullet(body));
            }
            else if (line.StartsWith('>'))
            {
                lines.Add(new ReportLine(ReportLineKind.Quote, line.TrimStart('>').Trim()));
            }
            else
            {
                lines.Add(new ReportLine(ReportLineKind.Text, line));
            }
        }
        return lines;
    }

    private static ReportLine Bullet(string body)
    {
        if (BracketRepo().Match(body) is { Success: true } b)
            // "[repo]: texto" (así responden algunos modelos) o "[repo] texto" (la plantilla).
            return new ReportLine(ReportLineKind.Bullet, b.Groups["rest"].Value.TrimStart(':').Trim(),
                Repo: b.Groups["repo"].Value.Trim());
        if (ColonRepo().Match(body) is { Success: true } c)
            return new ReportLine(ReportLineKind.Bullet, c.Groups["rest"].Value.Trim(), Repo: c.Groups["repo"].Value);
        return new ReportLine(ReportLineKind.Bullet, body);
    }
}
