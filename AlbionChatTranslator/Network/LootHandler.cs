using AlbionChatTranslator.Translation;
using StatisticsAnalysisTool.Network;

namespace AlbionChatTranslator.Network;

/// <summary>
/// Handles game-server loot and harvest events.
/// EventCode 275 (OtherGrabbedLoot) — someone looted an item from a body.
/// EventCode 54  (HarvestFinished)  — local player finished gathering a resource.
/// </summary>
public class LootHandler : PacketHandler<EventPacket>
{
    public event Action<ChatMessage>? MessageReceived;

    public Func<int, IReadOnlyList<string>>? ChannelResolver { get; set; }

    /// <summary>Channel 40 = Loot Log (also in Combat and Info).</summary>
    public int LootChannelId { get; set; } = 40;

    private int _playerEntityId;

    protected override Task OnHandleAsync(EventPacket packet)
    {
        ChatMessage? msg = packet.EventCode switch
        {
            635 => TrackPlayerEntity(packet),
            275 => ParseGrabbedLoot(packet),
            54  => ParseHarvestFinished(packet),
            _   => null
        };

        if (msg?.Tabs.Count > 0)
            MessageReceived?.Invoke(msg);

        return NextAsync(packet);
    }

    // ── EventCode 635: player entity ID (shared with SystemMessageHandler) ──

    private ChatMessage? TrackPlayerEntity(EventPacket packet)
    {
        if (packet.Parameters.TryGetValue(0, out var eid) && eid is int id)
            _playerEntityId = id;
        return null;
    }

    // ── EventCode 256: OtherGrabbedLoot ──────────────────────────────────────

    private ChatMessage? ParseGrabbedLoot(EventPacket packet)
    {
        // [3] = IsSilver — skip silver, EC275 already covers that
        if (packet.Parameters.TryGetValue(3, out var silverObj) && silverObj is true)
            return null;

        // [2] = LooterByName, [1] = LootedFromName, [4] = ItemIndex, [5] = Quantity
        if (!packet.Parameters.TryGetValue(2, out var looterObj) || looterObj is not string looter) return null;
        if (string.IsNullOrWhiteSpace(looter)) return null;

        if (!packet.Parameters.TryGetValue(4, out var indexObj)) return null;
        int itemIndex = indexObj switch { int i => i, short s => s, _ => -1 };
        if (itemIndex < 0) return null;

        var itemName = ItemIndex.GetUniqueName(itemIndex) ?? $"Item#{itemIndex}";

        int qty = 1;
        if (packet.Parameters.TryGetValue(5, out var qtyObj))
            qty = qtyObj switch { int i => i, short s => s, byte b => b, _ => 1 };

        string from = string.Empty;
        if (packet.Parameters.TryGetValue(1, out var fromObj) && fromObj is string f && !string.IsNullOrWhiteSpace(f))
            from = $" from {f}";

        var text = qty > 1
            ? $"{looter} looted {qty}x {itemName}{from}."
            : $"{looter} looted {itemName}{from}.";

        return MakeMessage(text);
    }

    // ── EventCode 54: HarvestFinished ─────────────────────────────────────────

    private ChatMessage? ParseHarvestFinished(EventPacket packet)
    {
        // [0] = UserObjectId (long) — only show for local player
        if (_playerEntityId == 0) return null;
        if (!packet.Parameters.TryGetValue(0, out var userObj)) return null;
        long userId = userObj switch { long l => l, int i => i, _ => -1 };
        if (userId != _playerEntityId) return null;

        // [4] = ItemId, [5] = standard, [6] = collector bonus, [7] = premium bonus
        if (!packet.Parameters.TryGetValue(4, out var indexObj)) return null;
        int itemIndex = indexObj switch { int i => i, short s => s, _ => -1 };
        if (itemIndex < 0) return null;

        var itemName = ItemIndex.GetUniqueName(itemIndex) ?? $"Item#{itemIndex}";

        int standard  = packet.Parameters.TryGetValue(5, out var s5) ? s5 switch { int i => i, _ => 0 } : 0;
        int collector = packet.Parameters.TryGetValue(6, out var s6) ? s6 switch { int i => i, _ => 0 } : 0;
        int premium   = packet.Parameters.TryGetValue(7, out var s7) ? s7 switch { int i => i, _ => 0 } : 0;
        int total     = standard + collector + premium;
        if (total <= 0) total = standard;

        var text = total > 1
            ? $"You harvested {total}x {itemName}."
            : $"You harvested {itemName}.";

        return MakeMessage(text);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ChatMessage MakeMessage(string text)
    {
        var tabs = ChannelResolver?.Invoke(LootChannelId) ?? (IReadOnlyList<string>)[];
        return new ChatMessage(DateTime.Now, tabs, "System", string.Empty, text);
    }
}
