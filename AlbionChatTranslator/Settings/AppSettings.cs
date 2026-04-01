using System.IO;
using System.Text.Json;

namespace AlbionChatTranslator.Settings;

public class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "EN-US";
    public bool BlockGlobalChannels { get; set; } = true;
    public List<ChatTabSettings> Tabs { get; set; } = new();

    /// <summary>
    /// Minimum message length (characters) before DeepL translation is attempted.
    /// Short messages like "hi", "ok", "test" are too ambiguous for reliable language detection.
    /// Default: 8
    /// </summary>
    public int MinTranslationLength { get; set; } = 8;

    /// <summary>
    /// Maps EventCode-73 [0] Byte value → EventCode-216 channel ID (global channels).
    /// Populated at runtime by EventCode 205 when [1] is Byte.
    /// </summary>
    public Dictionary<int, int> GlobalChannelMap { get; set; } = new() { [2] = 8, [7] = 14 };

    /// <summary>
    /// Maps EventCode-73 [0] Int16 runtime channel ID → EventCode-216 channel type ID.
    /// Populated at runtime by EventCode 205 when [1] is Int16 (guild/alliance/faction/party).
    /// e.g. 2028 → 25 (alliance), 1866 → 29 (faction)
    /// Persisted across sessions — overwritten each login when EC205 re-announces them.
    /// </summary>
    public Dictionary<int, int> DynamicChannelMap { get; set; } = new();

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AlbionChatTranslator");

    public static string FilePath => Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
