using AlbionChatTranslator.Core.Ipc;
using Velopack;

namespace AlbionChatTranslator;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks, then returns.
        VelopackApp.Build().Run();

        // Elevated capture helper mode: no window, no WPF.
        if (args.Length == 3 && args[0] == CaptureHost.Argument && int.TryParse(args[2], out int parentPid))
            return CaptureHost.Run(args[1], parentPid);

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
