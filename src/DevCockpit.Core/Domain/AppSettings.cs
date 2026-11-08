namespace DevCockpit.Core.Domain;

public sealed record AppSettings
{
    public List<WatchedRepo> Repos { get; init; } = [];
    public List<string> AuthorEmails { get; init; } = [];
    public string GitExecutable { get; init; } = "git";
    public ReportLanguage DefaultLanguage { get; init; } = ReportLanguage.Es;
    public LlmProviderKind Provider { get; init; } = LlmProviderKind.Ollama;
    public string OllamaUrl { get; init; } = "http://localhost:11434";
    public string OllamaModel { get; init; } = "qwen2.5:7b-instruct";
    public string AnthropicModel { get; init; } = "claude-haiku-5-5";
    public string OpenAIModel { get; init; } = "";
    public string Hotkey { get; init; } = "Ctrl+Alt+N";
    public List<string> CloudConfirmedRepos { get; init; } = [];
}
