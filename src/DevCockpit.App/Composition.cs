using System;
using System.Net.Http;
using System.Threading;
using DevCockpit.App.Services;
using DevCockpit.App.ViewModels;
using DevCockpit.Core.Ports;
using DevCockpit.Core.Time;
using DevCockpit.Infrastructure.Llm;
using DevCockpit.Infrastructure.Storage;
using DevCockpit.Infrastructure.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace DevCockpit.App;

/// <summary>
/// Armado de dependencias: el equivalente al módulo de NestJS o a la configuración de beans de Spring.
/// Es el único lugar que conoce las implementaciones concretas.
/// </summary>
public static class Composition
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(AppPaths.SettingsFile));
        services.AddSingleton<INoteStore>(_ =>
        {
            System.IO.Directory.CreateDirectory(AppPaths.DataDir);
            return new SqliteNoteStore($"Data Source={AppPaths.DatabaseFile}");
        });
        services.AddSingleton<ISecretStore>(_ => OperatingSystem.IsWindows()
            ? new CredentialManagerSecretStore()
            : throw new PlatformNotSupportedException("Por ahora las API keys solo se guardan en Windows."));
        services.AddSingleton<ConfiguredGit>();
        services.AddSingleton<IGitLog>(sp => sp.GetRequiredService<ConfiguredGit>());
        services.AddSingleton<IGitIdentity>(sp => sp.GetRequiredService<ConfiguredGit>());

        // Cada proveedor maneja su propio timeout (120 s); el HttpClient compartido no corta antes.
        services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        services.AddSingleton<LlmProviderFactory>();

        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
