using StatisticsAnalysisTool.Network;
using System.Text.Json;

namespace AlbionChatTranslator.Network;

/// <summary>
/// Reconstructs system messages from game state events that the client
/// normally renders locally (zone entry, currency gain, PvP results, etc.).
/// Fires ChatMessage with Channel="System" and empty Sender so the
/// existing rendering path shows them with a [System] label, never translated.
/// </summary>
public class SystemMessageHandler : PacketHandler<EventPacket>
{
    public event Action<ChatMessage>? MessageReceived;

    /// <summary>
    /// Routes system messages through the same channel-resolver mechanism as chat
    /// messages. Defaults to channel ID 37 which is the zone/area notification
    /// channel shared by the General, Combat, and Info tabs.
    /// </summary>
    public Func<int, IReadOnlyList<string>>? ChannelResolver { get; set; }

    /// <summary>
    /// Channel ID for zone-entry messages. Channel 43 is in Combat and Info but NOT General.
    /// </summary>
    public int ZoneChannelId { get; set; } = 43;

    /// <summary>
    /// Channel ID for personal combat stats (heal, fame, energy, challenge points).
    /// Channel 36 is in Combat ONLY — not in Info or General.
    /// </summary>
    public int PersonalCombatChannelId { get; set; } = 36;

    /// <summary>
    /// Channel ID for shared combat events (honorable combat, reputation, duel results).
    /// Channel 34 is in Combat and Info but NOT General.
    /// </summary>
    public int CombatEventChannelId { get; set; } = 34;

    /// <summary>
    /// Channel ID for player kill notifications.
    /// Channel 37 is in General, Combat, and Info.
    /// </summary>
    public int KillChannelId { get; set; } = 37;

    /// <summary>
    /// Channel ID for silver/loot gain events.
    /// Channel 40 is in Loot Log, Combat, and Info but NOT General.
    /// </summary>
    public int SilverChannelId { get; set; } = 40;

    /// <summary>
    /// Channel ID for social notifications (player online/offline).
    /// Channel 43 is in Combat and Info but NOT General.
    /// </summary>
    public int SocialChannelId { get; set; } = 43;

    // Deduplication: suppress repeated EC45 events for the same zone while
    // the player browses (EC45 fires 20+ times for the same sub-zone).
    private string? _lastZoneId;

    // Cross-zone travel: EC216 fires when the player crosses to a new zone server.
    // No EC45 follows — instead EC362 fires with zone entity ID strings from which
    // we extract the biome/city name.
    private bool _pendingCrossZoneEntry;

    // Player entity ID in the current zone, tracked from EC635.
    // Used to identify self-heal events in EC6.
    private int _playerEntityId;

    protected override Task OnHandleAsync(EventPacket packet)
    {
        List<ChatMessage> msgs = [];

        switch (packet.EventCode)
        {
            case 635:
                // Zone-load player identity: [0] = player entity ID (Int32 only).
                // Fires as Byte in some contexts (dungeon load) — only accept Int32.
                if (packet.Parameters.TryGetValue(0, out var eidObj) && eidObj is int eid)
                    _playerEntityId = eid;
                break;

            case 6:
                var combatMsg = ParseCombatHit(packet);
                if (combatMsg != null) msgs.Add(combatMsg);
                break;

            case 301:
                var repMsg = ParseReputationGain(packet);
                if (repMsg != null) msgs.Add(repMsg);
                break;

            case 166:
                var killMsg = ParseHonorableKill(packet);
                if (killMsg != null) msgs.Add(killMsg);
                break;

            case 163:
                var pvpMsg = ParsePvpResult(packet);
                if (pvpMsg != null) msgs.Add(pvpMsg);
                break;

            case 275:
                var silverMsg = ParseSilverGain(packet);
                if (silverMsg != null) msgs.Add(silverMsg);
                break;

            case 165:
                var playerKillMsg = ParsePlayerKill(packet);
                if (playerKillMsg != null) msgs.Add(playerKillMsg);
                break;

            case 8:
                var energyMsg = ParseEnergyGain(packet);
                if (energyMsg != null) msgs.Add(energyMsg);
                break;

            case 82:
                var fameMsg = ParseFameGain(packet);
                if (fameMsg != null) msgs.Add(fameMsg);
                break;

            case 84:
                var fameCreditsMsg = ParseFameCredits(packet);
                if (fameCreditsMsg != null) msgs.Add(fameCreditsMsg);
                break;

            case 373:
                var challengeMsg = ParseChallengePoints(packet);
                if (challengeMsg != null) msgs.Add(challengeMsg);
                break;

            case 294:
                var onlineMsg = ParsePlayerOnline(packet);
                if (onlineMsg != null) msgs.Add(onlineMsg);
                break;
        }

        foreach (var msg in msgs)
            if (msg.Tabs.Count > 0)
                MessageReceived?.Invoke(msg);

        return NextAsync(packet);
    }

