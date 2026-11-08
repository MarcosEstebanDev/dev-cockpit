using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Reports;

namespace DevCockpit.App.Tests;

public class BitacoraViewModelTests
{
    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; private set; }
        public Task SetTextAsync(string text) { Text = text; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Generate_sets_text_and_warning()
    {
        (ReportKind, ReportLanguage, bool)? received = null;
        var vm = new ReportPanelViewModel(ReportKind.Weekly, (k, l, ai, _, _) =>
        {
            received = (k, l, ai);
            return Task.FromResult(new ReportResult("**Semana**", false, "No se pudo usar la IA: x"));
        }, new FakeClipboard(), ReportLanguage.En);
        vm.UseAi = true;

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Equal((ReportKind.Weekly, ReportLanguage.En, true), received);
        Assert.Equal("**Semana**", vm.Text);
        Assert.Equal("No se pudo usar la IA: x", vm.Warning);
    }

    [Fact]
    public async Task Successful_generation_clears_previous_warning()
    {
        var results = new Queue<ReportResult>([new("a", false, "aviso"), new("b", true, null)]);
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => Task.FromResult(results.Dequeue()),
            new FakeClipboard(), ReportLanguage.Es);

        await vm.GenerateCommand.ExecuteAsync(null);
        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("b", vm.Text);
        Assert.Null(vm.Warning);
    }

    [Fact]
    public async Task Generate_disables_command_while_running()
    {
        var pending = new TaskCompletionSource<ReportResult>();
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => pending.Task,
            new FakeClipboard(), ReportLanguage.Es);

        var running = vm.GenerateCommand.ExecuteAsync(null);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        Assert.True(vm.IsGenerating);

        pending.SetResult(new ReportResult("listo", false, null));
        await running;
        Assert.True(vm.GenerateCommand.CanExecute(null));
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task Unexpected_error_is_shown_not_thrown()
    {
        // Revisión final I1: una excepción en el comando cerraba toda la app.
        var vm = new ReportPanelViewModel(ReportKind.Daily,
            (_, _, _, _, _) => throw new InvalidOperationException("disco lleno"), new FakeClipboard(), ReportLanguage.Es)
        { Text = "lo que había" };

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("No se pudo generar: disco lleno", vm.Warning);
        Assert.Equal("lo que había", vm.Text);
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task Cancel_stops_generation_and_says_so()
    {
        // Revisión final I6: con Ollama en frío puede tardar minutos; tiene que poder cancelarse.
        var vm = new ReportPanelViewModel(ReportKind.Daily, async (_, _, _, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new ReportResult("nunca", false, null);
        }, new FakeClipboard(), ReportLanguage.Es);

        var running = vm.GenerateCommand.ExecuteAsync(null);
        vm.GenerateCancelCommand.Execute(null);
        await running;

        Assert.Equal("Generación cancelada", vm.Warning);
        Assert.False(vm.IsGenerating);
    }

    private sealed class BrokenClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => throw new InvalidOperationException("portapapeles ocupado");
    }

    [Fact]
    public async Task Copy_failure_is_shown_not_thrown()
    {
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => Task.FromResult(new ReportResult("x", false, null)),
            new BrokenClipboard(), ReportLanguage.Es) { Text = "algo" };

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal("No se pudo copiar: portapapeles ocupado", vm.CopyStatus);
    }

    [Fact]
    public async Task Copy_puts_current_text_in_clipboard()
    {
        var clipboard = new FakeClipboard();
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => Task.FromResult(new ReportResult("x", false, null)),
            clipboard, ReportLanguage.Es) { Text = "editado a mano" };

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal("editado a mano", clipboard.Text);
    }

    private static async Task Until(Func<bool> condition)
    {
        // Progress<T> sin contexto de UI publica en el thread pool: se espera a que llegue.
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(20);
        Assert.True(condition());
    }

    [Fact]
    public async Task Partial_text_is_shown_while_generating()
    {
        var pending = new TaskCompletionSource<ReportResult>();
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, partial, _) =>
        {
            partial?.Report("**Ayer**\n- arreglé");
            return pending.Task;
        }, new FakeClipboard(), ReportLanguage.Es);

        var running = vm.GenerateCommand.ExecuteAsync(null);
        await Until(() => vm.Text == "**Ayer**\n- arreglé");

        pending.SetResult(new ReportResult("final", true, null));
        await running;
        Assert.Equal("final", vm.Text);
    }

    [Fact]
    public async Task Late_partial_never_overwrites_the_final_text()
    {
        IProgress<string>? captured = null;
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, partial, _) =>
        {
            captured = partial;
            return Task.FromResult(new ReportResult("final", true, null));
        }, new FakeClipboard(), ReportLanguage.Es);

        await vm.GenerateCommand.ExecuteAsync(null);
        captured!.Report("parcial atrasado");
        await Task.Delay(100);

        Assert.Equal("final", vm.Text);
    }

    [Fact]
    public async Task Elapsed_counter_runs_only_while_generating()
    {
        var pending = new TaskCompletionSource<ReportResult>();
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => pending.Task,
            new FakeClipboard(), ReportLanguage.Es);

        var running = vm.GenerateCommand.ExecuteAsync(null);
        Assert.Equal("Generando… 0:00", vm.Progress);

        pending.SetResult(new ReportResult("listo", false, null));
        await running;
        Assert.Null(vm.Progress);
    }

    [Fact]
    public void Reading_view_follows_the_text_and_edit_mode_toggles()
    {
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _, _) => Task.FromResult(new ReportResult("", false, null)),
            new FakeClipboard(), ReportLanguage.Es);
        Assert.False(vm.HasText);

        vm.Text = "**Ayer** (vie 06/11)\n- [api] fix: algo";

        Assert.True(vm.HasText);
        Assert.Equal(2, vm.Lines.Count);
        Assert.Equal("api", vm.Lines[1].Repo);
        Assert.False(vm.IsEditing);

        vm.ToggleEditCommand.Execute(null);
        Assert.True(vm.IsEditing);
    }
}
