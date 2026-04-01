using AlbionChatTranslator.Settings;
using StatisticsAnalysisTool.Network;
using System.IO;
using System.Text;

namespace AlbionChatTranslator.Network;

public class ChatSettingsHandler : PacketHandler<EventPacket>
{
    public event Action<List<ChatTabSettings>>? TabsReceived;

    /// <summary>
    /// Fired when EventCode 205 [1]=Byte: global byte value → EC216 channel ID.
    /// Args: (byteValue, channelId)
    /// </summary>
    public event Action<int, int>? GlobalChannelMapped;

    /// <summary>
    /// Fired when EventCode 205 [1]=Int16: runtime dynamic ID → EC216 channel type.
    /// Args: (dynamicChannelId, ec216ChannelType)
    /// e.g. (2028, 25) means alliance runtime channel 2028 = EC216 type 25
    /// </summary>
    public event Action<int, int>? DynamicChannelMapped;

    // Set to a file path to dump raw events for analysis.
    public string? DumpPath { get; set; }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        if (DumpPath != null)
            DumpPacket(packet);

        switch (packet.EventCode)
        {
            case 205:
                ParseGlobalChannelMapping(packet);
                break;
            case 216:
                var tabs = ParseTabs(packet);
                if (tabs != null)
                    TabsReceived?.Invoke(tabs);
                break;
        }
        return NextAsync(packet);
    }

    /// <summary>
    /// EventCode 205: server announces a channel subscription.
    /// [0] = Int32 EC216 channel type, [1] = Byte (global) or Int16 (dynamic runtime ID)
    /// </summary>
    private void ParseGlobalChannelMapping(EventPacket packet)
    {
        if (!packet.Parameters.TryGetValue(0, out var chIdObj)) return;
        if (!packet.Parameters.TryGetValue(1, out var keyObj))  return;

        int ec216Type = chIdObj switch
        {
            int  i => i,
            long l => (int)l,
            _      => -1
        };
        if (ec216Type < 0) return;

        switch (keyObj)
        {
            case byte byteVal:
                // Global channel: byte key → EC216 channel ID
                GlobalChannelMapped?.Invoke(byteVal, ec216Type);
                break;
            case short dynId:
                // Dynamic channel (guild/alliance/faction): runtime Int16 ID → EC216 channel type
                DynamicChannelMapped?.Invoke((int)dynId, ec216Type);
                break;
            case int intDynId:
                // Dynamic channel (party): runtime Int32 ID → EC216 channel type
                DynamicChannelMapped?.Invoke(intDynId, ec216Type);
                break;
        }
    }

    private void DumpPacket(EventPacket packet)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== EventCode {packet.EventCode} @ {DateTime.Now:HH:mm:ss} ===");
            sb.AppendLine($"  Parameter count: {packet.Parameters.Count}");

            foreach (var kv in packet.Parameters.OrderBy(k => k.Key))
            {
                sb.Append($"  [{kv.Key}] ({kv.Value?.GetType().Name ?? "null"}) = ");
                sb.AppendLine(FormatValue(kv.Value));
            }

            sb.AppendLine();
            File.AppendAllText(DumpPath!, sb.ToString());
        }
        catch { }
    }

    private static string FormatValue(object? value) => value switch
    {
        null           => "null",
        string s       => $"\"{s}\"",
        byte b         => $"0x{b:X2} ({b})",
        int i          => i.ToString(),
        long l         => $"{l}L",
        short sh       => $"{sh}s",
        bool bo        => bo.ToString(),
        int[]   arr    => $"int[{arr.Length}] {{ {string.Join(", ", arr)} }}",
        byte[]  arr    => $"byte[{arr.Length}] {{ {string.Join(", ", arr.Select(b => $"0x{b:X2}"))} }}",
        object[] arr   => $"object[{arr.Length}] {{ {string.Join(", ", arr.Select(o => FormatValue(o)))} }}",
        _              => value.ToString() ?? "?"
    };

    private static List<ChatTabSettings>? ParseTabs(EventPacket packet)
    {
        if (!packet.Parameters.TryGetValue(0, out var namesObj)    || namesObj    is not object[] names)         return null;
        if (!packet.Parameters.TryGetValue(1, out var channelsObj) || channelsObj is not object[] channelArrays) return null;

        var result = new List<ChatTabSettings>();

        for (int i = 0; i < Math.Min(names.Length, channelArrays.Length); i++)
        {
            if (names[i] is not string rawName) continue;

            // Inner arrays come out as int[] from Protocol16
            int[]? ids = channelArrays[i] switch
            {
                int[]    arr => arr,
                object[] arr => arr.OfType<int>().ToArray(),
                _            => null
            };
            if (ids == null) continue;

            string displayName = rawName.StartsWith("@CHAT_SETTINGS_TAB_NAME_", StringComparison.OrdinalIgnoreCase)
                ? FriendlyName(rawName["@CHAT_SETTINGS_TAB_NAME_".Length..])
                : rawName;

            result.Add(new ChatTabSettings { Name = displayName, ChannelIds = ids.ToList() });
        }

        return result.Count > 0 ? result : null;
    }

    private static string FriendlyName(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
