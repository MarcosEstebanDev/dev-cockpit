namespace DevCockpit.Core.Domain;

public sealed record Commit(string RepoName, string Sha, string AuthorEmail, DateTimeOffset When, string Subject);
