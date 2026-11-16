using System.Collections;
using System.Text;

namespace AlbionChatTranslator.Core.Chat;

/// <summary>
/// Describes the layout of a game event for the diagnostic log: parameter keys, value types and
/// numbers. Game keys such as "@CHAT_CHANNEL_HELP" are shown; any other text (player names,
/// messages, guild names) is replaced by its length, so nothing personal is written.
/// </summary>
public static class EventShape
{
    private const int MaxItems = 16;
    private const int MaxDepth = 3;

    public static string Describe(IReadOnlyDictionary<byte, object> parameters, params byte[] hidden)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in parameters.OrderBy(kv => kv.Key))
        {
            if (key == 252) continue; // Photon's own event-code parameter
            if (sb.Length > 0) sb.Append(' ');
            sb.Append('[').Append(key).Append("]=");
            if (hidden.Contains(key))
                sb.Append(value is string s ? $"text({s.Length})" : "hidden");
            else
                Append(sb, value, 0);
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, object? value, int depth)
    {
        switch (value)
        {
            case null: sb.Append("null"); break;
            case string s:
                if (s.StartsWith('@') && s.Length <= 80 && !s.Contains(' ')) sb.Append('"').Append(s).Append('"');
                else sb.Append("text(").Append(s.Length).Append(')');
                break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case byte b: sb.Append("byte ").Append(b); break;
            case sbyte or short or ushort or int or uint or long or ulong:
                sb.Append(ShortName(value.GetType())).Append(' ').Append(value);
                break;
            case float or double: sb.Append(ShortName(value.GetType())).Append(' ').Append(value); break;
            case IDictionary d:
                sb.Append("dict(").Append(d.Count).Append(')');
                if (depth < MaxDepth)
                {
                    sb.Append('{');
                    int n = 0;
                    foreach (DictionaryEntry e in d)
                    {
                        if (n++ >= MaxItems) { sb.Append(" …"); break; }
                        if (n > 1) sb.Append(", ");
                        Append(sb, e.Key, depth + 1);
                        sb.Append(':');
                        Append(sb, e.Value, depth + 1);
                    }
                    sb.Append('}');
                }
                break;
            case IEnumerable e:
                var items = e.Cast<object?>().ToList();
                sb.Append(ShortName(value.GetType())).Append('(').Append(items.Count).Append(')');
                if (depth < MaxDepth)
                {
                    sb.Append('{');
                    for (int i = 0; i < Math.Min(items.Count, MaxItems); i++)
                    {
                        if (i > 0) sb.Append(", ");
                        Append(sb, items[i], depth + 1);
                    }
                    if (items.Count > MaxItems) sb.Append(", …");
                    sb.Append('}');
                }
                break;
            default: sb.Append(ShortName(value.GetType())); break;
        }
    }

    private static string ShortName(Type t) => t.IsArray ? ShortName(t.GetElementType()!) + "[]" : t.Name switch
    {
        "Byte" => "byte", "SByte" => "sbyte", "Int16" => "short", "UInt16" => "ushort", "Int32" => "int",
        "UInt32" => "uint", "Int64" => "long", "UInt64" => "ulong", "Single" => "float", "Double" => "double",
        "String" => "string", "Object" => "object", "Boolean" => "bool",
        var n => n,
    };
}
