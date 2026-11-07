using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Reports;

/// <summary>
/// La IA recibe el borrador de la plantilla, no los datos crudos: así lo que el usuario confirma
/// es exactamente lo que se envía, y la IA solo puede reescribir hechos que ya están ahí.
/// </summary>
public static class PromptBuilder
{
    public static string For(ReportKind kind, ReportLanguage lang, string templateDraft) => lang switch
    {
        ReportLanguage.En => $"""
            Rewrite this draft {(kind == ReportKind.Daily ? "daily stand-up update" : "weekly summary")} so it reads clearly and professionally.
            Rules:
            - Do not invent facts, tasks or numbers that are not in the draft.
            - Keep the bold headings exactly as they are.
            - Do not translate repository names (the bold lines and the names in [brackets]); keep them exactly as written.
            - Merge commits that describe the same work into one line.
            - Reply only with the final text, in English.

            Draft:

            """ + templateDraft,
        _ => $"""
            Reescribí este borrador de {(kind == ReportKind.Daily ? "daily" : "resumen semanal")} para que se lea claro y profesional.
            Reglas:
            - No inventes hechos, tareas ni números que no estén en el borrador.
            - Mantené los encabezados en negrita tal cual están.
            - No traduzcas los nombres de los repos (las líneas en negrita y lo que va entre [corchetes]); dejalos exactamente igual.
            - Uní en una sola línea los commits que describen el mismo trabajo.
            - Respondé solo con el texto final, en español.

            Borrador:

            """ + templateDraft,
    };
}
