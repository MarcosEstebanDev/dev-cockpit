namespace DevCockpit.Core.Ports;

public interface ILlmProvider
{
    string DisplayName { get; }

    /// <summary>true si corre en la PC (Ollama): no hace falta pedir confirmación antes de enviar.</summary>
    bool IsLocal { get; }

    /// <exception cref="LlmException">Cualquier falla de red, HTTP o respuesta inesperada.</exception>
    Task<string> CompleteAsync(string prompt, CancellationToken ct);

    /// <summary>
    /// Igual que <see cref="CompleteAsync(string, CancellationToken)"/>, pero informa el texto acumulado
    /// a medida que llega. Por defecto no hay streaming: el texto aparece todo junto al final.
    /// </summary>
    Task<string> CompleteAsync(string prompt, IProgress<string>? partial, CancellationToken ct)
        => CompleteAsync(prompt, ct);
}

public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);
