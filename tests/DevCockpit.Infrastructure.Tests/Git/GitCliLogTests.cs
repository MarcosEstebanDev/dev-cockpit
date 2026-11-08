using DevCockpit.Core.Domain;
using DevCockpit.Infrastructure.Git;

namespace DevCockpit.Infrastructure.Tests.Git;

public class GitCliLogTests : IDisposable
{
    private static readonly TimeSpan Ar = TimeSpan.FromHours(-3);
    private static readonly DateTimeOffset Day = new(2026, 11, 6, 10, 0, 0, Ar);
    private readonly TempRepo _repo = new();
    private readonly GitCliLog _git = new(TempRepo.Git);

    public void Dispose() => _repo.Dispose();

    private Task<Core.Ports.GitLogResult> Read(params string[] emails)
        => _git.GetCommitsAsync(new WatchedRepo(_repo.Path, "demo"), emails, Day.AddDays(-1), Day.AddDays(1), CancellationToken.None);

    [Fact]
    public async Task Returns_only_commits_of_given_authors()
    {
        _repo.Commit("yo@mail.com", "mío", Day);
        _repo.Commit("otro@mail.com", "ajeno", Day);

        var result = await Read("yo@mail.com");

        Assert.Null(result.Error);
        var commit = Assert.Single(result.Commits);
        Assert.Equal("mío", commit.Subject);
        Assert.Equal("demo", commit.RepoName);
        Assert.Equal("yo@mail.com", commit.AuthorEmail);
        Assert.Equal(Day, commit.When);
        Assert.Equal(40, commit.Sha.Length);
    }

    [Fact]
    public async Task Several_emails_are_combined()
    {
        _repo.Commit("trabajo@empresa.com", "a", Day);
        _repo.Commit("personal@mail.com", "b", Day.AddMinutes(1));

        var result = await Read("trabajo@empresa.com", "personal@mail.com");

        Assert.Equal(2, result.Commits.Count);
    }

    [Fact]
    public async Task Email_with_regex_chars_matches_literally()
    {
        _repo.Commit("marcos.godoy+work@mail.com", "literal", Day);
        _repo.Commit("marcosXgodoy+work@mail.com", "parecido", Day.AddMinutes(1));
        _repo.Commit("otro.marcos.godoy+work@mail.com", "contiene", Day.AddMinutes(2));

        var result = await Read("marcos.godoy+work@mail.com");

        Assert.Equal("literal", Assert.Single(result.Commits).Subject);
    }

    [Fact]
    public async Task Email_match_ignores_case()
    {
        _repo.Commit("Marcos@Mail.com", "mayúsculas", Day);

        var result = await Read("marcos@mail.com");

        Assert.Single(result.Commits);
    }

    [Fact]
    public async Task Preserves_unicode_subject()
    {
        _repo.Commit("yo@mail.com", "fix: ñandú áéí 🚀", Day);

        var result = await Read("yo@mail.com");

        Assert.Equal("fix: ñandú áéí 🚀", Assert.Single(result.Commits).Subject);
    }

    [Fact]
    public async Task Respects_date_window()
    {
        _repo.Commit("yo@mail.com", "viejo", Day.AddDays(-5));
        _repo.Commit("yo@mail.com", "adentro", Day);
        _repo.Commit("yo@mail.com", "futuro", Day.AddDays(5));

        var result = await Read("yo@mail.com");

        Assert.Equal("adentro", Assert.Single(result.Commits).Subject);
    }

    [Fact]
    public async Task Includes_commits_from_other_branches()
    {
        _repo.Commit("yo@mail.com", "en main", Day);
        _repo.Run("switch", "-q", "-c", "feature");
        _repo.Commit("yo@mail.com", "en feature", Day.AddMinutes(1));
        _repo.Run("switch", "-q", "main");

        var result = await Read("yo@mail.com");

        Assert.Equal(2, result.Commits.Count);
    }

    [Fact]
    public async Task Missing_folder_returns_error_not_exception()
    {
        var result = await _git.GetCommitsAsync(new WatchedRepo(@"C:\no\existe\" + Guid.NewGuid(), "fantasma"),
            ["yo@mail.com"], Day.AddDays(-1), Day.AddDays(1), CancellationToken.None);

        Assert.Empty(result.Commits);
        Assert.Equal("La carpeta no existe", result.Error);
    }

    [Fact]
    public async Task Non_repo_folder_returns_error()
    {
        var dir = Directory.CreateTempSubdirectory("devcockpit-norepo-").FullName;
        try
        {
            var result = await _git.GetCommitsAsync(new WatchedRepo(dir, "suelto"),
                ["yo@mail.com"], Day.AddDays(-1), Day.AddDays(1), CancellationToken.None);

            Assert.Empty(result.Commits);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Repo_without_commits_returns_empty_without_error()
    {
        var result = await Read("yo@mail.com");

        Assert.Empty(result.Commits);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Global_email_is_read_from_global_config()
    {
        var config = Path.Combine(_repo.Path, "global.gitconfig");
        File.WriteAllText(config, "[user]\n\temail = global@mail.com\n");
        var git = new GitCliLog(TempRepo.Git, new Dictionary<string, string> { ["GIT_CONFIG_GLOBAL"] = config });

        Assert.Equal("global@mail.com", await git.GetGlobalEmailAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Global_email_is_null_when_not_configured()
    {
        var config = Path.Combine(_repo.Path, "vacio.gitconfig");
        File.WriteAllText(config, "");
        var git = new GitCliLog(TempRepo.Git, new Dictionary<string, string> { ["GIT_CONFIG_GLOBAL"] = config });

        Assert.Null(await git.GetGlobalEmailAsync(CancellationToken.None));
    }
}
