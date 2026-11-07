using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Infrastructure.Git;

namespace DevCockpit.App.Services;

/// <summary>
/// Usa la ruta de git de la configuración vigente en cada llamada, así un cambio en
/// Configuración aplica sin reiniciar la app.
/// </summary>
public sealed class ConfiguredGit(ISettingsStore settings) : IGitLog, IGitIdentity
{
    private static readonly string PortableGit = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "PortableGit", "cmd", "git.exe");

    /// <summary>"git" a secas usa el PATH; si no hay git de sistema pero sí PortableGit, usa ese.</summary>
    public static string Resolve(string configured) =>
        configured == "git" && File.Exists(PortableGit) ? PortableGit : configured;

    private async Task<GitCliLog> GitAsync(CancellationToken ct)
        => new(Resolve((await settings.LoadAsync(ct)).GitExecutable));

    public async Task<GitLogResult> GetCommitsAsync(WatchedRepo repo, IReadOnlyCollection<string> authorEmails,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => await (await GitAsync(ct)).GetCommitsAsync(repo, authorEmails, from, to, ct);

    public async Task<string?> GetGlobalEmailAsync(CancellationToken ct)
        => await (await GitAsync(ct)).GetGlobalEmailAsync(ct);
}
