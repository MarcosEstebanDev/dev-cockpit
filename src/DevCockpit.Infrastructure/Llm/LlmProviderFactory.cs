using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Llm;

/// <summary>Arma el proveedor elegido en la configuración, leyendo la API key del almacén seguro.</summary>
public sealed class LlmProviderFactory(HttpClient http, ISecretStore secrets)
{
    public static string SecretKey(LlmProviderKind kind) => kind.ToString().ToLowerInvariant();

    public ILlmProvider? Create(AppSettings s) => s.Provider switch
    {
        LlmProviderKind.None => null,
        LlmProviderKind.Ollama => new OllamaProvider(http, s.OllamaUrl, s.OllamaModel),
        LlmProviderKind.Anthropic => new AnthropicProvider(http, KeyOrThrow(LlmProviderKind.Anthropic, "Claude"),
            Required(s.AnthropicModel, "Claude")),
        LlmProviderKind.OpenAI => new OpenAIProvider(http, KeyOrThrow(LlmProviderKind.OpenAI, "ChatGPT"),
            Required(s.OpenAIModel, "ChatGPT")),
        _ => throw new LlmException($"Proveedor desconocido: {s.Provider}"),
    };

    private string KeyOrThrow(LlmProviderKind kind, string name) =>
        secrets.Get(SecretKey(kind)) is { Length: > 0 } key
            ? key
            : throw new LlmException($"Falta la API key de {name}. Cargala en Configuración.");

    private static string Required(string model, string name) =>
        string.IsNullOrWhiteSpace(model)
            ? throw new LlmException($"Falta elegir el modelo de {name} en Configuración.")
            : model;
}
