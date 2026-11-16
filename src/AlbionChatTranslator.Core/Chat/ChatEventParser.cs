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
    private readonly HashSet<string> _shapesLogged = [];

    // ChatMessage [0] runtime ID (guild/alliance/party/...) -> chat-settings channel ID.
    // Runtime IDs are reassigned every session, so this is never persisted.
    private readonly Dictionary<int, int> _dynamicChannels = new();
    private readonly Lock _lock = new();
    private readonly TimeProvider _clock;

    public ChatEventParser(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public event Action<ChatLine>? LineReceived;
    public event Action<IReadOnlyList<ChatTab>>? TabsReceived;

    /// <summary>The game said which chat-settings channel a chat room belongs to: (room, channel).</summary>
    public event Action<int, int>? RoomJoined;

    /// <summary>The game closed a chat room (for example the zone chat when you leave the zone).</summary>
    public event Action<int>? RoomRemoved;

    /// <summary>Technical notes for the optional diagnostic log: channel numbers only, never text or names.</summary>
    public event Action<string>? Diagnostic;

    public static readonly IReadOnlyList<EventCodes> HandledEvents =
    [
        EventCodes.ChatMessage,
        EventCodes.ChatSay,
        EventCodes.ChatWhisper,
        EventCodes.NewChatChannels,
        EventCodes.JoinedChatChannel,
        EventCodes.LeftChatChannel,
        EventCodes.RemovedChatChannel,
        EventCodes.UpdateChatSettings,
    ];

    /// <summary>Builds a Photon receiver that feeds this parser.</summary>
    public IPhotonReceiver BuildReceiver()
    {
        var builder = ReceiverBuilder.Create();
        foreach (var code in HandledEvents)
            builder.AddHandler(new DelegateEventHandler((int)code, Handle));
        // Discovery for the diagnostic log: the layout of any other event that carries text,
        // once per event type (text itself is never logged, only its length).
        foreach (var code in Enum.GetValues<EventCodes>().Distinct().Except(HandledEvents))
            builder.AddHandler(new DelegateEventHandler((int)code, LogOtherEvent));
        return builder.Build();
    }

    /// <summary>Processes one event. Public so tests can feed synthetic packets.</summary>
    public void Handle(EventPacket packet)
    {
        switch ((EventCodes)packet.EventCode)
        {
            case EventCodes.ChatMessage: Emit(ParseChannelMessage(packet.Parameters)); break;
            case EventCodes.ChatSay:
                LogShapeOnce("ChatSay", "first ChatSay", packet.Parameters);
                Emit(ParseSay(packet.Parameters));
                break;
            case EventCodes.ChatWhisper:
                LogShapeOnce("ChatWhisper", "first ChatWhisper", packet.Parameters);
                var whisper = ParseWhisper(packet.Parameters);
                if (whisper is null) LogShapeOnce("ChatWhisper:unread", "ChatWhisper not understood", packet.Parameters);
                Emit(whisper);
                break;
            case EventCodes.NewChatChannels:
                Diagnostic?.Invoke($"event NewChatChannels: {EventShape.Describe(packet.Parameters)}");
                ParseChannelAnnouncement(packet.Parameters);
                break;
            case EventCodes.JoinedChatChannel:
                Diagnostic?.Invoke($"event JoinedChatChannel: {EventShape.Describe(packet.Parameters)}");
                // [0] chat-settings channel, [1] room number used by ChatMessage (seen in game, Nov 2026).
                if (ParamReader.TryGetInt(packet.Parameters, 0, out int channel) && ParamReader.TryGetInt(packet.Parameters, 1, out int room))
                    MapRoom(room, channel);
                else
                    Diagnostic?.Invoke("JoinedChatChannel did not match the expected layout ([0] channel, [1] room); not used");
                break;
            case EventCodes.LeftChatChannel:
                Diagnostic?.Invoke($"event LeftChatChannel: {EventShape.Describe(packet.Parameters)}");
                break;
            case EventCodes.RemovedChatChannel:
                Diagnostic?.Invoke($"event RemovedChatChannel: {EventShape.Describe(packet.Parameters)}");
                // [0] room number (seen in game, Nov 2026). The number can be reused later.
                if (ParamReader.TryGetInt(packet.Parameters, 0, out int removed))
                {
                    bool had;
                    lock (_lock) had = _dynamicChannels.Remove(removed);
                    if (had) RoomRemoved?.Invoke(removed);
                }
                break;
            case EventCodes.UpdateChatSettings:
                LogShapeOnce("UpdateChatSettings", "event UpdateChatSettings", packet.Parameters);
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
        // Layout of the first message on each channel ([1] sender and [2] text are masked).
        LogShapeOnce($"msg:{channelObj?.GetType().Name}:{channelObj}", $"first ChatMessage on channel {channelObj} ({channelObj?.GetType().Name})", p, 1, 2);
        if (!ParamReader.TryGetString(p, 2, out var text) || !IsUsableText(text)) return null;
        ParamReader.TryGetString(p, 1, out var sender);

        // System broadcasts have no sender; they are not player chat.
        if (string.IsNullOrWhiteSpace(sender)) return null;

        int channelId;
        int? room = null;
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
                    room = runtimeId;
                    if (_reportedUnknown.Add(runtimeId))
                        Diagnostic?.Invoke($"chat in room {runtimeId}, which the game hasn't assigned to a channel this session");
                }
                kind = room is null ? KnownChannels.KindOf(channelId) : ChannelKind.Other;
            }
            else
            {
                return null;
            }
        }

        return new ChatLine(_clock.GetLocalNow(), channelId, kind, sender, Truncate(text), room);
    }

    // ChatSay: [0] sender, [1] text, [6] true for emotes
    private ChatLine? ParseSay(Dictionary<byte, object> p)
    {
        if (p.TryGetValue(6, out var emote) && emote is true) return null;
        if (!ParamReader.TryGetString(p, 0, out var sender)) return null;
        if (!ParamReader.TryGetString(p, 1, out var text) || !IsUsableText(text)) return null;
        return new ChatLine(_clock.GetLocalNow(), KnownChannels.Say, ChannelKind.Say, sender, Truncate(text));
    }

    // ChatWhisper: [0] the other player, [3] text (seen in game, Nov 2026: [0]=text [3]=text [4]=byte).
    // Older clients sent the text in [2].
    private ChatLine? ParseWhisper(Dictionary<byte, object> p)
    {
        if (!ParamReader.TryGetString(p, 0, out var sender)) return null;
        if (!(ParamReader.TryGetString(p, 3, out var text) && IsUsableText(text))
            && !(ParamReader.TryGetString(p, 2, out text) && IsUsableText(text))) return null;
        return new ChatLine(_clock.GetLocalNow(), KnownChannels.Whisper, ChannelKind.Whisper, sender, Truncate(text));
    }

    // NewChatChannels: [0] chat-settings channel ID, [1] key (Byte = global, other integer = runtime ID)
    private void ParseChannelAnnouncement(Dictionary<byte, object> p)
    {
        // Current layout (seen in game, Nov 2026): [0] byte[] holding little-endian Int32 channel IDs,
        // [1] the matching room numbers, e.g. [0]={27,0,0,0} [1]={479}.
        if (p.TryGetValue(0, out var packed) && packed is byte[] bytes && p.TryGetValue(1, out var roomsObj))
        {
            var rooms = ParamReader.ToIntList(roomsObj);
            if (bytes.Length % 4 == 0 && rooms.Count == bytes.Length / 4 && roomsObj is not string)
            {
                for (int i = 0; i < rooms.Count; i++)
                    MapRoom(rooms[i], BitConverter.ToInt32(bytes, i * 4));
                return;
            }
        }

        // Older layout: [0] channel, [1] key (Byte = global, other integer = room).
        if (!ParamReader.TryGetInt(p, 0, out int channelId) || channelId < 0 || !p.TryGetValue(1, out var key))
        {
            Diagnostic?.Invoke("NewChatChannels did not match a known layout; not used");
            return;
        }

        if (key is byte globalKey)
        {
            lock (_lock) _globalChannels[globalKey] = channelId;
            Diagnostic?.Invoke($"global channel {globalKey} -> tab channel {channelId}");
        }
        else if (ParamReader.TryToInt(key, out int runtimeId))
        {
            MapRoom(runtimeId, channelId);
        }
    }

    private void MapRoom(int room, int channel)
    {
        lock (_lock)
        {
            if (_dynamicChannels.TryGetValue(room, out int old) && old == channel) return;
            _dynamicChannels[room] = channel;
        }
        Diagnostic?.Invoke($"room {room} -> channel {channel}");
        RoomJoined?.Invoke(room, channel);
    }

    private readonly bool[] _otherSeen = new bool[4096];

    private void LogOtherEvent(EventPacket packet)
    {
        int code = packet.EventCode;
        if (Diagnostic is null || code < 0 || code >= _otherSeen.Length || _otherSeen[code]) return;
        if (!packet.Parameters.Values.Any(v => v is string { Length: > 0 } s && !s.StartsWith('@') || v is string[] { Length: > 0 })) return;
        _otherSeen[code] = true;
        Diagnostic.Invoke($"other event {(EventCodes)code}: {EventShape.Describe(packet.Parameters)}");
    }

    private void LogShapeOnce(string id, string label, IReadOnlyDictionary<byte, object> p, params byte[] hidden)
    {
        if (Diagnostic is null) return;
        lock (_lock)
        {
            if (_shapesLogged.Count >= 200 || !_shapesLogged.Add(id)) return;
        }
        Diagnostic.Invoke($"{label}: {EventShape.Describe(p, hidden)}");
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
