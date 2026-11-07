using DevCockpit.Core.Domain;
using DevCockpit.Infrastructure.Storage;

namespace DevCockpit.Infrastructure.Tests.Storage;

public class SqliteNoteStoreTests : IDisposable
{
    private static readonly TimeSpan Ar = TimeSpan.FromHours(-3);
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"devcockpit-{Guid.NewGuid():N}.db");
    private readonly SqliteNoteStore _store;
    private readonly CancellationToken _ct = CancellationToken.None;

    public SqliteNoteStoreTests() => _store = new SqliteNoteStore($"Data Source={_file};Pooling=False");

    public void Dispose() => File.Delete(_file);

    private static Note N(string text, DateTimeOffset at, bool achievement = false, string? metric = null)
        => new(Guid.NewGuid(), at, text, achievement, metric);

    [Fact]
    public async Task Add_then_list_in_window()
    {
        var note = N("hola ñandú", new DateTimeOffset(2026, 11, 6, 10, 0, 0, Ar), true, "x3");
        await _store.AddAsync(note, _ct);

        var listed = await _store.ListAsync(note.CreatedAt.AddHours(-1), note.CreatedAt.AddHours(1), _ct);

        Assert.Equal(note, Assert.Single(listed));
    }

    [Fact]
    public async Task List_excludes_outside_window()
    {
        var at = new DateTimeOffset(2026, 11, 6, 10, 0, 0, Ar);
        await _store.AddAsync(N("antes", at.AddDays(-2)), _ct);
        await _store.AddAsync(N("adentro", at), _ct);
        await _store.AddAsync(N("después", at.AddDays(2)), _ct);

        var listed = await _store.ListAsync(at.AddHours(-1), at.AddHours(1), _ct);

        Assert.Equal("adentro", Assert.Single(listed).Text);
    }

    [Fact]
    public async Task Window_compares_instants_not_text_across_offsets()
    {
        // 10:00 en Argentina = 13:00 UTC: tiene que entrar en una ventana expresada en UTC.
        await _store.AddAsync(N("ar", new DateTimeOffset(2026, 11, 6, 10, 0, 0, Ar)), _ct);

        var listed = await _store.ListAsync(
            new DateTimeOffset(2026, 11, 6, 12, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 6, 13, 30, 0, TimeSpan.Zero), _ct);

        Assert.Single(listed);
    }

    [Fact]
    public async Task Achievements_only_flagged()
    {
        var at = new DateTimeOffset(2026, 11, 6, 10, 0, 0, Ar);
        await _store.AddAsync(N("común", at), _ct);
        await _store.AddAsync(N("logro", at, true, "4 s a 300 ms"), _ct);

        var achievements = await _store.ListAchievementsAsync(_ct);

        var only = Assert.Single(achievements);
        Assert.Equal("logro", only.Text);
        Assert.Equal("4 s a 300 ms", only.Metric);
    }

    [Fact]
    public async Task Delete_removes_note()
    {
        var note = N("borrame", new DateTimeOffset(2026, 11, 6, 10, 0, 0, Ar));
        await _store.AddAsync(note, _ct);

        await _store.DeleteAsync(note.Id, _ct);

        Assert.Empty(await _store.ListAsync(note.CreatedAt.AddDays(-1), note.CreatedAt.AddDays(1), _ct));
    }
}
