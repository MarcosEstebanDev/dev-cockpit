using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevCockpit.App.Services;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Input;
using DevCockpit.Core.Ports;
using DevCockpit.Infrastructure.Llm;

namespace DevCockpit.App.ViewModels;

public sealed partial class SettingsViewModel(
    ISettingsStore settings,
    ISecretStore secrets,
    IGitIdentity gitIdentity,
    Func<AppSettings, ILlmProvider?> llmFactory,
    IFolderPicker folderPicker) : ViewModelBase
{
    private AppSettings _loaded = new();

    /// <summary>Se dispara después de guardar (por ejemplo, para volver a registrar el atajo).</summary>
    public event Action<AppSettings>? Saved;

    public ObservableCollection<WatchedRepo> Repos { get; } = [];
    public LlmProviderKind[] Providers { get; } = Enum.GetValues<LlmProviderKind>();
    public ReportLanguage[] Languages { get; } = [ReportLanguage.Es, ReportLanguage.En];

    [ObservableProperty] public partial string AuthorEmailsText { get; set; } = "";
    [ObservableProperty] public partial string GitExecutable { get; set; } = "git";
    [ObservableProperty] public partial ReportLanguage DefaultLanguage { get; set; }
    [ObservableProperty] public partial LlmProviderKind Provider { get; set; }
    [ObservableProperty] public partial string OllamaUrl { get; set; } = "";
    [ObservableProperty] public partial string OllamaModel { get; set; } = "";
    [ObservableProperty] public partial string AnthropicModel { get; set; } = "";
    [ObservableProperty] public partial string OpenAIModel { get; set; } = "";
    [ObservableProperty] public partial string Hotkey { get; set; } = "";

    // Los campos de key se usan solo para escribir: nunca se muestran las guardadas.
    [ObservableProperty] public partial string AnthropicKey { get; set; } = "";
    [ObservableProperty] public partial string OpenAIKey { get; set; } = "";
    [ObservableProperty] public partial bool HasAnthropicKey { get; set; }
    [ObservableProperty] public partial bool HasOpenAIKey { get; set; }

    [ObservableProperty] public partial string? Status { get; set; }
    [ObservableProperty] public partial string? HotkeyWarning { get; set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _loaded = await settings.LoadAsync(ct);
        Repos.Clear();
        foreach (var r in _loaded.Repos) Repos.Add(r);

        var emails = _loaded.AuthorEmails;
        if (emails.Count == 0 && await gitIdentity.GetGlobalEmailAsync(ct) is { } globalEmail)
            emails = [globalEmail];
        AuthorEmailsText = string.Join("\n", emails);

        GitExecutable = _loaded.GitExecutable;
        DefaultLanguage = _loaded.DefaultLanguage;
        Provider = _loaded.Provider;
        OllamaUrl = _loaded.OllamaUrl;
        OllamaModel = _loaded.OllamaModel;
        AnthropicModel = _loaded.AnthropicModel;
        OpenAIModel = _loaded.OpenAIModel;
        Hotkey = _loaded.Hotkey;
        RefreshKeyFlags();
        RefreshHotkeyWarning();
    }

    private void RefreshHotkeyWarning()
        => HotkeyWarning = HotkeyParser.TryParse(Hotkey, out _)
            ? null
            : $"Atajo inválido: se usa {HotkeyParser.Default}. Tiene que llevar Ctrl o Alt y una letra, número o F1-F12.";

    private AppSettings Current() => _loaded with
    {
        Repos = [.. Repos],
        AuthorEmails = [.. AuthorEmailsText
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)],
        GitExecutable = string.IsNullOrWhiteSpace(GitExecutable) ? "git" : GitExecutable.Trim(),
        DefaultLanguage = DefaultLanguage,
        Provider = Provider,
        OllamaUrl = OllamaUrl.Trim(),
        OllamaModel = OllamaModel.Trim(),
        AnthropicModel = AnthropicModel.Trim(),
        OpenAIModel = OpenAIModel.Trim(),
        Hotkey = Hotkey.Trim(),
    };

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        var updated = Current();
        await settings.SaveAsync(updated, ct);
        _loaded = updated;

        if (!string.IsNullOrWhiteSpace(AnthropicKey)) secrets.Set(LlmProviderFactory.SecretKey(LlmProviderKind.Anthropic), AnthropicKey.Trim());
        if (!string.IsNullOrWhiteSpace(OpenAIKey)) secrets.Set(LlmProviderFactory.SecretKey(LlmProviderKind.OpenAI), OpenAIKey.Trim());
        AnthropicKey = "";
        OpenAIKey = "";
        RefreshKeyFlags();
        RefreshHotkeyWarning();

        Status = "Guardado";
        Saved?.Invoke(updated);
    }

    [RelayCommand]
    private async Task AddRepoAsync()
    {
        var path = await folderPicker.PickFolderAsync();
        if (path is null) return;
        var full = Path.TrimEndingDirectorySeparator(path);
        if (Repos.Any(r => string.Equals(r.Path, full, StringComparison.OrdinalIgnoreCase))) return;
        Repos.Add(new WatchedRepo(full, Path.GetFileName(full)));
    }

    [RelayCommand]
    private void RemoveRepo(WatchedRepo repo) => Repos.Remove(repo);

    [RelayCommand]
    private async Task TestAiAsync(CancellationToken ct)
    {
        Status = "Probando…";
        try
        {
            var llm = llmFactory(Current());
            if (llm is null) { Status = "No hay proveedor elegido"; return; }
            var answer = await llm.CompleteAsync("Respondé solamente: OK", ct);
            Status = $"{llm.DisplayName} respondió: {answer}";
        }
        catch (LlmException e)
        {
            Status = "Falló: " + e.Message;
        }
    }

    private void RefreshKeyFlags()
    {
        HasAnthropicKey = secrets.Get(LlmProviderFactory.SecretKey(LlmProviderKind.Anthropic)) is { Length: > 0 };
        HasOpenAIKey = secrets.Get(LlmProviderFactory.SecretKey(LlmProviderKind.OpenAI)) is { Length: > 0 };
    }
}
