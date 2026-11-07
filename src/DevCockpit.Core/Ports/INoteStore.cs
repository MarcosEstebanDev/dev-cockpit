using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Ports;

public interface INoteStore
{
    Task AddAsync(Note note, CancellationToken ct);
    Task<IReadOnlyList<Note>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<Note>> ListAchievementsAsync(CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
