using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Reports;

public sealed record ReportInput(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<Commit> Commits,
    IReadOnlyList<Note> Notes,
    IReadOnlyList<string> RepoErrors);
