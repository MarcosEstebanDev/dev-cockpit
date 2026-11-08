using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace DevCockpit.App.Services;

/// <summary>
/// Garantiza una sola instancia por usuario. La segunda instancia avisa a la primera por un
/// named pipe ("mostrate") y termina; así no aparecen dos íconos ni dos atajos globales.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Name = "DevCockpit-" + Environment.UserName;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + Name, out var created);
        IsFirst = created;
    }

    /// <summary>Desde la segunda instancia: le pide a la primera que muestre su ventana.</summary>
    public static void SignalFirstInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Name, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("show");
        }
        catch (Exception e) when (e is TimeoutException or IOException) { }
    }

    /// <summary>En la primera instancia: escucha pedidos de las siguientes.</summary>
    public void Listen(Action onShowRequested)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(Name, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server);
                    if (await reader.ReadLineAsync(_cts.Token) == "show") onShowRequested();
                }
                catch (OperationCanceledException) { return; }
                catch (IOException) { }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
