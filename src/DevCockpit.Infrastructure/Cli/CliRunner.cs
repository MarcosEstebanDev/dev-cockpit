using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DevCockpit.Infrastructure.Cli;

public sealed record CliResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Ejecuta un programa de línea de comandos y captura su salida en UTF-8.</summary>
public static class CliRunner
{
    public static async Task<CliResult> RunAsync(
        string executable,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // ArgumentList escapa cada argumento: nunca se arma un string de comando a mano.
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (environment is not null)
            foreach (var (k, v) in environment) psi.Environment[k] = v;

        System.Diagnostics.Process process;
        try
        {
            process = System.Diagnostics.Process.Start(psi)
                      ?? throw new InvalidOperationException($"No se pudo iniciar {executable}");
        }
        catch (Win32Exception e)
        {
            return new CliResult(-1, "", $"No se encontró el ejecutable '{executable}': {e.Message}");
        }

        using (process)
        {
            // Leer las dos salidas a la vez: si una se llena y nadie la lee, el proceso se bloquea.
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                throw;
            }
            return new CliResult(process.ExitCode, await stdout, await stderr);
        }
    }
}
