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
            - Each "- [repo] ..." line holds all the commits of that repository separated by ";". Under each heading
              write one single line per repository: "- repo-name: what was done, in a few words".
              Notes without a repository get a line of their own.
            - No introduction or closing remarks.
            - Reply only with the final text, in English.

            Draft:

            """ + templateDraft,
        _ => $"""
            Reescribí este borrador de {(kind == ReportKind.Daily ? "daily" : "resumen semanal")} para que se lea claro y profesional.
            Reglas:
            - No inventes hechos, tareas ni números que no estén en el borrador.
            - Mantené los encabezados en negrita tal cual están.
            - No traduzcas los nombres de los repos (las líneas en negrita y lo que va entre [corchetes]); dejalos exactamente igual.
            - Cada línea "- [repo] ..." junta todos los commits de ese repo separados por ";". Debajo de cada encabezado
              escribí una sola línea por repo: "- nombre-del-repo: qué se hizo, en pocas palabras".
              Las notas sin repo van en una línea propia.
            - Sin introducción ni cierre.
            - Respondé solo con el texto final, en español.

            Borrador:

            """ + templateDraft,
    };
}
