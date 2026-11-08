using System.Globalization;
using Dapper;
using DevCockpit.Core.Domain;
using DevCockpit.Core.Ports;
using Microsoft.Data.Sqlite;

namespace DevCockpit.Infrastructure.Storage;

/// <summary>
/// Notas en SQLite. Se guardan dos columnas de fecha: <c>created_utc</c> (ticks UTC, para filtrar
/// por ventana sin errores de zona horaria) y <c>created_at</c> (ISO con offset, para mostrarla tal cual).
/// </summary>
public sealed class SqliteNoteStore(string connectionString) : INoteStore
{
    private bool _initialized;

    private sealed class Row
    {
        public string id { get; init; } = "";
        public string created_at { get; init; } = "";
        public string text { get; init; } = "";
        public long is_achievement { get; init; }
        public string? metric { get; init; }

        public Note ToNote() => new(
            Guid.Parse(id),
            DateTimeOffset.Parse(created_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            text, is_achievement != 0, metric);
    }

    private const string Columns = "id, created_at, text, is_achievement, metric";

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        if (!_initialized)
        {
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS notes (
                    id             TEXT PRIMARY KEY,
                    created_utc    INTEGER NOT NULL,
                    created_at     TEXT NOT NULL,
                    text           TEXT NOT NULL,
                    is_achievement INTEGER NOT NULL,
                    metric         TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_notes_created_utc ON notes(created_utc);
                """);
            _initialized = true;
        }
        return conn;
    }

    public async Task AddAsync(Note note, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO notes (id, created_utc, created_at, text, is_achievement, metric) VALUES (@id, @utc, @at, @text, @ach, @metric)",
            new
            {
                id = note.Id.ToString(),
                utc = note.CreatedAt.UtcTicks,
                at = note.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                text = note.Text,
                ach = note.IsAchievement ? 1 : 0,
                metric = note.Metric,
            }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM notes WHERE created_utc >= @from AND created_utc <= @to ORDER BY created_utc",
            new { from = from.UtcTicks, to = to.UtcTicks }, cancellationToken: ct));
        return rows.Select(r => r.ToNote()).ToList();
    }

    public async Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM notes WHERE is_achievement = 1 ORDER BY created_utc DESC",
            cancellationToken: ct));
        return rows.Select(r => r.ToNote()).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM notes WHERE id = @id", new { id = id.ToString() }, cancellationToken: ct));
    }
}
