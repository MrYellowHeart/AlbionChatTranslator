using StatisticsAnalysisTool.Network;
using System.IO;
using System.Text;

namespace AlbionChatTranslator.Network;

public record ChatMessage(
    DateTime Timestamp,
    IReadOnlyList<string> Tabs,  // all tab names this message should appear in
    string Channel,              // "SAY" | "WHISPER" | "Friendly" | "Global" — for text colour
    string Sender,
    string Text
);

public class ChatHandler : PacketHandler<EventPacket>
{
    public event Action<ChatMessage>? MessageReceived;

    // EC216 channel type IDs identified empirically:
    public const int SayChannelId      = 31;
    public const int WhisperChannelId  = 28;
    public const int PartyChannelId    = 26;
    public const int GuildChannelId    = 24;
    public const int AllianceChannelId = 25;

    /// <summary>
    /// Given an Int32 channel ID, returns all enabled tab names that include it.
    /// Return an empty list to silently drop the message.
    /// </summary>
    public Func<int, IReadOnlyList<string>>? ChannelResolver { get; set; }

    /// <summary>
    /// When true, byte-keyed (global) channels in EventCode 73 are dropped.
    /// When false, the byte value is looked up in GlobalChannelMap to obtain
    /// the EventCode-216 channel ID and routed via normal tab resolution.
    /// </summary>
    public bool BlockGlobalChannels { get; set; } = true;

    /// <summary>
    /// Maps EventCode-73 [0] Byte value → EventCode-216 channel ID (global channels).
    /// </summary>
    public Dictionary<int, int> GlobalChannelMap { get; set; } = new() { [2] = 8, [7] = 14 };

    /// <summary>
    /// Maps EventCode-73 [0] Int16 runtime ID → EventCode-216 channel type.
    /// Populated from EC205 at login. Session-only.
    /// </summary>
    public Dictionary<int, int> DynamicChannelMap { get; set; } = new();

    // Set to a file path to dump all raw EventCode-73 global messages for analysis.
    public string? DumpPath { get; set; }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var msg = packet.EventCode switch
        {
            73 => ParseChannelMessage(packet),
            74 => ParseSayMessage(packet),
            75 => ParseWhisperMessage(packet),
            _  => null
        };

        if (msg != null && msg.Tabs.Count > 0)
            MessageReceived?.Invoke(msg);

        return NextAsync(packet);
    }

    private static string FormatParam(object? v) => v switch
    {
        null         => "null",
        byte b       => $"Byte {b} (0x{b:X2})",
        int i        => $"Int32 {i}",
        long l       => $"Int64 {l}",
        short s      => $"Int16 {s}",
        bool bo      => $"Bool {bo}",
        string str   => $"String \"{str}\"",
        int[]  arr   => $"int[{arr.Length}]{{{string.Join(",", arr)}}}",
        object[] arr => $"obj[{arr.Length}]{{{string.Join(",", arr.Select(FormatParam))}}}",
        _            => $"{v.GetType().Name}:{v}"
    };

    private ChatMessage? ParseChannelMessage(EventPacket packet)
    {
        // [0] = channel discriminator: Byte = global, Int32 = private channel
        // [1] = sender, [2] = message
        if (!packet.Parameters.TryGetValue(0, out var chObj)) return null;

#if DEBUG
        if (DumpPath != null && chObj is byte)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== EC73 global @ {DateTime.Now:HH:mm:ss} ===");
                foreach (var kv in packet.Parameters.OrderBy(k => k.Key))
                    sb.AppendLine($"  [{kv.Key}] {FormatParam(kv.Value)}");
                sb.AppendLine();
                File.AppendAllText(DumpPath, sb.ToString());
            }
            catch { }
        }
#endif

        int channelId;
        bool isGlobal = chObj is byte;

        if (isGlobal)
        {
            if (BlockGlobalChannels) return null;
            int byteVal = (byte)chObj;
            if (!GlobalChannelMap.TryGetValue(byteVal, out channelId))
                return null; // unknown global byte value — drop until mapped
        }
        else
        {
            int rawId = chObj switch
            {
                int   i => i,
                long  l => (int)l,
                short s => (int)s,
                _       => -1
            };

            // Dynamic channels (guild/alliance/faction=Int16, party=Int32) are runtime-assigned.
            // EC205 populates DynamicChannelMap with their EC216 channel type — translate if known.
            channelId = DynamicChannelMap.TryGetValue(rawId, out int ec216Type) ? ec216Type : rawId;
        }

        // Sender may be null/missing for system-generated messages
        packet.Parameters.TryGetValue(1, out var senderObj);
        string sender = senderObj is string sStr && !string.IsNullOrEmpty(sStr) ? sStr : string.Empty;

        if (!packet.Parameters.TryGetValue(2, out var msgObj) || msgObj is not string text) return null;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var tabs = Resolve(channelId);
        bool isSystem = string.IsNullOrEmpty(sender);
        string channelType = isSystem ? "System" : isGlobal ? "Global" : channelId switch
        {
            PartyChannelId    => "Party",
            GuildChannelId    => "Guild",
            AllianceChannelId => "Alliance",
            _                 => "Friendly",
        };
        return new ChatMessage(DateTime.Now, tabs, channelType, sender, text);
    }

    private ChatMessage? ParseSayMessage(EventPacket packet)
    {
        // [0] = sender, [1] = message, [6] = true if emote
        if (packet.Parameters.TryGetValue(6, out var emoteObj) && emoteObj is true) return null;

        if (!packet.Parameters.TryGetValue(0, out var senderObj) || senderObj is not string sender) return null;
        if (!packet.Parameters.TryGetValue(1, out var msgObj)    || msgObj    is not string text)   return null;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Route via the SAY channel ID so it appears in any tab that includes channel 31
        return new ChatMessage(DateTime.Now, Resolve(SayChannelId), "SAY", sender, text);
    }

    private ChatMessage? ParseWhisperMessage(EventPacket packet)
    {
        // [0] = sender, [2] = message
        if (!packet.Parameters.TryGetValue(0, out var senderObj) || senderObj is not string sender) return null;
        if (!packet.Parameters.TryGetValue(2, out var msgObj)    || msgObj    is not string text)   return null;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Route via the WHISPER channel ID so it appears in any tab that includes channel 28
        return new ChatMessage(DateTime.Now, Resolve(WhisperChannelId), "WHISPER", sender, text);
    }

    private IReadOnlyList<string> Resolve(int channelId) =>
        ChannelResolver?.Invoke(channelId) ?? [];
}