    // ── EventCode 45: sub-zone entry (same server) ────────────────────────────

    private ChatMessage? ParseZoneEntry(EventPacket packet)
    {
        // [0] Byte = local player sub-zone transition; Int16/Int32 = other entity.
        if (!packet.Parameters.TryGetValue(0, out var discriminator) || discriminator is not byte)
            return null;

        // [3] = zone ID string, e.g. "STEPPE_GREEN_MARKETPLACE_CENTERCITY"
        if (!packet.Parameters.TryGetValue(3, out var zoneObj) || zoneObj is not string zoneId)
            return null;
        if (string.IsNullOrWhiteSpace(zoneId)) return null;

        // Suppress duplicate fires for the same zone
        if (zoneId == _lastZoneId) return null;
        _lastZoneId = zoneId;

        // Sub-zone entry supersedes any pending cross-zone entry.
        _pendingCrossZoneEntry = false;

        var zoneName = ZoneNames.Format(zoneId);
        return MakeMessage($"You entered {zoneName}.", ZoneChannelId);
    }

    // ── EventCode 362: zone entity — used for cross-zone city name ────────────

    private ChatMessage? ParseCrossZoneEntry(EventPacket packet)
    {
        // Only fire when EC216 indicated a cross-zone transition.
        if (!_pendingCrossZoneEntry) return null;

        // [1] = zone ID string of an entity in the new zone, e.g. "STEPPE_RED_FACTION_WARFARE_OUTPOST_TOWER"
        // Sufficient to extract the biome/city prefix.
        if (!packet.Parameters.TryGetValue(1, out var zoneObj) || zoneObj is not string zoneId)
            return null;
        if (string.IsNullOrWhiteSpace(zoneId)) return null;

        var city = ZoneNames.ExtractCity(zoneId);
        if (city == null) return null;

        // Deduplicate and suppress further EC362 events for this transition.
        _pendingCrossZoneEntry = false;
        if (city == _lastZoneId) return null;
        _lastZoneId = city;

        return MakeMessage($"You entered {city}.", ZoneChannelId);
    }

    // ── EventCode 6: combat hit (damage or heal) ──────────────────────────────

    private ChatMessage? ParseCombatHit(EventPacket packet)
    {
        // [2] = value: positive = heal, negative = damage. Skip if we don't know our entity yet.
        if (_playerEntityId == 0) return null;
        if (!packet.Parameters.TryGetValue(2, out var valObj)) return null;
        float value = valObj switch
        {
            float  f => f,
            double d => (float)d,
            int    i => i,
            _        => float.NaN
        };
        if (float.IsNaN(value) || value == 0) return null;

        // [0] = entity that received the hit; only emit for the local player.
        if (!packet.Parameters.TryGetValue(0, out var targetObj)) return null;
        int target = targetObj switch
        {
            int   i => i,
            short s => s,
            _       => -1
        };
        if (target == _playerEntityId)
        {
            int amount = (int)Math.Abs(Math.Round(value));
            if (value > 0)
                return MakeMessage($"You healed yourself for {amount} Health.", PersonalCombatChannelId);
            else
                return MakeMessage($"You received {amount} damage.", PersonalCombatChannelId);
        }

        // Outgoing damage: check if local player is the source.
        if (value < 0 && packet.Parameters.TryGetValue(6, out var srcObj))
        {
            int src = srcObj switch { int i => i, short s => s, _ => -1 };
            if (src == _playerEntityId)
            {
                int amount = (int)Math.Abs(Math.Round(value));
                return MakeMessage($"You dealt {amount} damage to a mob.", PersonalCombatChannelId);
            }
        }

        return null;
    }

    // ── EventCode 301: reputation gain ────────────────────────────────────────

    private ChatMessage? ParseReputationGain(EventPacket packet)
    {
        // [0] = player entity; fires for all players in zone — only emit for local player.
        if (_playerEntityId == 0) return null;
        if (!packet.Parameters.TryGetValue(0, out var entityObj)) return null;
        int entity = entityObj switch { int i => i, short s => s, _ => -1 };
        if (entity != _playerEntityId) return null;

        // [2] = reputation gained (float, e.g. 4.6368)
        if (!packet.Parameters.TryGetValue(2, out var gainObj)) return null;
        float gain = gainObj switch
        {
            float  f => f,
            double d => (float)d,
            _        => float.NaN
        };
        if (float.IsNaN(gain) || gain <= 0) return null;

        return MakeMessage($"Your reputation increased by {gain:F2}.", CombatEventChannelId);
    }

