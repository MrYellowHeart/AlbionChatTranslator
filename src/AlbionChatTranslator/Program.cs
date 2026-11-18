using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using System.IO;
using Velopack;

namespace AlbionChatTranslator;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks, then returns.
        VelopackApp.Build()
            // Offline packs live outside the install folder so updates keep them; remove them
            // (and the optional translation cache) when the app itself is uninstalled.
            .OnBeforeUninstallFastCallback(_ =>
            {
                try { Directory.Delete(AppSettings.LocalDataDirectory, recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            })
            .Run();

        // Elevated capture helper mode: no window, no WPF.
        if (args.Length == 3 && args[0] == CaptureHost.Argument && int.TryParse(args[2], out int parentPid))
            return CaptureHost.Run(args[1], parentPid);

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
