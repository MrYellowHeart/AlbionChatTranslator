using AlbionChatTranslator.Core.Translation;

namespace AlbionChatTranslator.Core.Settings;

/// <summary>
/// Chat languages pre-selected for a game server. Only where the answer is known: the Americas
/// server's main languages are English, Spanish and Portuguese. There is no published data for
/// Europe or Asia, so nothing is pre-selected there and the user picks.
/// </summary>
public static class ServerLanguages
{
    public static IReadOnlyList<string> Preset(GameServer server, string reader) => server switch
    {
        GameServer.Americas => new[] { "en", "es", "pt" }.Where(c => c != Languages.Normalize(reader)).ToList(),
        _ => [],
    };
}
