namespace DevCockpit.Core.Ports;

/// <summary>Almacén seguro del sistema operativo para API keys.</summary>
public interface ISecretStore
{
    string? Get(string key);
    void Set(string key, string value);
    void Delete(string key);
}
