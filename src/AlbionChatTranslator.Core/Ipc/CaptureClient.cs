using AlbionChatTranslator.Core.Chat;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace AlbionChatTranslator.Core.Ipc;

/// <summary>
/// UI-side half of the capture link: opens a private pipe, starts the elevated helper
/// (one UAC prompt) and raises events for whatever the helper reports.
/// </summary>
public sealed class CaptureClient : IAsyncDisposable
{
    private const int ErrorCancelled = 1223; // ERROR_CANCELLED: the user said "No" to UAC

    private CancellationTokenSource? _cts;
    private Task? _readLoop;
    private Process? _helper;

    public event Action<ChatLine>? LineReceived;
    public event Action<IReadOnlyList<ChatTab>>? TabsReceived;
    public event Action<string, bool>? StatusChanged;
    public event Action? Stopped;

    public bool IsRunning => _readLoop is { IsCompleted: false };

    /// <summary>Starts the helper. Returns false (and raises StatusChanged) if it could not start.</summary>
    public async Task<bool> StartAsync(string helperExePath)
    {
        if (IsRunning) return true;

        var pipeName = PipeProtocol.NewPipeName();
        var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        try
        {
            _helper = Process.Start(new ProcessStartInfo(helperExePath)
            {
                ArgumentList = { CaptureHost.Argument, pipeName, Environment.ProcessId.ToString() },
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            await server.DisposeAsync();
            StatusChanged?.Invoke("Administrator permission was declined — chat capture needs it.", false);
            return false;
        }
        catch (Win32Exception e)
        {
            await server.DisposeAsync();
            StatusChanged?.Invoke($"Could not start capture: {e.Message}", false);
            return false;
        }

        _cts = new CancellationTokenSource();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await server.WaitForConnectionAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            await server.DisposeAsync();
            StatusChanged?.Invoke(_helper is { HasExited: true }
                ? "Capture helper exited early. If you approved admin with a different Windows account, sign in with an admin account instead."
                : "Capture helper did not respond.", false);
            KillHelper();
            return false;
        }

        _readLoop = Task.Run(() => ReadLoopAsync(server, _cts.Token));
        return true;
    }

    private async Task ReadLoopAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        await using (server)
        using (var reader = new StreamReader(server, new UTF8Encoding(false)))
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break; // helper exited
                    var msg = PipeProtocol.TryDeserialize(line);
                    if (msg is null) continue;

                    switch (msg.Type)
                    {
                        case PipeMessage.TypeLine when msg.Line is { } chat:
                            LineReceived?.Invoke(chat);
                            break;
                        case PipeMessage.TypeTabs when msg.Tabs is { } tabs:
                            TabsReceived?.Invoke(tabs);
                            break;
                        case PipeMessage.TypeStatus when msg.Status is { } status:
                            StatusChanged?.Invoke(status, msg.Ok);
                            break;
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException) { }
        }
        Stopped?.Invoke();
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_readLoop is not null)
        {
            try { await _readLoop.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (TimeoutException) { }
        }
        KillHelper();
        _cts?.Dispose();
        _cts = null;
        _readLoop = null;
    }

    // The helper also exits by itself when the pipe closes; this is a backstop.
    private void KillHelper()
    {
        try
        {
            if (_helper is { HasExited: false }) _helper.Kill();
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception) { }
        _helper?.Dispose();
        _helper = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
