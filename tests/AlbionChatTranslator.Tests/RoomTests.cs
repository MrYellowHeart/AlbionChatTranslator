using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Translation;
using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using StatisticsAnalysisTool.Network;

namespace AlbionChatTranslator.Tests;

/// <summary>Chat rooms, using the event layouts recorded from the live game (Nov 2026).</summary>
public class RoomTests
{
    private static (ChatEventParser Parser, List<ChatLine> Lines, List<(int Room, int Channel)> Joins) Create()
    {
        var parser = new ChatEventParser();
        var lines = new List<ChatLine>();
        var joins = new List<(int, int)>();
        parser.LineReceived += lines.Add;
        parser.RoomJoined += (r, c) => joins.Add((r, c));
        return (parser, lines, joins);
    }

    private static EventPacket Packet(EventCodes code, Dictionary<byte, object> p) => new((short)code, p);

    [Fact]
    public void Joined_chat_channel_sorts_the_room_into_its_channel()
    {
        var (parser, lines, joins) = Create();
        // As logged: Help = channel 5 in room 21, Recruit = 2 in room 18, guild chat = 24.
        parser.Handle(Packet(EventCodes.JoinedChatChannel, new() { [0] = 5, [1] = 21 }));
        parser.Handle(Packet(EventCodes.JoinedChatChannel, new() { [0] = 2, [1] = 18 }));
        parser.Handle(Packet(EventCodes.JoinedChatChannel, new() { [0] = KnownChannels.Guild, [1] = 640 }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 21, [1] = "Ana", [2] = "how do I get to Caerleon", [3] = (byte)0 }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 18, [1] = "Bo", [2] = "recruiting for zvz", [3] = (byte)0, [5] = "abcdefghijklmnop" }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 640, [1] = "Cy", [2] = "guild meeting now", [3] = (byte)0 }));

        Assert.Equal([5, 2, KnownChannels.Guild], lines.Select(l => l.ChannelId));
        Assert.All(lines, l => Assert.Null(l.Room));
        Assert.Equal(ChannelKind.Guild, lines[2].Kind);
        Assert.Equal([(21, 5), (18, 2), (640, KnownChannels.Guild)], joins);
    }

    [Fact]
    public void Packed_new_chat_channels_are_read()
    {
        var (parser, lines, joins) = Create();
        // As logged: [0]=byte[]{27,0,0,0} [1]=short[]{479}; plus a second pair.
        parser.Handle(Packet(EventCodes.NewChatChannels, new() { [0] = new byte[] { 27, 0, 0, 0, 3, 0, 0, 0 }, [1] = new short[] { 479, 19 } }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 19, [1] = "Di", [2] = "lf healer for hce", [3] = (byte)0 }));

        Assert.Equal([(479, 27), (19, 3)], joins);
        Assert.Equal(3, lines.Single().ChannelId);
    }

    [Fact]
    public void Unassigned_rooms_say_so_and_repeat_joins_are_quiet()
    {
        var (parser, lines, joins) = Create();
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 22, [1] = "Ed", [2] = "anyone around", [3] = (byte)0 }));
        Assert.Equal(22, lines[0].Room);
        Assert.Equal(ChannelKind.Other, lines[0].Kind);

        parser.Handle(Packet(EventCodes.JoinedChatChannel, new() { [0] = 7, [1] = 22 }));
        parser.Handle(Packet(EventCodes.JoinedChatChannel, new() { [0] = 7, [1] = 22 }));
        Assert.Single(joins);
    }

    [Fact]
    public void Room_assignments_cross_the_pipe_and_are_remembered()
    {
        var msg = PipeProtocol.TryDeserialize(PipeProtocol.Serialize(new PipeMessage { Type = PipeMessage.TypeRoom, Room = 18, Channel = 2 }));
        Assert.Equal((18, 2), (msg!.Room, msg.Channel));

        var dir = Path.Combine(Path.GetTempPath(), "act-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = new AppSettings();
            s.Rooms[18] = 2;
            s.Rooms[21] = 5;
            s.Save(dir);
            Assert.Equal(new Dictionary<int, int> { [18] = 2, [21] = 5 }, AppSettings.Load(dir).Rooms);
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class RoomDefaultsTests
{
    private static readonly Dictionary<int, int> None = [];

    [Theory]
    [InlineData(18, 2)]  // Recruit
    [InlineData(19, 3)]  // Looking for group
    [InlineData(21, 5)]  // Help
    [InlineData(2, 8)]   // in none of the default tabs
    public void Fixed_rooms_sort_without_a_relog(int room, int channel)
    {
        Assert.True(KnownChannels.TryResolveRoom(room, None, out int resolved));
        Assert.Equal(channel, resolved);
    }

    [Fact]
    public void The_games_own_announcement_wins_and_unknown_rooms_stay_unknown()
    {
        Assert.True(KnownChannels.TryResolveRoom(18, new Dictionary<int, int> { [18] = 5 }, out int resolved));
        Assert.Equal(5, resolved);
        Assert.False(KnownChannels.TryResolveRoom(105579, None, out _));
    }

    [Fact]
    public void Removed_rooms_are_forgotten()
    {
        var parser = new ChatEventParser();
        var lines = new List<ChatLine>();
        var removed = new List<int>();
        parser.LineReceived += lines.Add;
        parser.RoomRemoved += removed.Add;
        parser.Handle(new EventPacket((short)EventCodes.JoinedChatChannel, new Dictionary<byte, object> { [0] = 27, [1] = 409 }));
        parser.Handle(new EventPacket((short)EventCodes.RemovedChatChannel, new Dictionary<byte, object> { [0] = 409 }));
        parser.Handle(new EventPacket((short)EventCodes.ChatMessage, new Dictionary<byte, object> { [0] = 409, [1] = "Al", [2] = "anyone here", [3] = (byte)0 }));

        Assert.Equal([409], removed);
        Assert.Equal(409, lines.Single().Room); // no longer assigned
    }
}

public class TabShowsTests
{
    private static readonly int[] General = [23, 27, 31, 28, 38, 33, 34, 26];
    private static readonly int[] Recruit = [2, 28, 33];

    [Fact]
    public void Language_chat_rooms_show_where_the_tab_lists_23()
    {
        Assert.True(KnownChannels.TabShows(General, 8));
        Assert.True(KnownChannels.TabShows(General, 22));
        Assert.False(KnownChannels.TabShows(Recruit, 8));
        Assert.False(KnownChannels.TabShows(General, 7));   // not a language room
        Assert.False(KnownChannels.TabShows(General, 2));   // Recruit stays in Recruit
        Assert.True(KnownChannels.TabShows(Recruit, 2));
        Assert.True(KnownChannels.TabShows(General, 27));   // zone chat
    }
}

public class WhisperTests
{
    [Theory]
    [InlineData((byte)3)] // layout seen in game, Nov 2026
    [InlineData((byte)2)] // older layout
    public void Whispers_are_read(byte textKey)
    {
        var parser = new ChatEventParser();
        var lines = new List<ChatLine>();
        parser.LineReceived += lines.Add;
        parser.Handle(new EventPacket((short)EventCodes.ChatWhisper, new Dictionary<byte, object> { [0] = "Somebody", [textKey] = "oi, voce vende a bolsa?", [4] = (byte)0 }));

        var line = Assert.Single(lines);
        Assert.Equal((KnownChannels.Whisper, ChannelKind.Whisper, "Somebody", "oi, voce vende a bolsa?"), (line.ChannelId, line.Kind, line.Sender, line.Text));
    }
}

public class OutgoingWhisperTests
{
    private static (ChatEventParser, List<ChatLine>) Create()
    {
        var parser = new ChatEventParser();
        var lines = new List<ChatLine>();
        parser.LineReceived += lines.Add;
        return (parser, lines);
    }

    [Fact]
    public void Own_whispers_are_shown_as_sent_to_the_recipient()
    {
        var (parser, lines) = Create();
        // As logged: request 193: [0]=text(8) [1]=text(21) [253]=short 193 [255]=int 36
        parser.HandleRequest(new RequestPacket(193, new Dictionary<byte, object> { [0] = "Somebody", [1] = "hello there my friend", [253] = (short)193, [255] = 36 }));

        var line = Assert.Single(lines);
        Assert.True(line.Outgoing);
        Assert.Equal((KnownChannels.Whisper, ChannelKind.Whisper, "Somebody", "hello there my friend"), (line.ChannelId, line.Kind, line.Sender, line.Text));
    }

    [Fact]
    public void Other_requests_and_other_layouts_are_not_chat()
    {
        var (parser, lines) = Create();
        parser.HandleRequest(new RequestPacket(194, new Dictionary<byte, object> { [0] = "Somebody", [1] = "hello there", [253] = (short)194 }));
        parser.HandleRequest(new RequestPacket(193, new Dictionary<byte, object> { [0] = "Somebody", [1] = "hello there", [2] = 5, [253] = (short)193 }));
        parser.HandleRequest(new RequestPacket(193, new Dictionary<byte, object> { [0] = 7, [1] = "hello there", [253] = (short)193 }));
        Assert.Empty(lines);
    }
}

public class ReplyMemoryTests
{
    [Fact]
    public void A_sent_reply_recalls_what_was_typed()
    {
        var memory = new ReplyMemory(capacity: 2);
        memory.Remember("가방 팔아요?", "do you sell bags?", "ko");
        Assert.True(memory.TryRecall("  가방   팔아요? ", out var typed, out var language));
        Assert.Equal(("do you sell bags?", "ko"), (typed, language));
        Assert.False(memory.TryRecall("something else", out _, out _));

        memory.Remember("b", "two", "es");
        memory.Remember("c", "three", "es");
        Assert.False(memory.TryRecall("가방 팔아요?", out _, out _)); // only the last few are kept
    }
}
