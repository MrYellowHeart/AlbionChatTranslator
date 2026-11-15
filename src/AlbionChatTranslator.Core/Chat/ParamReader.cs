using System.Collections;
using System.Globalization;

namespace AlbionChatTranslator.Core.Chat;

/// <summary>
/// Tolerant readers for Photon event parameters. Protocol 18 encodes integers
/// compactly, so the same field can arrive as byte, short, int or long depending
/// on its value. Never trust the exact CLR type.
/// </summary>
internal static class ParamReader
{
    public static bool TryGetString(IReadOnlyDictionary<byte, object> p, byte key, out string value)
    {
        if (p.TryGetValue(key, out var o) && o is string s)
        {
            value = s;
            return true;
        }
        value = string.Empty;
        return false;
    }

    public static bool TryGetInt(IReadOnlyDictionary<byte, object> p, byte key, out int value)
    {
        value = 0;
        return p.TryGetValue(key, out var o) && TryToInt(o, out value);
    }

    public static bool TryToInt(object? o, out int value)
    {
        switch (o)
        {
            case byte b: value = b; return true;
            case sbyte sb: value = sb; return true;
            case short s: value = s; return true;
            case ushort us: value = us; return true;
            case int i: value = i; return true;
            case uint ui when ui <= int.MaxValue: value = (int)ui; return true;
            case long l when l is >= int.MinValue and <= int.MaxValue: value = (int)l; return true;
            case IConvertible c and not string and not bool:
                try
                {
                    value = Convert.ToInt32(c, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                {
                    break;
                }
        }
        value = 0;
        return false;
    }

    /// <summary>Enumerates any array-like parameter (object[], string[], int[], ...). Strings are not sequences here.</summary>
    public static IEnumerable<object?> AsSequence(object? o) => o switch
    {
        null or string => [],
        IEnumerable e => e.Cast<object?>(),
        _ => [],
    };

    public static List<int> ToIntList(object? o)
    {
        var result = new List<int>();
        foreach (var item in AsSequence(o))
        {
            if (TryToInt(item, out int v))
                result.Add(v);
        }
        return result;
    }
}
