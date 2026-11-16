namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Remembers the last few replies the reply box translated (in memory only, never on disk), so
/// that when the player sends one, the app can show the exact words they typed instead of a
/// machine translation of a machine translation.
/// </summary>
public sealed class ReplyMemory(int capacity = 50)
{
    public static ReplyMemory Shared { get; } = new();

    private readonly Lock _lock = new();
    private readonly LinkedList<(string Key, string Typed, string Language)> _items = new();

    public void Remember(string translated, string typed, string language)
    {
        string key = Normalize(translated);
        if (key.Length == 0) return;
        lock (_lock)
        {
            for (var n = _items.First; n is not null; n = n.Next)
                if (n.Value.Key == key) { _items.Remove(n); break; }
            _items.AddFirst((key, typed.Trim(), language));
            while (_items.Count > capacity) _items.RemoveLast();
        }
    }

    public bool TryRecall(string sentText, out string typed, out string language)
    {
        string key = Normalize(sentText);
        lock (_lock)
        {
            foreach (var item in _items)
            {
                if (item.Key != key) continue;
                (typed, language) = (item.Typed, item.Language);
                return true;
            }
        }
        typed = language = string.Empty;
        return false;
    }

    private static string Normalize(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
