namespace DevCockpit.Core.Ports;

public sealed record ConfirmationResult(bool Send, bool DontAskAgain);

/// <summary>Pregunta al usuario antes de mandar texto a una IA en la nube. La implementa la interfaz gráfica.</summary>
public interface ICloudSendConfirmation
{
    /// <param name="payload">El texto exacto que se va a enviar.</param>
    /// <param name="repoNames">Repos cuyos commits van en el texto y todavía no fueron aprobados.</param>
    Task<ConfirmationResult> ConfirmAsync(string providerName, string payload, IReadOnlyList<string> repoNames);
}
