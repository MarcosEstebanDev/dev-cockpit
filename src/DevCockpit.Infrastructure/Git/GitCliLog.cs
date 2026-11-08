using System.Globalization;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Infrastructure.Cli;

namespace DevCockpit.Infrastructure.Git;

/// <summary>Lee commits ejecutando el <c>git</c> instalado (mismo comportamiento que en la terminal).</summary>
public sealed class GitCliLog(string gitExecutable, IReadOnlyDictionary<string, string>? environment = null)
    : IGitLog, IGitIdentity
{
    private const char FieldSep = '\u001f';  // %x1f
    private const char RecordSep = '\u001e'; // %x1e

    public async Task<GitLogResult> GetCommitsAsync(
        WatchedRepo repo,
        IReadOnlyCollection<string> authorEmails,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        if (!Directory.Exists(repo.Path))
            return new GitLogResult([], "La carpeta no existe");
        if (authorEmails.Count == 0)
            return new GitLogResult([], null);

        var args = new List<string>
        {
            "-C", repo.Path,
            "-c", "i18n.logOutputEncoding=UTF-8",
            "log", "--all", "--no-merges",
            "--fixed-strings", "--regexp-ignore-case",
            "--since=" + from.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            "--until=" + to.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            "--format=%H%x1f%ae%x1f%aI%x1f%s%x1e",
        };
        // "<email>" literal: git compara contra "Nombre <email>", así no matchea emails que lo contengan.
        foreach (var email in authorEmails) args.Add($"--author=<{email}>");

        var result = await CliRunner.RunAsync(gitExecutable, args, environment, ct);
        if (result.ExitCode != 0)
            return new GitLogResult([], FirstLine(result.StdErr));

        var commits = new List<Commit>();
        var seen = new HashSet<string>();
        foreach (var record in result.StdOut.Split(RecordSep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var f = record.Split(FieldSep);
            if (f.Length < 4 || !seen.Add(f[0])) continue;
            commits.Add(new Commit(repo.Name, f[0], f[1],
                DateTimeOffset.Parse(f[2], CultureInfo.InvariantCulture), f[3]));
        }
        return new GitLogResult(commits, null);
    }

    public async Task<string?> GetGlobalEmailAsync(CancellationToken ct)
    {
        var result = await CliRunner.RunAsync(gitExecutable, ["config", "--global", "user.email"], environment, ct);
        var email = result.StdOut.Trim();
        return result.ExitCode == 0 && email.Length > 0 ? email : null;
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "git terminó con error" : line;
    }
}
