namespace DevCockpit.Infrastructure.Storage;

/// <summary>Dónde vive cada archivo de la app (en Windows: %APPDATA%\DevCockpit).</summary>
public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevCockpit");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string DatabaseFile => Path.Combine(DataDir, "devcockpit.db");
}
