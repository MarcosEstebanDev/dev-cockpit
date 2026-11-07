using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>IA local: <c>POST {url}/api/generate</c> sin streaming.</summary>
public sealed class OllamaProvider(HttpClient http, string baseUrl, string model) : ILlmProvider
{
    public string DisplayName => $"Ollama ({model})";
    public bool IsLocal => true;

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var url = baseUrl.TrimEnd('/') + "/api/generate";
        using var request = HttpJson.Json(url, new { model, prompt, stream = false });
        using var json = await HttpJson.PostAsync(http, request, "Ollama", ct);

        if (json.RootElement.TryGetProperty("response", out var text) && text.GetString() is { } value)
            return value.Trim();
        throw new LlmException("Ollama devolvió una respuesta sin texto");
    }
}
