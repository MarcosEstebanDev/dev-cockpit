namespace DevCockpit.Core.Ports;

public interface ILlmProvider
{
    string DisplayName { get; }

    /// <summary>true si corre en la PC (Ollama): no hace falta pedir confirmación antes de enviar.</summary>
    bool IsLocal { get; }

    /// <exception cref="LlmException">Cualquier falla de red, HTTP o respuesta inesperada.</exception>
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}

public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);
