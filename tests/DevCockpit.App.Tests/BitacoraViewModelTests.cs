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
        var vm = new ReportPanelViewModel(ReportKind.Weekly, (k, l, ai, _) =>
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
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _) => Task.FromResult(results.Dequeue()),
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
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _) => pending.Task,
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
    public async Task Copy_puts_current_text_in_clipboard()
    {
        var clipboard = new FakeClipboard();
        var vm = new ReportPanelViewModel(ReportKind.Daily, (_, _, _, _) => Task.FromResult(new ReportResult("x", false, null)),
            clipboard, ReportLanguage.Es) { Text = "editado a mano" };

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal("editado a mano", clipboard.Text);
    }
}
