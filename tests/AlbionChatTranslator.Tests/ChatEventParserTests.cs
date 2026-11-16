using AlbionChatTranslator.Core.Chat;
using StatisticsAnalysisTool.Network;

namespace AlbionChatTranslator.Tests;

public class ChatEventParserTests
{
    private static (ChatEventParser Parser, List<ChatLine> Lines, List<IReadOnlyList<ChatTab>> Tabs) Create()
    {
        var parser = new ChatEventParser();
        var lines = new List<ChatLine>();
        var tabs = new List<IReadOnlyList<ChatTab>>();
        parser.LineReceived += lines.Add;
        parser.TabsReceived += tabs.Add;
        return (parser, lines, tabs);
    }

    private static EventPacket Packet(EventCodes code, Dictionary<byte, object> p) => new((short)code, p);

    [Fact]
    public void Say_is_parsed_and_emotes_are_ignored()
    {
        var (parser, lines, _) = Create();
        parser.Handle(Packet(EventCodes.ChatSay, new() { [0] = "Bob", [1] = "hola amigos" }));
        parser.Handle(Packet(EventCodes.ChatSay, new() { [0] = "Bob", [1] = "waves", [6] = true }));

        var line = Assert.Single(lines);
        Assert.Equal(("Bob", "hola amigos", ChannelKind.Say, KnownChannels.Say), (line.Sender, line.Text, line.Kind, line.ChannelId));
    }

    [Fact]
    public void Whisper_uses_parameter_two_for_text()
    {
        var (parser, lines, _) = Create();
        parser.Handle(Packet(EventCodes.ChatWhisper, new() { [0] = "Ana", [1] = "ignored", [2] = "oi tudo bem" }));
        Assert.Equal("oi tudo bem", Assert.Single(lines).Text);
    }

    [Theory]
    [InlineData((short)1866)]
    [InlineData(1866)]
    [InlineData(1866L)]
    public void Dynamic_channel_ids_map_through_NewChatChannels_whatever_the_integer_type(object runtimeId)
    {
        var (parser, lines, _) = Create();
        parser.Handle(Packet(EventCodes.NewChatChannels, new() { [0] = KnownChannels.Guild, [1] = runtimeId }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = runtimeId, [1] = "Zed", [2] = "guild stuff" }));

        var line = Assert.Single(lines);
        Assert.Equal(ChannelKind.Guild, line.Kind);
        Assert.Equal(KnownChannels.Guild, line.ChannelId);
    }

    [Fact]
    public void Global_channels_use_the_byte_value_unless_the_game_announces_a_mapping()
    {
        // The game's tab layout lists global channels by the same number (e.g. Recruit = 2).
        var (parser, lines, _) = Create();
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = (byte)2, [1] = "Kim", [2] = "recruiting" }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = (byte)5, [1] = "Kim", [2] = "need help" }));
        parser.Handle(Packet(EventCodes.NewChatChannels, new() { [0] = 50, [1] = (byte)99 }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = (byte)99, [1] = "Kim", [2] = "announced" }));

        Assert.Equal([2, 5, 50], lines.Select(l => l.ChannelId));
        Assert.All(lines, l => Assert.Equal(ChannelKind.Global, l.Kind));
    }

    [Fact]
    public void Channel_mappings_and_unknown_channels_are_reported_without_message_text()
    {
        var (parser, lines, _) = Create();
        var notes = new List<string>();
        parser.Diagnostic += notes.Add;

        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = (short)2028, [1] = "Zed", [2] = "secret guild plan" }));
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = (short)2028, [1] = "Zed", [2] = "again" }));
        parser.Handle(Packet(EventCodes.NewChatChannels, new() { [0] = KnownChannels.Guild, [1] = (short)2028 }));

        Assert.Equal(2, lines.Count); // unannounced channels are still delivered (the UI puts them in the first tab)
        Assert.Single(notes, n => n.Contains("unannounced runtime channel 2028")); // not repeated
        Assert.Single(notes, n => n.Contains("2028 -> tab channel 24"));
        Assert.DoesNotContain(notes, n => n.Contains("secret") || n.Contains("Zed"));
    }

    [Fact]
    public void Messages_without_sender_are_not_player_chat()
    {
        var (parser, lines, _) = Create();
        parser.Handle(Packet(EventCodes.ChatMessage, new() { [0] = 5, [2] = "Server restart in 5 minutes" }));
        Assert.Empty(lines);
    }

    [Fact]
    public void Tabs_accept_object_arrays_and_typed_arrays()
    {
        var (parser, _, tabs) = Create();
        parser.Handle(Packet(EventCodes.UpdateChatSettings, new()
        {
            [0] = new object[] { "@CHAT_SETTINGS_TAB_NAME_GENERAL", "My Tab" },
            [1] = new object[] { new int[] { 31, 28 }, new short[] { 24, 25 } },
        }));
        parser.Handle(Packet(EventCodes.UpdateChatSettings, new()
        {
            [0] = new[] { "Trade" },
            [1] = new[] { new byte[] { 8 } },
        }));

        Assert.Equal(2, tabs.Count);
        Assert.Equal("General", tabs[0][0].Name);
        Assert.Equal([31, 28], tabs[0][0].ChannelIds);
        Assert.Equal([24, 25], tabs[0][1].ChannelIds);
        Assert.Equal([8], Assert.Single(tabs[1]).ChannelIds);
    }

    [Fact]
    public void Handled_events_still_have_their_expected_upstream_numbers()
    {
        // Not a hard rule (SBI renumbers), but a surprise here means the vendored list moved.
        Assert.Equal(73, (int)EventCodes.ChatMessage);
        Assert.Equal(74, (int)EventCodes.ChatSay);
        Assert.Equal(75, (int)EventCodes.ChatWhisper);
        Assert.Equal(205, (int)EventCodes.NewChatChannels);
        Assert.Equal(217, (int)EventCodes.UpdateChatSettings);
    }
}
