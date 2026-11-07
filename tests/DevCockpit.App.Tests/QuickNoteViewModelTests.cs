using DevCockpit.App.ViewModels;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Time;

namespace DevCockpit.App.Tests;

public class QuickNoteViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 15, 30, 0, TimeSpan.FromHours(-3));

    private sealed class FakeClock : IClock { public DateTimeOffset Now => QuickNoteViewModelTests.Now; }

    private sealed class FakeNotes : INoteStore
    {
        public List<Note> Notes { get; } = [];
        public Task AddAsync(Note n, CancellationToken ct) { Notes.Add(n); return Task.CompletedTask; }
        public Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset f, DateTimeOffset t, CancellationToken ct) => Task.FromResult<IReadOnlyList<Note>>(Notes);
        public Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Note>>(Notes);
        public Task DeleteAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task Save_stores_note_and_requests_close()
    {
        var notes = new FakeNotes();
        var vm = new QuickNoteViewModel(notes, new FakeClock()) { Text = "  arreglé el timeout  " };
        var closed = false;
        vm.CloseRequested += () => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        var note = Assert.Single(notes.Notes);
        Assert.Equal("arreglé el timeout", note.Text);
        Assert.Equal(Now, note.CreatedAt);
        Assert.False(note.IsAchievement);
        Assert.Null(note.Metric);
        Assert.True(closed);
    }

    [Fact]
    public async Task Save_stores_metric_only_if_achievement()
    {
        var notes = new FakeNotes();
        var vm = new QuickNoteViewModel(notes, new FakeClock()) { Text = "query más rápida", Metric = "4 s a 300 ms" };

        await vm.SaveCommand.ExecuteAsync(null); // no es logro: la métrica se descarta
        vm.Text = "query más rápida";
        vm.IsAchievement = true;
        vm.Metric = "4 s a 300 ms";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(notes.Notes[0].Metric);
        Assert.True(notes.Notes[1].IsAchievement);
        Assert.Equal("4 s a 300 ms", notes.Notes[1].Metric);
    }

    [Fact]
    public async Task Empty_text_does_not_save_nor_close()
    {
        var notes = new FakeNotes();
        var vm = new QuickNoteViewModel(notes, new FakeClock()) { Text = "   " };
        var closed = false;
        vm.CloseRequested += () => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(notes.Notes);
        Assert.False(closed);
    }

    private sealed class BrokenNotes : INoteStore
    {
        public Task AddAsync(Note n, CancellationToken ct) => throw new IOException("database is locked");
        public Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset f, DateTimeOffset t, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Save_failure_keeps_text_and_window_open()
    {
        // Revisión final I1: un error de SQLite cerraba la app y se perdía la nota escrita.
        var vm = new QuickNoteViewModel(new BrokenNotes(), new FakeClock()) { Text = "no la pierdas" };
        var closed = false;
        vm.CloseRequested += () => closed = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("no la pierdas", vm.Text);
        Assert.False(closed);
        Assert.Equal("No se pudo guardar: database is locked", vm.Error);
    }

    [Fact]
    public async Task Fields_are_cleared_after_save_for_the_next_note()
    {
        var vm = new QuickNoteViewModel(new FakeNotes(), new FakeClock()) { Text = "x", IsAchievement = true, Metric = "y" };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("", vm.Text);
        Assert.False(vm.IsAchievement);
        Assert.Equal("", vm.Metric);
    }
}
