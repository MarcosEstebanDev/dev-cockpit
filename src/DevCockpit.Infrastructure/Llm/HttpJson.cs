using System.Net.Http.Json;
using System.Text.Json;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>POST de JSON con timeout propio; traduce toda falla a <see cref="LlmException"/>.</summary>
internal static class HttpJson
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public static Task<JsonDocument> PostAsync(
        HttpClient http, HttpRequestMessage request, string providerName, CancellationToken ct)
        => PostAsync(http, request, providerName, Timeout, ct);

    public static async Task<JsonDocument> PostAsync(
        HttpClient http, HttpRequestMessage request, string providerName, TimeSpan limit, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(limit);
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new LlmException($"{providerName} respondió {(int)response.StatusCode}: {Shorten(body)}");
            return JsonDocument.Parse(body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException($"{providerName} tardó más de {limit.TotalSeconds:0} s en responder");
        }
        catch (HttpRequestException e)
        {
            throw new LlmException($"No se pudo conectar con {providerName}: {e.Message}", e);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NotSupportedException or FormatException)
        {
            // URL mal formada u otro problema del HttpClient: nunca debe escapar y cerrar la app.
            throw new LlmException($"No se pudo llamar a {providerName}: {e.Message}", e);
        }
        catch (JsonException e)
        {
            throw new LlmException($"{providerName} devolvió una respuesta que no es JSON", e);
        }
    }

    public static HttpRequestMessage Json(string url, object body) =>
        new(HttpMethod.Post, url) { Content = JsonContent.Create(body) };

    internal static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
