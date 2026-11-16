using StatisticsAnalysisTool.Abstractions;
using StatisticsAnalysisTool.Network;

namespace AlbionChatTranslator.Core.Chat;

/// <summary>
/// Turns Albion chat events into <see cref="ChatLine"/>s and <see cref="ChatTab"/> lists.
/// Event codes are looked up by name from the vendored upstream <see cref="EventCodes"/>,
/// so an Albion renumbering only needs a vendored-library refresh.
/// </summary>
public sealed class ChatEventParser
{
    private const string TabNamePrefix = "@CHAT_SETTINGS_TAB_NAME_";
    private const int MaxTextLength = 1024;

    // ChatMessage [0] Byte value -> chat-settings channel ID (global channels). The game's tab
    // layout lists global channels by that same number (e.g. Recruit = 2), so the byte value is
    // used as-is unless NewChatChannels announces a different mapping.
    private readonly Dictionary<int, int> _globalChannels = new();
    private readonly HashSet<int> _reportedUnknown = [];

    // ChatMessage [0] runtime ID (guild/alliance/party/...) -> chat-settings channel ID.
    // Runtime IDs are reassigned every session, so this is never persisted.
    private readonly Dictionary<int, int> _dynamicChannels = new();
    private readonly Lock _lock = new();
    private readonly TimeProvider _clock;

