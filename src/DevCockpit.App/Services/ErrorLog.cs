using System;
using System.IO;
using System.Threading.Tasks;
using DevCockpit.Infrastructure.Storage;

namespace DevCockpit.App.Services;

/// <summary>
/// Registro de errores inesperados en %APPDATA%\DevCockpit\error.log. Existe para que ninguna falla
/// quede muda: ni las de tareas en segundo plano ni las que llegarían a cerrar la app.
/// </summary>
public static class ErrorLog
{
    public static string File => Path.Combine(AppPaths.DataDir, "error.log");

    public static void Write(string where, Exception e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            System.IO.File.AppendAllText(File, $"{DateTimeOffset.Now:O} [{where}] {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException) { }            // si ni siquiera se puede escribir el log, no hay más que hacer
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Espera una tarea "dispara y olvidá" y registra su error en vez de perderlo.</summary>
    public static async Task Observe(Task task, string what, Action<Exception>? onError = null)
    {
        try
        {
            await task;
        }
        catch (Exception e)
        {
            Write(what, e);
            onError?.Invoke(e);
        }
    }
}
