using System.Net.Http.Headers;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>ChatGPT por la API de chat completions.</summary>
public sealed class OpenAIProvider(HttpClient http, string apiKey, string model) : ILlmProvider
{
    public string DisplayName => $"ChatGPT ({model})";
    public bool IsLocal => false;

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        using var request = HttpJson.Json("https://api.openai.com/v1/chat/completions", new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
        });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var json = await HttpJson.PostAsync(http, request, "ChatGPT", ct);

        try
        {
            var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();
        }
        catch (Exception e) when (e is KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new LlmException("ChatGPT devolvió una respuesta con un formato inesperado", e);
        }
        throw new LlmException("ChatGPT devolvió una respuesta sin texto");
    }
}
