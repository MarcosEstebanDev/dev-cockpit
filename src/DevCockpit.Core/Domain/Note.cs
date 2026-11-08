namespace DevCockpit.Core.Domain;

public sealed record Note(Guid Id, DateTimeOffset CreatedAt, string Text, bool IsAchievement, string? Metric);
