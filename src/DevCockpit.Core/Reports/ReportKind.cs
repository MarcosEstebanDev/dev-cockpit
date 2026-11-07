namespace DevCockpit.Core.Reports;

public enum ReportKind { Daily, Weekly }

/// <param name="Text">El resumen final (de la IA o de la plantilla).</param>
/// <param name="UsedAi">true si el texto lo redactó la IA.</param>
/// <param name="Warning">Aviso para mostrar si la IA no se pudo usar o se canceló.</param>
public sealed record ReportResult(string Text, bool UsedAi, string? Warning);
