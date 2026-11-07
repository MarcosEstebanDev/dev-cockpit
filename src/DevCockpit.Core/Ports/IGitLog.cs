using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Ports;

/// <summary>Resultado de leer un repo: commits, o un error legible si no se pudo leer.</summary>
public sealed record GitLogResult(IReadOnlyList<Commit> Commits, string? Error);

public interface IGitLog
{
    Task<GitLogResult> GetCommitsAsync(
        WatchedRepo repo,
        IReadOnlyCollection<string> authorEmails,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct);
}

public interface IGitIdentity
{
    /// <summary>Email de <c>git config --global user.email</c>, o null si no está configurado.</summary>
    Task<string?> GetGlobalEmailAsync(CancellationToken ct);
}
