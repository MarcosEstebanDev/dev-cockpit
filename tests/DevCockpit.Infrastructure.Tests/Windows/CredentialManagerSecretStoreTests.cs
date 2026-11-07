using DevCockpit.Infrastructure.Windows;

namespace DevCockpit.Infrastructure.Tests.Windows;

[Trait("Category", "Windows")]
public class CredentialManagerSecretStoreTests
{
    [Fact]
    public void SecretStore_set_get_delete_roundtrip()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new CredentialManagerSecretStore();
        var key = "test-" + Guid.NewGuid().ToString("N");
        try
        {
            store.Set(key, "sk-ñandú-123");
            Assert.Equal("sk-ñandú-123", store.Get(key));

            store.Set(key, "otra");
            Assert.Equal("otra", store.Get(key));
        }
        finally
        {
            store.Delete(key);
        }
        Assert.Null(store.Get(key));
    }

    [Fact]
    public void Get_missing_returns_null_and_delete_missing_is_noop()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new CredentialManagerSecretStore();
        var key = "no-existe-" + Guid.NewGuid().ToString("N");

        Assert.Null(store.Get(key));
        store.Delete(key);
    }
}
