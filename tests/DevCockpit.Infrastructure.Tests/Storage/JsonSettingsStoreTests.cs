using DevCockpit.Core.Domain;
using DevCockpit.Infrastructure.Storage;

namespace DevCockpit.Infrastructure.Tests.Storage;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("devcockpit-settings-").FullName;
    private string File_ => Path.Combine(_dir, "settings.json");
    private readonly CancellationToken _ct = CancellationToken.None;

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public async Task Load_missing_file_returns_defaults()
    {
        var s = await new JsonSettingsStore(File_).LoadAsync(_ct);

        Assert.Equal("qwen2.5:7b-instruct", s.OllamaModel);
        Assert.Equal("http://localhost:11434", s.OllamaUrl);
        Assert.Equal("Ctrl+Alt+N", s.Hotkey);
        Assert.Equal(LlmProviderKind.Ollama, s.Provider);
        Assert.Empty(s.Repos);
    }

    [Fact]
    public async Task Load_corrupt_file_returns_defaults_and_keeps_backup()
    {
        File.WriteAllText(File_, "{ esto no es json");

        var s = await new JsonSettingsStore(File_).LoadAsync(_ct);

        Assert.Equal("Ctrl+Alt+N", s.Hotkey);
        Assert.Equal("{ esto no es json", File.ReadAllText(File_ + ".bad"));
    }

    [Fact]
    public async Task Save_then_load_roundtrip()
    {
        var store = new JsonSettingsStore(File_);
        var original = new AppSettings
        {
            Repos = [new WatchedRepo(@"C:\repos\api", "api")],
            AuthorEmails = ["yo@mail.com"],
            Provider = LlmProviderKind.Anthropic,
            DefaultLanguage = ReportLanguage.En,
            CloudConfirmedRepos = ["api"],
        };

        await store.SaveAsync(original, _ct);
        var loaded = await store.LoadAsync(_ct);

        Assert.Equal("api", Assert.Single(loaded.Repos).Name);
        Assert.Equal(["yo@mail.com"], loaded.AuthorEmails);
        Assert.Equal(LlmProviderKind.Anthropic, loaded.Provider);
        Assert.Equal(ReportLanguage.En, loaded.DefaultLanguage);
        Assert.Equal(["api"], loaded.CloudConfirmedRepos);
        Assert.Contains("\"Anthropic\"", File.ReadAllText(File_)); // enums legibles en el JSON
    }

    [Fact]
    public async Task Save_creates_missing_directory()
    {
        var nested = Path.Combine(_dir, "a", "b", "settings.json");

        await new JsonSettingsStore(nested).SaveAsync(new AppSettings(), _ct);

        Assert.True(File.Exists(nested));
    }
}
