using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>IA local: <c>POST {url}/api/generate</c> sin streaming.</summary>
public sealed class OllamaProvider(HttpClient http, string baseUrl, string model, TimeSpan? timeout = null) : ILlmProvider
{
    /// <summary>
    /// Medido en la PC de desarrollo (HDD, CPU): cargar un modelo 7B en frío tarda ~200 s y
    /// generar una daily ~45 s. El límite de la nube (120 s) cortaría el primer uso del día.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Cuánto tiempo Ollama mantiene el modelo en RAM después de cada uso.</summary>
    private const string KeepAlive = "1h";

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;
    private string Url => baseUrl.TrimEnd('/') + "/api/generate";

    public string DisplayName => $"Ollama ({model})";
    public bool IsLocal => true;

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        using var request = HttpJson.Json(Url, new { model, prompt, stream = false, keep_alive = KeepAlive });
        using var json = await HttpJson.PostAsync(http, request, "Ollama", _timeout, ct);

        if (json.RootElement.TryGetProperty("response", out var text) && text.GetString() is { } value)
            return value.Trim();
        throw new LlmException("Ollama devolvió una respuesta sin texto");
    }

    /// <summary>
    /// Pide a Ollama que cargue el modelo en memoria (un request sin prompt). Se llama al abrir la app
    /// para que el primer "Generar" no espere la carga. Nunca falla: si Ollama no está, no pasa nada.
    /// </summary>
    public async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            using var request = HttpJson.Json(Url, new { model, stream = false, keep_alive = KeepAlive });
            using var _ = await HttpJson.PostAsync(http, request, "Ollama", _timeout, ct);
        }
        catch (LlmException) { }
    }
}
