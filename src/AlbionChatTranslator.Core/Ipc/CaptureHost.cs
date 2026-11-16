using AlbionChatTranslator.Core.Capture;
using AlbionChatTranslator.Core.Chat;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace AlbionChatTranslator.Core.Ipc;

/// <summary>Exit codes of the capture helper, so the UI can explain what went wrong.</summary>
public static class CaptureExitCode
{
    public const int Ok = 0;
    public const int BadArguments = 2;
    public const int ParentGone = 3;
    public const int CouldNotConnect = 4;
    public const int NoInterfaces = 5;
    public const int PipeNotOurs = 6;
    public const int Crashed = 7;

    public static string Describe(int code) => code switch
    {
        BadArguments => "the capture helper was started with invalid arguments",
        ParentGone => "the capture helper could not find this window",
        CouldNotConnect => "the capture helper could not connect to this window",
        NoInterfaces => "no network adapter could be opened for capture",
        PipeNotOurs => "the capture helper refused a connection that did not come from this window",
        Crashed => "the capture helper hit an unexpected error",
        _ => $"the capture helper stopped (code {code})",
    };
}

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
        if (!PipeProtocol.IsValidPipeName(pipeName)) return CaptureExitCode.BadArguments;

        Process parent;
        try { parent = Process.GetProcessById(parentPid); }
        catch (ArgumentException) { return CaptureExitCode.ParentGone; }

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.None);
        try { pipe.Connect(TimeSpan.FromSeconds(15)); }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException) { return CaptureExitCode.CouldNotConnect; }

        // Only talk to the exact window that launched us: the process on the other end of the
        // (randomly named) pipe must be the parent we were given. (.NET's PipeOptions.CurrentUserOnly
        // can't be used here: it compares against the token's default *owner*, which for an elevated
        // process is the Administrators group rather than the user, so it always refuses.)
        if (ServerProcessId(pipe) != parentPid) return CaptureExitCode.PipeNotOurs;

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

        try
        {
            var parser = new ChatEventParser();
            parser.LineReceived += line => Send(new PipeMessage { Type = PipeMessage.TypeLine, Line = line });
            parser.TabsReceived += tabs => Send(new PipeMessage { Type = PipeMessage.TypeTabs, Tabs = [.. tabs] });

            using var capture = new RawSocketCapture(parser.BuildReceiver());
            int interfaces = capture.Start();
            if (interfaces == 0)
            {
                Send(new PipeMessage { Type = PipeMessage.TypeStatus, Ok = false, Status = "Could not open any network adapter for capture." });
                return CaptureExitCode.NoInterfaces;
            }
            Send(new PipeMessage { Type = PipeMessage.TypeStatus, Status = $"Capturing on {interfaces} network adapter(s)" });

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
            return CaptureExitCode.Ok;
        }
        catch (Exception e)
        {
            Send(new PipeMessage { Type = PipeMessage.TypeStatus, Ok = false, Status = $"Capture error: {e.GetType().Name}: {e.Message}" });
            return CaptureExitCode.Crashed;
        }
    }

    /// <summary>Process ID of whoever created the pipe we are connected to, or -1.</summary>
    internal static int ServerProcessId(NamedPipeClientStream pipe) =>
        GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint pid) ? (int)pid : -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint serverProcessId);
}
