using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>Claude por el SDK oficial de Anthropic.</summary>
public sealed class AnthropicProvider(HttpClient http, string apiKey, string model) : ILlmProvider
{
    private readonly AnthropicClient _client = new()
    {
        ApiKey = apiKey,
        HttpClient = http,
        Timeout = HttpJson.Timeout,
    };

    public string DisplayName => $"Claude ({model})";
    public bool IsLocal => false;

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        Message response;
        try
        {
            response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = model,
                MaxTokens = 16000,
                // Reescribir un resumen es una tarea simple: effort bajo responde rápido y gasta poco.
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = [new UserMessageParam { Content = prompt }],
            }, ct);
        }
        catch (AnthropicException e)
        {
            throw new LlmException($"Claude respondió con error: {e.Message}", e);
        }
        catch (HttpRequestException e)
        {
            throw new LlmException($"No se pudo conectar con Claude: {e.Message}", e);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmException($"Claude tardó más de {HttpJson.Timeout.TotalSeconds:0} s en responder");
        }

        if (response.StopReason == "refusal")
            throw new LlmException("Claude no quiso procesar este texto");

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();
        return text.Length > 0 ? text : throw new LlmException("Claude devolvió una respuesta sin texto");
    }
}