    // ── EventCode 166: honorable PvP kill ─────────────────────────────────────

    private ChatMessage? ParseHonorableKill(EventPacket packet)
    {
        // [3] = winner name, [5] = loser name
        if (!packet.Parameters.TryGetValue(3, out var winnerObj) || winnerObj is not string winner) return null;
        if (!packet.Parameters.TryGetValue(5, out var loserObj)  || loserObj  is not string loser)  return null;
        if (string.IsNullOrWhiteSpace(winner) || string.IsNullOrWhiteSpace(loser)) return null;

        return MakeMessage($"{winner} has bested {loser} in honorable combat!", CombatEventChannelId);
    }

    // ── EventCode 163: PvP combat result (silver reward) ──────────────────────

    private ChatMessage? ParsePvpResult(EventPacket packet)
    {
        // [2] = JSON, e.g. {"reason":"...","challenger":"Askelah","challengee":"MaxLeBlond","wager":"10000"}
        // challengee = winner; wager / 10000 = silver
        if (!packet.Parameters.TryGetValue(2, out var jsonObj) || jsonObj is not string json) return null;
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("challenger", out var challengerEl)) return null;
            var winner = challengerEl.GetString();
            if (string.IsNullOrWhiteSpace(winner)) return null;

            root.TryGetProperty("challengee", out var challengeeEl);
            var loser = challengeeEl.GetString();

            long silver = 0;
            if (root.TryGetProperty("wager", out var wagerEl))
            {
                var wagerStr = wagerEl.ValueKind == JsonValueKind.String
                    ? wagerEl.GetString()
                    : wagerEl.GetRawText();
                if (long.TryParse(wagerStr, out var raw))
                    silver = raw / 10_000;
            }

