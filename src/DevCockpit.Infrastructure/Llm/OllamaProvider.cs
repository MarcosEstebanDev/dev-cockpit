using System.Text;
using System.Text.Json;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>IA local: <c>POST {url}/api/generate</c> con streaming (una línea JSON por pedazo de texto).</summary>
public sealed class OllamaProvider(HttpClient http, string baseUrl, string model, TimeSpan? timeout = null) : ILlmProvider
{
    /// <summary>
    /// Medido en la PC de desarrollo (HDD, CPU): cargar un modelo 7B en frío tarda ~200 s y
    /// generar una daily ~45 s. El límite de la nube (120 s) cortaría el primer uso del día.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tope de tokens de la respuesta. Medido en la PC de desarrollo (CPU, ~3 tokens/s): una daily de
    /// 570 tokens tardó 3m20s. El prompt ya pide brevedad; esto corta si el modelo se va de largo.
    /// </summary>
    public const int MaxAnswerTokens = 450;

    /// <summary>Cuánto tiempo Ollama mantiene el modelo en RAM después de cada uso.</summary>
    private const string KeepAlive = "1h";

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    private string Url => IsValidBaseUrl(baseUrl)
        ? baseUrl.Trim().TrimEnd('/') + "/api/generate"
        : throw new LlmException($"La URL de Ollama no es válida: '{baseUrl}'. Tiene que ser como http://localhost:11434");

    /// <summary>Solo http(s) absoluto: "127.0.0.1:11434" (formato de OLLAMA_HOST) no sirve como URL.</summary>
    public static bool IsValidBaseUrl(string? url)
        => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public string DisplayName => $"Ollama ({model})";
    public bool IsLocal => true;

    public Task<string> CompleteAsync(string prompt, CancellationToken ct) => CompleteAsync(prompt, null, ct);

    public async Task<string> CompleteAsync(string prompt, IProgress<string>? partial, CancellationToken ct)
    {
        using var request = HttpJson.Json(Url, new
        {
            model, prompt, stream = true, keep_alive = KeepAlive,
            options = new { num_predict = MaxAnswerTokens },
        });

        // El límite es por inactividad: se reinicia con cada pedazo. Así una carga en frío (~200 s)
        // seguida de una respuesta larga no se corta mientras Ollama siga mandando texto.
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(_timeout);
        var text = new StringBuilder();
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idle.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(idle.Token);
                throw new LlmException($"Ollama respondió {(int)response.StatusCode}: {HttpJson.Shorten(body)}");
            }

            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(idle.Token), Encoding.UTF8);
            while (await reader.ReadLineAsync(idle.Token) is { } line)
            {
                idle.CancelAfter(_timeout);
                if (string.IsNullOrWhiteSpace(line)) continue;

                using var chunk = JsonDocument.Parse(line);
                var root = chunk.RootElement;
                if (root.TryGetProperty("error", out var error))
                    throw new LlmException("Ollama falló: " + error.GetString());
                if (root.TryGetProperty("response", out var piece) && piece.GetString() is { Length: > 0 } value)
                {
                    text.Append(value);
                    partial?.Report(text.ToString());
                }
                if (root.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException($"Ollama tardó más de {_timeout.TotalSeconds:0} s sin mandar texto");
        }
        catch (HttpRequestException e)
        {
            throw new LlmException($"No se pudo conectar con Ollama: {e.Message}", e);
        }
        catch (JsonException e)
        {
            throw new LlmException("Ollama devolvió una respuesta que no es JSON", e);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException or FormatException or IOException)
        {
            throw new LlmException($"No se pudo llamar a Ollama: {e.Message}", e);
        }

        var result = text.ToString().Trim();
        return result.Length > 0 ? result : throw new LlmException("Ollama devolvió una respuesta sin texto");
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
