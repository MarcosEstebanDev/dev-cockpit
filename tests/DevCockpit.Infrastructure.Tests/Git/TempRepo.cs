using System.Diagnostics;

namespace DevCockpit.Infrastructure.Tests.Git;

/// <summary>Repo de git descartable para los tests. Se borra al hacer Dispose.</summary>
public sealed class TempRepo : IDisposable
{
    public static readonly string Git =
        File.Exists(@"C:\Users\marcos\AppData\Local\Programs\PortableGit\cmd\git.exe")
            ? @"C:\Users\marcos\AppData\Local\Programs\PortableGit\cmd\git.exe"
            : "git";

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "devcockpit-test-" + Guid.NewGuid().ToString("N"));

    public TempRepo()
    {
        Directory.CreateDirectory(Path);
        Run("init", "-q", "-b", "main");
    }

    public void Commit(string email, string subject, DateTimeOffset when)
    {
        var date = when.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        Run(new Dictionary<string, string> { ["GIT_AUTHOR_DATE"] = date, ["GIT_COMMITTER_DATE"] = date },
            "-c", $"user.email={email}", "-c", "user.name=Test", "commit", "-q", "--allow-empty", "-m", subject);
    }

    public void Run(params string[] args) => Run(new Dictionary<string, string>(), args);

    private void Run(Dictionary<string, string> env, params string[] args)
    {
        var psi = new ProcessStartInfo(Git) { WorkingDirectory = Path, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException(p.StandardError.ReadToEnd());
    }

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal); // git deja archivos de solo lectura
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException) { }
    }
}