            var loserText  = !string.IsNullOrWhiteSpace(loser) ? $" defeated {loser}" : string.Empty;
            var silverText = silver > 0 ? $" and receives {silver:N0} Silver" : string.Empty;
            return MakeMessage($"{winner}{loserText}{silverText}.", CombatEventChannelId);
        }
        catch
        {
            return null;
        }
    }

    // ── EventCode 275: silver/loot gain ───────────────────────────────────────

    private ChatMessage? ParseSilverGain(EventPacket packet)
    {
        // [2] = killer name, [3] = True (PvP), [5] = raw silver; fires for all players' kills
        if (!packet.Parameters.TryGetValue(2, out var nameObj) || nameObj is not string name) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (!packet.Parameters.TryGetValue(5, out var amountObj)) return null;
        long raw = amountObj switch
        {
            int  i => i,
            long l => l,
            _      => 0
        };
        if (raw <= 0) return null;

        var silver = raw / 10_000;
        return MakeMessage($"{name} gained +{silver:N0} Silver.", CombatEventChannelId);
    }

    // ── EventCode 165: player killed player ───────────────────────────────────

    private ChatMessage? ParsePlayerKill(EventPacket packet)
    {
        // [1] = victim entity, [2] = victim name, [3] = victim guild
        // [9] = killer entity, [10] = killer name, [11] = killer guild
        if (!packet.Parameters.TryGetValue(2,  out var vNameObj) || vNameObj  is not string victimName) return null;
        if (!packet.Parameters.TryGetValue(10, out var kNameObj) || kNameObj  is not string killerName) return null;
        if (string.IsNullOrWhiteSpace(victimName) || string.IsNullOrWhiteSpace(killerName)) return null;

        packet.Parameters.TryGetValue(3,  out var vGuildObj);
        packet.Parameters.TryGetValue(11, out var kGuildObj);
        string victimGuild = vGuildObj is string vg && !string.IsNullOrWhiteSpace(vg) ? $" [{vg}]" : string.Empty;
        string killerGuild = kGuildObj is string kg && !string.IsNullOrWhiteSpace(kg) ? $" [{kg}]" : string.Empty;

        // Personalise message if the local player is involved (requires entity ID from EC635)
        if (_playerEntityId != 0)
        {
            int victimEntity = packet.Parameters.TryGetValue(1, out var ve) ? ve switch { int i => i, short s => s, _ => -1 } : -1;
            int killerEntity = packet.Parameters.TryGetValue(9, out var ke) ? ke switch { int i => i, short s => s, _ => -1 } : -1;

            if (killerEntity == _playerEntityId)
                return MakeKillMessage($"You killed {victimName}{victimGuild}!", KillChannelId);
            if (victimEntity == _playerEntityId)
                return MakeKillMessage($"You were killed by {killerName}{killerGuild}.", KillChannelId);
        }

        return MakeKillMessage($"{killerName}{killerGuild} killed {victimName}{victimGuild}.", KillChannelId);
    }

    // ── EventCode 294: player online notification ──────────────────────────────

    private ChatMessage? ParsePlayerOnline(EventPacket packet)
    {
        // [2] = player name. [0] = Boolean (True = online) when present, but may be absent.
        if (!packet.Parameters.TryGetValue(2, out var nameObj) || nameObj is not string name) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;

        bool cameOnline = !packet.Parameters.TryGetValue(0, out var stateObj) || stateObj is not bool b || b;
        var verb = cameOnline ? "has come online." : "has gone offline.";
        return MakeMessage($"{name} {verb}", SocialChannelId);
    }

    // ── EventCode 8: energy gain (discriminated by [4] == 0x08) ──────────────

    private ChatMessage? ParseEnergyGain(EventPacket packet)
    {
        // [4] discriminates event type. Energy regen = 0x02 (self) or 0x08 (from mob).
        if (!packet.Parameters.TryGetValue(4, out var typeObj) || typeObj is not byte typeVal) return null;
        if (typeVal != 0x02 && typeVal != 0x08) return null;

        // [0] = target, [5] = source. Self-regen: target == source.
        // Accept if we know our entity ID OR if it's clearly a self-regen ([0] == [5]).
        if (!packet.Parameters.TryGetValue(0, out var targetObj)) return null;
        int target = targetObj switch { int i => i, short s => s, _ => -1 };
        if (target < 0) return null;

        if (_playerEntityId == 0) return null;
        if (target != _playerEntityId) return null;

        // [2] = energy amount
        if (!packet.Parameters.TryGetValue(2, out var valObj)) return null;
        float energy = valObj switch
        {
            float  f => f,
            double d => (float)d,
            int    i => i,
            _        => float.NaN
        };
        if (float.IsNaN(energy) || energy <= 0) return null;

        int amount = (int)Math.Round(energy);
        return MakeMessage($"You regenerated {amount} Energy.", PersonalCombatChannelId);
    }

    // ── EventCode 82: fame gain ────────────────────────────────────────────────

    private ChatMessage? ParseFameGain(EventPacket packet)
    {
        // [0] = player entity; only emit for the local player.
        if (_playerEntityId == 0) return null;
        if (!packet.Parameters.TryGetValue(0, out var entityObj)) return null;
        int entity = entityObj switch
        {
            int   i => i,
            short s => s,
            _       => -1
        };
        if (entity != _playerEntityId) return null;

        // [2] = raw fame * 10000 (total including premium bonus)
        if (!packet.Parameters.TryGetValue(2, out var rawObj)) return null;
        long raw = rawObj switch
        {
            int  i => i,
            long l => l,
            _      => 0L
        };
        if (raw <= 0) return null;

        var fame = raw / 10_000;
        return MakeMessage($"You gained +{fame:N0} Fame.", CombatEventChannelId);
    }

    // ── EventCode 84: fame credits ────────────────────────────────────────────

    private ChatMessage? ParseFameCredits(EventPacket packet)
    {
        // [2] = raw fame credits * 10000
        if (!packet.Parameters.TryGetValue(2, out var rawObj)) return null;
        long raw = rawObj switch
        {
            int  i => i,
            long l => l,
            _      => 0L
        };
        if (raw <= 0) return null;

        var credits = raw / 10_000;
        return MakeMessage($"You gained +{credits:N0} Fame Credits.", CombatEventChannelId);
    }

    // ── EventCode 373: challenge points ───────────────────────────────────────

    private ChatMessage? ParseChallengePoints(EventPacket packet)
    {
        // [0] = raw challenge points * 10000
        if (!packet.Parameters.TryGetValue(0, out var rawObj)) return null;
        long raw = rawObj switch
        {
            int  i => i,
            long l => l,
            _      => 0L
        };
        if (raw <= 0) return null;

        var points = raw / 10_000;
        return MakeMessage($"You gained +{points:N0} Challenge Points.", CombatEventChannelId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ChatMessage MakeMessage(string text, int channelId)
    {
        var tabs = ChannelResolver?.Invoke(channelId) ?? (IReadOnlyList<string>)[];
        return new ChatMessage(DateTime.Now, tabs, "System", string.Empty, text);
    }

    private ChatMessage MakeKillMessage(string text, int channelId)
    {
        var tabs = ChannelResolver?.Invoke(channelId) ?? (IReadOnlyList<string>)[];
        return new ChatMessage(DateTime.Now, tabs, "Kill", string.Empty, text);
    }
}
