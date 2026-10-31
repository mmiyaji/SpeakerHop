using System.IO.Pipes;

namespace SpeakerHop.Services;

/// <summary>Delivers manual relaunch requests to the existing instance for this user.</summary>
public sealed class ActivationChannel : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _listener;
    private int _disposed;

    public ActivationChannel(string pipeName, Action showSettings)
    {
        _listener = ListenAsync(pipeName, showSettings, _shutdown.Token);
    }

    public static async Task<bool> RequestSettingsAsync(string pipeName, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(cancellation.Token);
            await client.WriteAsync(new byte[] { 1 }, cancellation.Token);
            await client.FlushAsync(cancellation.Token);
            return true;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _shutdown.Cancel();
        // Dispose the token source only after pending pipe I/O has observed cancellation.
        _ = _listener.ContinueWith(_ => _shutdown.Dispose(), TaskScheduler.Default);
    }

    private static async Task ListenAsync(string pipeName, Action showSettings, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellation);
                var message = new byte[1];
                // A connected client must not be able to monopolize the activation channel.
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                readTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                if (await server.ReadAsync(message, readTimeout.Token) == 1 && message[0] == 1)
                {
                    showSettings();
                }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                // Drop an unresponsive client and accept another request.
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellation).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }
}
