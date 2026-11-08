using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;

namespace DevCockpit.App.Tests;

public class SettingsViewModelTests
{
    private sealed class FakeSettings(AppSettings initial) : ISettingsStore
    {
        public AppSettings Current { get; private set; } = initial;
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Current);
        public Task SaveAsync(AppSettings s, CancellationToken ct) { Current = s; return Task.CompletedTask; }
    }

    private sealed class MemorySecrets : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = [];
        public string? Get(string key) => Values.GetValueOrDefault(key);
        public void Set(string key, string value) => Values[key] = value;
        public void Delete(string key) => Values.Remove(key);
    }

    private sealed class FakeIdentity(string? email) : IGitIdentity
    {
        public Task<string?> GetGlobalEmailAsync(CancellationToken ct) => Task.FromResult(email);
    }

    private sealed class FakePicker(string? path) : IFolderPicker
    {
        public Task<string?> PickFolderAsync() => Task.FromResult(path);
    }

    private static SettingsViewModel Vm(FakeSettings settings, MemorySecrets? secrets = null, string? gitEmail = null,
        string? pickedFolder = null)
        => new(settings, secrets ?? new MemorySecrets(), new FakeIdentity(gitEmail), _ => null, new FakePicker(pickedFolder));

    [Fact]
    public async Task Settings_save_persists_and_key_goes_to_secret_store()
    {
        var settings = new FakeSettings(new AppSettings { CloudConfirmedRepos = ["api"] });
        var secrets = new MemorySecrets();
        var vm = Vm(settings, secrets);
        await vm.LoadAsync();

        vm.Provider = LlmProviderKind.Anthropic;
        vm.AnthropicKey = "sk-ant-secreta";
        vm.AuthorEmailsText = "yo@mail.com\n trabajo@empresa.com ,";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(LlmProviderKind.Anthropic, settings.Current.Provider);
        Assert.Equal(["yo@mail.com", "trabajo@empresa.com"], settings.Current.AuthorEmails);
        Assert.Equal(["api"], settings.Current.CloudConfirmedRepos); // no se pierde lo ya aprobado
        Assert.Equal("sk-ant-secreta", secrets.Get("anthropic"));
        Assert.DoesNotContain("sk-ant-secreta", System.Text.Json.JsonSerializer.Serialize(settings.Current));
        Assert.Equal("", vm.AnthropicKey); // el campo se vacía: la key no queda en memoria de la vista
        Assert.True(vm.HasAnthropicKey);
    }

    [Fact]
    public async Task Invalid_ollama_url_is_not_saved()
    {
        var settings = new FakeSettings(new AppSettings());
        var vm = Vm(settings);
        await vm.LoadAsync();

        vm.OllamaUrl = "127.0.0.1:11434";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("http://localhost:11434", settings.Current.OllamaUrl);
        Assert.Equal("No se guardó: la URL de Ollama tiene que ser como http://localhost:11434", vm.Status);
    }

    private sealed class BrokenSecrets : ISecretStore
    {
        public string? Get(string key) => null;
        public void Set(string key, string value) => throw new System.ComponentModel.Win32Exception(5, "Acceso denegado");
        public void Delete(string key) { }
    }

    [Fact]
    public async Task Secret_store_failure_is_shown_not_thrown()
    {
        var vm = new SettingsViewModel(new FakeSettings(new AppSettings()), new BrokenSecrets(), new FakeIdentity(null),
            _ => null, new FakePicker(null));
        await vm.LoadAsync();

        vm.AnthropicKey = "sk";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("No se pudo guardar: Acceso denegado", vm.Status);
    }

    [Fact]
    public async Task Test_ai_unexpected_error_is_shown_not_thrown()
    {
        var vm = new SettingsViewModel(new FakeSettings(new AppSettings()), new MemorySecrets(), new FakeIdentity(null),
            _ => throw new InvalidOperationException("raro"), new FakePicker(null));
        await vm.LoadAsync();

        await vm.TestAiCommand.ExecuteAsync(null);

        Assert.Equal("Falló: raro", vm.Status);
    }

    [Fact]
    public async Task Empty_key_field_does_not_erase_stored_key()
    {
        var secrets = new MemorySecrets();
        secrets.Set("openai", "sk-vieja");
        var vm = Vm(new FakeSettings(new AppSettings()), secrets);
        await vm.LoadAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("sk-vieja", secrets.Get("openai"));
    }

    [Fact]
    public async Task Empty_emails_prefilled_from_git_config()
    {
        var vm = Vm(new FakeSettings(new AppSettings()), gitEmail: "global@mail.com");

        await vm.LoadAsync();

        Assert.Equal("global@mail.com", vm.AuthorEmailsText);
    }

    [Fact]
    public async Task Configured_emails_are_not_overwritten_by_git_config()
    {
        var vm = Vm(new FakeSettings(new AppSettings { AuthorEmails = ["propio@mail.com"] }), gitEmail: "global@mail.com");

        await vm.LoadAsync();

        Assert.Equal("propio@mail.com", vm.AuthorEmailsText);
    }

    [Fact]
    public async Task Add_repo_uses_folder_name_and_ignores_duplicates()
    {
        var vm = Vm(new FakeSettings(new AppSettings()), pickedFolder: @"C:\repos\helpdesk-api");
        await vm.LoadAsync();

        await vm.AddRepoCommand.ExecuteAsync(null);
        await vm.AddRepoCommand.ExecuteAsync(null);

        var repo = Assert.Single(vm.Repos);
        Assert.Equal("helpdesk-api", repo.Name);
        Assert.Equal(@"C:\repos\helpdesk-api", repo.Path);
    }

    [Fact]
    public async Task Invalid_hotkey_shows_warning_and_valid_one_clears_it()
    {
        var vm = Vm(new FakeSettings(new AppSettings { Hotkey = "N" }));

        await vm.LoadAsync();
        Assert.Equal("Atajo inválido: se usa Ctrl+Alt+N. Tiene que llevar Ctrl o Alt y una letra, número o F1-F12.", vm.HotkeyWarning);

        vm.Hotkey = "Ctrl+Shift+K";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(vm.HotkeyWarning);
    }

    [Fact]
    public async Task Cancelled_folder_picker_adds_nothing()
    {
        var vm = Vm(new FakeSettings(new AppSettings()), pickedFolder: null);
        await vm.LoadAsync();

        await vm.AddRepoCommand.ExecuteAsync(null);

        Assert.Empty(vm.Repos);
    }

    [Fact]
    public async Task Theme_is_loaded_and_saved()
    {
        var settings = new FakeSettings(new AppSettings { Theme = AppTheme.Light });
        var vm = Vm(settings);
        await vm.LoadAsync();
        Assert.Equal(AppTheme.Light, vm.Theme);

        vm.Theme = AppTheme.Dark;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
    }

    [Fact]
    public async Task Preload_ai_is_on_by_default_and_can_be_turned_off()
    {
        // Precargar Ollama ocupa ~5 GB de RAM apenas abre la app (arranca con Windows): tiene que poder apagarse.
        var settings = new FakeSettings(new AppSettings());
        var vm = Vm(settings);
        await vm.LoadAsync();
        Assert.True(vm.PreloadAi);

        vm.PreloadAi = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(settings.Current.PreloadAi);
    }

    [Fact]
    public async Task Ai_by_default_is_off_and_can_be_turned_on()
    {
        // La IA local en CPU tarda minutos: no puede quedar tildada sola al abrir la app.
        var settings = new FakeSettings(new AppSettings());
        var vm = Vm(settings);
        await vm.LoadAsync();
        Assert.False(vm.UseAiByDefault);

        vm.UseAiByDefault = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(settings.Current.UseAiByDefault);
    }
}
