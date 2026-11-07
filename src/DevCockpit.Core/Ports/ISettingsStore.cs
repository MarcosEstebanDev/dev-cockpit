using DevCockpit.Core.Domain;

namespace DevCockpit.Core.Ports;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct);
    Task SaveAsync(AppSettings settings, CancellationToken ct);
}
