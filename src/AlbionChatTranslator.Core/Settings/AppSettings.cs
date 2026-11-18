using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Translation;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AlbionChatTranslator.Core.Settings;

/// <summary>The Albion server the user plays on.</summary>
public enum GameServer { Unknown, Americas, Europe, Asia }

/// <summary>An optional official translation service the user has a key for.</summary>
public enum KeyService { None, Microsoft, DeepL }

public enum TranslateMode { Automatic, OnClick }

public sealed class TabSetting
{
    public string Name { get; set; } = string.Empty;
    public List<int> ChannelIds { get; set; } = [];
    public bool Enabled { get; set; } = true;
    /// <summary>The game's built-in tab key (e.g. "COMBAT"), if any.</summary>
    public string? GameKey { get; set; }
}

/// <summary>User settings in %APPDATA%\AlbionChatTranslator\settings.json.</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 3;
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Language chat is translated into (ISO 639-1).</summary>
    public string TargetLanguage { get; set; } = Languages.English;

    /// <summary>Translate global channels (Help, LFG, Recruit, trade…) automatically. They are always shown.</summary>
    public bool IncludeGlobalChannels { get; set; }

    /// <summary>Write the troubleshooting log (no message text or names).</summary>
    public bool DiagnosticLog { get; set; }
    public int MinLetters { get; set; } = 4;
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>Stay on top (and show the overlay) only while Albion or this app is the active window.</summary>
    public bool OnTopOnlyWithGame { get; set; } = true;
    public int MaxLinesPerTab { get; set; } = 500;
    public List<TabSetting> Tabs { get; set; } = [];

    /// <summary>
    /// Chat room number -> chat-settings channel, as the game last announced it. Kept so chat is
    /// sorted correctly even when capture starts after logging in; the game's next announcement wins.
    /// </summary>
    public Dictionary<int, int> Rooms { get; set; } = new();

    // ── Privacy ──────────────────────────────────────────────────────────────

    /// <summary>Other languages the user reads (ISO 639-1): shown as-is, never sent.</summary>
    public List<string> ReadLanguages { get; set; } = [];

    /// <summary>The server the user plays on (asked on first launch).</summary>
    public GameServer Server { get; set; } = GameServer.Unknown;

    /// <summary>Languages the user sees in chat: their offline packs are downloaded ahead of time.</summary>
    public List<string> ChatLanguages { get; set; } = [];

    /// <summary>Channel kinds whose messages are never sent for translation (e.g. whispers).</summary>
    public List<ChannelKind> NeverSendChannels { get; set; } = [];

    /// <summary>Automatic, or only when the user clicks a message.</summary>
    public TranslateMode Mode { get; set; } = TranslateMode.Automatic;

    /// <summary>Words never translated (guild names, friends' names, local slang).</summary>
    public List<string> CustomTerms { get; set; } = [];

    /// <summary>Keep translations between sessions (stores other players' messages on disk). Off by default.</summary>
    public bool KeepCacheOnDisk { get; set; }

    // ── Translators, tried in this order ─────────────────────────────────────

    /// <summary>1. The user's own official service (optional).</summary>
    public KeyService KeyService { get; set; } = KeyService.None;

    /// <summary>2. Google's free endpoint (no setup).</summary>
    public bool UseGoogle { get; set; } = true;

    /// <summary>3. Offline models when the online services cannot answer (never stops).</summary>
    public bool OfflineFallback { get; set; } = true;

    /// <summary>Microsoft Translator key, DPAPI-encrypted.</summary>
    public string? MicrosoftKeyProtected { get; set; }

    /// <summary>Azure region of the Translator resource (e.g. "westeurope"); empty for global resources.</summary>
    public string? MicrosoftRegion { get; set; }

    [JsonIgnore]
    public string? MicrosoftKey
    {
        get => SecretProtector.Unprotect(MicrosoftKeyProtected);
        set => MicrosoftKeyProtected = string.IsNullOrWhiteSpace(value) ? null : SecretProtector.Protect(value.Trim());
    }

    // ── Overlay ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Off by default: Albion Online support has said third-party tools may not have an overlay on
    /// the game client, and would not say whether a separate window like this one counts.
    /// </summary>
    public bool OverlayEnabled { get; set; }

    public const string OverlayWarning =
        "Albion Online support has said third-party tools may not have an overlay on the game client. " +
        "This overlay is a separate window that does not change the game, but Sandbox Interactive would not say " +
        "whether it counts. If you turn it on, you do so at your own risk.";

    public double OverlayOpacity { get; set; } = 0.75;
    public bool OverlayTranslatedOnly { get; set; } = true;
    public bool OverlayClickThrough { get; set; } = true;
    /// <summary>The game tab shown in the overlay.</summary>
    public string? OverlayTab { get; set; }
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public double OverlayWidth { get; set; } = 520;
    public double OverlayHeight { get; set; } = 220;


    /// <summary>DeepL key encrypted with Windows DPAPI for the current user. Never stored in plain text.</summary>
    public string? DeepLKeyProtected { get; set; }

    [JsonIgnore]
    public string? DeepLKey
    {
        get => SecretProtector.Unprotect(DeepLKeyProtected);
        set => DeepLKeyProtected = string.IsNullOrWhiteSpace(value) ? null : SecretProtector.Protect(value.Trim());
    }

    [JsonIgnore] public bool IsFirstRun { get; private set; }

    /// <summary>
    /// Set ACT_DATA_DIR to keep settings and cache in one folder instead of the user profile
    /// (portable use, testing). Windows ignores %APPDATA% overrides, so this is the way to do it.
    /// </summary>
    private static string? DataDirOverride => Environment.GetEnvironmentVariable("ACT_DATA_DIR") is { Length: > 0 } d ? d : null;

    public static string DefaultDirectory => DataDirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlbionChatTranslator");

    /// <summary>
    /// Machine-local data: offline packs and the optional translation cache. Deliberately NOT
    /// %LOCALAPPDATA%\AlbionChatTranslator, which is the installer's own folder: running Setup
    /// replaced it and the packs (hundreds of MB) were downloaded again after every install.
    /// </summary>
    public static string LocalDataDirectory => DataDirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlbionChatTranslatorData");

    /// <summary>Where the optional translation cache lives (machine-local, not roaming).</summary>
    public static string CachePath => Path.Combine(LocalDataDirectory, "translation-cache.json");

    public static AppSettings Load(string? directory = null)
    {
        var path = Path.Combine(directory ?? DefaultDirectory, "settings.json");
        if (!File.Exists(path))
            return new AppSettings { IsFirstRun = true, TargetLanguage = GuessLanguage() };

        try
        {
            var json = File.ReadAllText(path);
            var node = JsonNode.Parse(json)?.AsObject();
            if (node is null) return new AppSettings();

            if (node["Version"] is null)
                return MigrateFromV1(node);

            // Earlier 2.0 builds had "Provider" (Google/DeepL/Offline) and "DeepLAsBackup".
            // Anyone who entered a DeepL key keeps using it, now as the first translator.
            if (node["KeyService"] is null && node["DeepLKeyProtected"]?.GetValue<string>() is { Length: > 0 })
                node["KeyService"] = nameof(KeyService.DeepL);

            var settings = node.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            // The game's Combat tab holds combat and system messages, never player chat: drop it.
            settings.Tabs.RemoveAll(IsCombat);
            settings.Version = CurrentVersion;
            return settings;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, SettingsJsonContext.Default.AppSettings));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Applies the tab list the game just sent, keeping the user's on/off choices.</summary>
    public void MergeTabs(IEnumerable<ChatTab> fromGame)
    {
        var previous = Tabs.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        Tabs = fromGame.Where(t => !t.IsCombatTab).Select(t => new TabSetting
        {
            Name = t.Name,
            ChannelIds = [.. t.ChannelIds],
            GameKey = t.GameKey,
            Enabled = !previous.TryGetValue(t.Name, out var old) || old.Enabled, // keep the user's choice
        }).ToList();
    }

    private static bool IsCombat(TabSetting t) =>
        string.Equals(t.GameKey, "COMBAT", StringComparison.OrdinalIgnoreCase)
        || t.GameKey is null && string.Equals(t.Name, "Combat", StringComparison.OrdinalIgnoreCase);

    // v1 (the original DeepL-only app) stored the DeepL key in plain text. Encrypt it and drop the rest.
    private static AppSettings MigrateFromV1(JsonObject v1)
    {
        var s = new AppSettings();
        if (v1["ApiKey"]?.GetValue<string>() is { Length: > 0 } key)
            s.DeepLKey = key;
        if (v1["TargetLanguage"]?.GetValue<string>() is { } lang && Languages.Find(lang) is { } info)
            s.TargetLanguage = info.Code;
        if (v1["BlockGlobalChannels"]?.GetValue<bool>() is { } block)
            s.IncludeGlobalChannels = !block;
        if (v1["Tabs"] is JsonArray tabs)
        {
            foreach (var t in tabs.OfType<JsonObject>())
            {
                s.Tabs.Add(new TabSetting
                {
                    Name = t["Name"]?.GetValue<string>() ?? "",
                    ChannelIds = t["ChannelIds"]?.AsArray().Select(n => n!.GetValue<int>()).ToList() ?? [],
                    Enabled = t["Enabled"]?.GetValue<bool>() ?? true,
                });
            }
        }
        // v1 users already chose DeepL and have a key: keep them on it.
        s.KeyService = s.DeepLKeyProtected is null ? KeyService.None : KeyService.DeepL;
        return s;
    }

    private static string GuessLanguage()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Find(code)?.Code ?? Languages.English;
    }
}

/// <summary>Windows DPAPI wrapper: only the same Windows user on the same PC can decrypt.</summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = "AlbionChatTranslator.v2"u8.ToArray();

    public static string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
