using AlbionChatTranslator.Core.Chat;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlbionChatTranslator.Core.Ipc;

/// <summary>
/// One line of JSON sent from the elevated capture helper to the UI. Traffic is one-way:
/// the helper never reads anything from the pipe, so the UI cannot be used to steer it.
/// </summary>
public sealed record PipeMessage
{
    public const string TypeLine = "line";
    public const string TypeTabs = "tabs";
    public const string TypeStatus = "status";
    public const string TypeHeartbeat = "hb";
    /// <summary>Technical note for the optional diagnostic log (channel numbers, never message text).</summary>
    public const string TypeDiagnostic = "diag";

    public required string Type { get; init; }
    public ChatLine? Line { get; init; }
    public List<ChatTab>? Tabs { get; init; }
    public string? Status { get; init; }
    public bool Ok { get; init; } = true;
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(PipeMessage))]
internal sealed partial class PipeJsonContext : JsonSerializerContext;

public static class PipeProtocol
{
    /// <summary>Largest line the UI will accept; anything bigger is dropped as malformed.</summary>
    public const int MaxLineLength = 64 * 1024;

    public static string Serialize(PipeMessage message) =>
        JsonSerializer.Serialize(message, PipeJsonContext.Default.PipeMessage);

    public static PipeMessage? TryDeserialize(string line)
    {
        if (line.Length is 0 or > MaxLineLength) return null;
        try
        {
            return JsonSerializer.Deserialize(line, PipeJsonContext.Default.PipeMessage);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A fresh, unguessable pipe name for one capture session.</summary>
    public static string NewPipeName() => "AlbionChatTranslator-" + Guid.NewGuid().ToString("N");

    public static bool IsValidPipeName(string name) =>
        name.StartsWith("AlbionChatTranslator-", StringComparison.Ordinal) &&
        name.Length == "AlbionChatTranslator-".Length + 32 &&
        name["AlbionChatTranslator-".Length..].All(Uri.IsHexDigit);
}
