using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Translation;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AlbionChatTranslator.Core.Settings;

public enum ProviderChoice { Google, DeepL }

public sealed class TabSetting
{
    public string Name { get; set; } = string.Empty;
    public List<int> ChannelIds { get; set; } = [];
    public bool Enabled { get; set; } = true;
}

/// <summary>User settings in %APPDATA%\AlbionChatTranslator\settings.json.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 2;

    /// <summary>Language chat is translated into (ISO 639-1).</summary>
    public string TargetLanguage { get; set; } = Languages.English;

    public bool IncludeGlobalChannels { get; set; }
    public int MinLetters { get; set; } = 4;
    public bool AlwaysOnTop { get; set; } = true;
    public int MaxLinesPerTab { get; set; } = 500;
    public List<TabSetting> Tabs { get; set; } = [];

    public ProviderChoice Provider { get; set; } = ProviderChoice.Google;

    /// <summary>DeepL key encrypted with Windows DPAPI for the current user. Never stored in plain text.</summary>
    public string? DeepLKeyProtected { get; set; }

    [JsonIgnore]
    public string? DeepLKey
    {
        get => SecretProtector.Unprotect(DeepLKeyProtected);
        set => DeepLKeyProtected = string.IsNullOrWhiteSpace(value) ? null : SecretProtector.Protect(value.Trim());
    }

    [JsonIgnore] public bool IsFirstRun { get; private set; }

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlbionChatTranslator");

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

            // The short-lived offline build stored Provider = "Offline"; that is Google now.
            if (node["Provider"]?.GetValue<string>() is "Offline")
                node["Provider"] = nameof(ProviderChoice.Google);

            return node.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
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
        Tabs = fromGame.Select(t => new TabSetting
        {
            Name = t.Name,
            ChannelIds = [.. t.ChannelIds],
            Enabled = !previous.TryGetValue(t.Name, out var old) || old.Enabled,
        }).ToList();
    }

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
        s.Provider = s.DeepLKeyProtected is null ? ProviderChoice.Google : ProviderChoice.DeepL;
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

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
