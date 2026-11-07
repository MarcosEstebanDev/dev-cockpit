using System.Text.Json;
using System.Text.Json.Serialization;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Storage;

/// <summary>Configuración en un JSON legible. Nunca guarda API keys (esas van a ISecretStore).</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<AppSettings> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, ct) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Archivo dañado: se guarda una copia para no perder lo que había y se arranca con defaults.
            File.Move(path, path + ".bad", overwrite: true);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
            await JsonSerializer.SerializeAsync(stream, settings, Options, ct);
        // Reemplazo atómico: si la app se corta a mitad de guardado, el archivo anterior queda intacto.
        File.Move(tmp, path, overwrite: true);
    }
}
