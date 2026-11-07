using System.Net.Http.Json;
using System.Text.Json;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>POST de JSON con timeout propio; traduce toda falla a <see cref="LlmException"/>.</summary>
internal static class HttpJson
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public static async Task<JsonDocument> PostAsync(
        HttpClient http, HttpRequestMessage request, string providerName, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
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
            throw new LlmException($"{providerName} tardó más de {Timeout.TotalSeconds:0} s en responder");
        }
        catch (HttpRequestException e)
        {
            throw new LlmException($"No se pudo conectar con {providerName}: {e.Message}", e);
        }
        catch (JsonException e)
        {
            throw new LlmException($"{providerName} devolvió una respuesta que no es JSON", e);
        }
    }

    public static HttpRequestMessage Json(string url, object body) =>
        new(HttpMethod.Post, url) { Content = JsonContent.Create(body) };

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
