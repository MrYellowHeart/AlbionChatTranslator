namespace AlbionChatTranslator.Core.Settings;

/// <summary>
/// Optional troubleshooting log (Settings → "Write a diagnostic log"). It records what the app
/// did: channel numbers, tab routing, which translator answered, timings and errors. It never
/// records message text or player names. Kept to ~2 MB (one previous file is kept as .old).
/// </summary>
public static class DiagnosticLog
{
    private const long MaxBytes = 2_000_000;
    private static readonly Lock Gate = new();

    public static bool Enabled { get; set; }

    public static string FilePath => Path.Combine(AppSettings.DefaultDirectory, "diagnostics.log");

    public static void Write(string message)
    {
        if (!Enabled) return;
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.DefaultDirectory);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
