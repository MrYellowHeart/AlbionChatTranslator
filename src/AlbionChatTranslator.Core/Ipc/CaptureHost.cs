using AlbionChatTranslator.Core.Capture;
using AlbionChatTranslator.Core.Chat;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace AlbionChatTranslator.Core.Ipc;

/// <summary>
/// Entry point of the elevated capture helper (<c>AlbionChatTranslator.exe --capture ...</c>).
/// Its only job: read Albion chat packets and forward parsed lines to the UI over a pipe.
/// No UI, no downloads, no files, no settings — the smallest possible thing running as admin.
/// </summary>
public static class CaptureHost
{
    public const string Argument = "--capture";

    public static int Run(string pipeName, int parentPid)
    {
        if (!PipeProtocol.IsValidPipeName(pipeName)) return 2;

        Process parent;
        try { parent = Process.GetProcessById(parentPid); }
        catch (ArgumentException) { return 3; }

        // CurrentUserOnly: refuse to talk to a pipe owned by anyone but the signed-in user.
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
        try { pipe.Connect(TimeSpan.FromSeconds(15)); }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException) { return 4; }

        using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        var writeLock = new Lock();
        using var done = new ManualResetEventSlim();

        void Send(PipeMessage message)
        {
            lock (writeLock)
            {
                try { writer.WriteLine(PipeProtocol.Serialize(message)); }
                catch (IOException) { done.Set(); } // UI went away
                catch (ObjectDisposedException) { done.Set(); }
            }
        }

        var parser = new ChatEventParser();
        parser.LineReceived += line => Send(new PipeMessage { Type = PipeMessage.TypeLine, Line = line });
        parser.TabsReceived += tabs => Send(new PipeMessage { Type = PipeMessage.TypeTabs, Tabs = [.. tabs] });

        using var capture = new RawSocketCapture(parser.BuildReceiver());
        int interfaces = capture.Start();
        if (interfaces == 0)
        {
            Send(new PipeMessage { Type = PipeMessage.TypeStatus, Ok = false, Status = "Could not open any network interface for capture." });
            return 5;
        }
        Send(new PipeMessage { Type = PipeMessage.TypeStatus, Status = $"Capturing on {interfaces} interface(s)" });

        // Stay alive until the UI closes the pipe or exits.
        try
        {
            parent.EnableRaisingEvents = true;
            parent.Exited += (_, _) => done.Set();
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }

        while (!done.Wait(TimeSpan.FromSeconds(2)))
        {
            if (!pipe.IsConnected || parent.HasExited) break;
            // A write is the only reliable way to notice the UI closed its end.
            Send(new PipeMessage { Type = PipeMessage.TypeHeartbeat });
        }
        return 0;
    }
}