    public ChatEventParser(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public event Action<ChatLine>? LineReceived;
    public event Action<IReadOnlyList<ChatTab>>? TabsReceived;

    /// <summary>Technical notes for the optional diagnostic log: channel numbers only, never text or names.</summary>
    public event Action<string>? Diagnostic;

    public static readonly IReadOnlyList<EventCodes> HandledEvents =
    [
        EventCodes.ChatMessage,
        EventCodes.ChatSay,
        EventCodes.ChatWhisper,
        EventCodes.NewChatChannels,
        EventCodes.UpdateChatSettings,
    ];

    /// <summary>Builds a Photon receiver that feeds this parser.</summary>
    public IPhotonReceiver BuildReceiver()
    {
        var builder = ReceiverBuilder.Create();
        foreach (var code in HandledEvents)
            builder.AddHandler(new DelegateEventHandler((int)code, Handle));
        return builder.Build();
    }

    /// <summary>Processes one event. Public so tests can feed synthetic packets.</summary>
    public void Handle(EventPacket packet)
    {
        switch ((EventCodes)packet.EventCode)
        {
            case EventCodes.ChatMessage: Emit(ParseChannelMessage(packet.Parameters)); break;
            case EventCodes.ChatSay: Emit(ParseSay(packet.Parameters)); break;
            case EventCodes.ChatWhisper: Emit(ParseWhisper(packet.Parameters)); break;
            case EventCodes.NewChatChannels: ParseChannelAnnouncement(packet.Parameters); break;
            case EventCodes.UpdateChatSettings:
                var tabs = ParseTabs(packet.Parameters);
                if (tabs.Count > 0)
                    TabsReceived?.Invoke(tabs);
                break;
        }
    }

    private void Emit(ChatLine? line)
    {
        if (line is not null)
            LineReceived?.Invoke(line);
    }

    // ChatMessage: [0] channel (Byte = global, other integer = runtime ID), [1] sender, [2] text
    private ChatLine? ParseChannelMessage(Dictionary<byte, object> p)
    {
        if (!p.TryGetValue(0, out var channelObj)) return null;
        if (!ParamReader.TryGetString(p, 2, out var text) || !IsUsableText(text)) return null;
        ParamReader.TryGetString(p, 1, out var sender);

        // System broadcasts have no sender; they are not player chat.
        if (string.IsNullOrWhiteSpace(sender)) return null;

        int channelId;
        ChannelKind kind;
        lock (_lock)
        {
            if (channelObj is byte globalKey)
            {
                channelId = _globalChannels.TryGetValue(globalKey, out int mappedGlobal) ? mappedGlobal : globalKey;
                kind = ChannelKind.Global;
            }
            else if (ParamReader.TryToInt(channelObj, out int runtimeId))
            {
                if (_dynamicChannels.TryGetValue(runtimeId, out int mapped))
                {
                    channelId = mapped;
                }
                else
                {
                    channelId = runtimeId;
                    if (_reportedUnknown.Add(runtimeId))
                        Diagnostic?.Invoke($"chat on unannounced runtime channel {runtimeId} (log in or change character while capturing so the game announces it)");
                }
                kind = KnownChannels.KindOf(channelId);
            }
            else
            {
                return null;
            }
        }

        return new ChatLine(_clock.GetLocalNow(), channelId, kind, sender, Truncate(text));
    }

    // ChatSay: [0] sender, [1] text, [6] true for emotes
    private ChatLine? ParseSay(Dictionary<byte, object> p)
    {
        if (p.TryGetValue(6, out var emote) && emote is true) return null;
        if (!ParamReader.TryGetString(p, 0, out var sender)) return null;
        if (!ParamReader.TryGetString(p, 1, out var text) || !IsUsableText(text)) return null;
        return new ChatLine(_clock.GetLocalNow(), KnownChannels.Say, ChannelKind.Say, sender, Truncate(text));
    }

    // ChatWhisper: [0] sender, [2] text
    private ChatLine? ParseWhisper(Dictionary<byte, object> p)
    {
        if (!ParamReader.TryGetString(p, 0, out var sender)) return null;
        if (!ParamReader.TryGetString(p, 2, out var text) || !IsUsableText(text)) return null;
        return new ChatLine(_clock.GetLocalNow(), KnownChannels.Whisper, ChannelKind.Whisper, sender, Truncate(text));
    }

    // NewChatChannels: [0] chat-settings channel ID, [1] key (Byte = global, other integer = runtime ID)
    private void ParseChannelAnnouncement(Dictionary<byte, object> p)
    {
        if (!ParamReader.TryGetInt(p, 0, out int channelId) || channelId < 0) return;
        if (!p.TryGetValue(1, out var key)) return;

        lock (_lock)
        {
            if (key is byte globalKey)
            {
                _globalChannels[globalKey] = channelId;
                Diagnostic?.Invoke($"global channel {globalKey} -> tab channel {channelId}");
            }
            else if (ParamReader.TryToInt(key, out int runtimeId))
            {
                _dynamicChannels[runtimeId] = channelId;
                Diagnostic?.Invoke($"runtime channel {runtimeId} -> tab channel {channelId}");
            }
        }
    }

    // UpdateChatSettings: [0] tab names, [1] per-tab arrays of channel IDs
    private static List<ChatTab> ParseTabs(Dictionary<byte, object> p)
    {
        var result = new List<ChatTab>();
        if (!p.TryGetValue(0, out var namesObj) || !p.TryGetValue(1, out var channelsObj)) return result;

        var names = ParamReader.AsSequence(namesObj).ToList();
        var channelLists = ParamReader.AsSequence(channelsObj).ToList();

        for (int i = 0; i < Math.Min(names.Count, channelLists.Count); i++)
        {
            if (names[i] is not string rawName || string.IsNullOrWhiteSpace(rawName)) continue;
            var ids = ParamReader.ToIntList(channelLists[i]);
            if (ids.Count == 0) continue;
            string? key = rawName.StartsWith(TabNamePrefix, StringComparison.OrdinalIgnoreCase) ? rawName[TabNamePrefix.Length..] : null;
            result.Add(new ChatTab(FriendlyTabName(rawName), ids, key));
        }
        return result;
    }

    internal static string FriendlyTabName(string raw)
    {
        if (!raw.StartsWith(TabNamePrefix, StringComparison.OrdinalIgnoreCase)) return raw;
        var s = raw[TabNamePrefix.Length..].Replace('_', ' ').ToLowerInvariant();
        return s.Length == 0 ? raw : char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static bool IsUsableText(string text) => !string.IsNullOrWhiteSpace(text);

    private static string Truncate(string text) => text.Length <= MaxTextLength ? text : text[..MaxTextLength];

    private sealed class DelegateEventHandler(int code, Action<EventPacket> action) : PacketHandler<EventPacket>(code)
    {
        protected override Task OnHandleAsync(EventPacket packet)
        {
            try
            {
                action(packet);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatEventParser] event {Code} failed: {e.Message}");
            }
            return Task.CompletedTask;
        }
    }
}
