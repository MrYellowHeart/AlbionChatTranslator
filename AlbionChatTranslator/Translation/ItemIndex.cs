using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace AlbionChatTranslator.Translation;

/// <summary>
/// Decrypts and parses the game's items.bin to produce an index → unique-name lookup.
/// Index values match the ItemIndex parameter in EventCode 256 (OtherGrabbedLoot)
/// and the ItemId parameter in EventCode 54 (HarvestFinished).
/// </summary>
public static class ItemIndex
{
    private static readonly Dictionary<int, string> _index = new();

    private static readonly string[] _searchPaths =
    [
        @"C:\Program Files (x86)\AlbionOnline\game\Albion-Online_Data\StreamingAssets\GameData\items.bin",
        @"C:\Program Files\AlbionOnline\game\Albion-Online_Data\StreamingAssets\GameData\items.bin",
    ];

    public static bool IsLoaded { get; private set; }

    public static string? GetUniqueName(int index) =>
        _index.TryGetValue(index, out var name) ? name : null;

    /// <summary>
    /// Loads item data in the background. Safe to call without awaiting.
    /// </summary>
    public static Task LoadAsync() => Task.Run(async () =>
    {
        var path = _searchPaths.FirstOrDefault(File.Exists);
        if (path == null) return;

        try
        {
            var xml = await DecryptAndDecompressAsync(path);
            BuildIndex(xml);
            IsLoaded = true;
        }
        catch { }
    });

    private static void BuildIndex(string xml)
    {
        // Strip control characters that would break XML parsing
        var clean = new string(xml.Where(c => !char.IsControl(c) || char.IsWhiteSpace(c)).ToArray());

        var doc = new XmlDocument();
        doc.LoadXml(clean);

        var index = 1;
        var journals = new List<string>();

        if (doc.LastChild?.ChildNodes is not { } childNodes) return;

        foreach (XmlNode node in childNodes)
        {
            if (node.NodeType != XmlNodeType.Element ||
                string.IsNullOrEmpty(node.Attributes?["uniquename"]?.Value))
                continue;

            var uniqueName  = node.Attributes["uniquename"]!.Value;
            var enchLevel   = node.Attributes["enchantmentlevel"]?.Value;
            var suffix      = enchLevel is not null and not "0" ? "@" + enchLevel : string.Empty;

            _index[index++] = uniqueName + suffix;

            // Enchantment variants
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child is not XmlElement { Name: "enchantments" } enchantmentsEl) continue;
                foreach (XmlElement enchNode in enchantmentsEl.ChildNodes)
                {
                    var lvl = enchNode.Attributes["enchantmentlevel"]?.Value;
                    _index[index++] = uniqueName + "@" + lvl;
                }
            }

            if (node.Name == "journalitem")
                journals.Add(uniqueName);
        }

        // Journal empty/full variants (appended at end, matching extractor behaviour)
        foreach (var j in journals)
        {
            _index[index++] = j + "_EMPTY";
            _index[index++] = j + "_FULL";
        }
    }

    private static async Task<string> DecryptAndDecompressAsync(string path)
    {
        // Key and IV from the original StatisticAnalysisTool.Extractor project
        byte[] key = [48, 239, 114, 71, 66, 242, 4, 50];
        byte[] iv  = [14, 166, 220, 137, 219, 237, 220, 79];

        var fileBytes = await File.ReadAllBytesAsync(path);

        using var des = DES.Create();
        des.Key  = key;
        des.IV   = iv;
        des.Mode = CipherMode.CBC;

        var decrypted = des.CreateDecryptor().TransformFinalBlock(fileBytes, 0, fileBytes.Length);

        using var ms     = new MemoryStream(decrypted);
        using var gz     = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gz, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
