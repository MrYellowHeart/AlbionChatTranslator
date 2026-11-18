using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Downloads language packs on demand and keeps them under %LOCALAPPDATA%.
/// Every file is checked against the SHA-256 pinned in the embedded catalog before it
/// is used, and only ever fetched over HTTPS from huggingface.co at a fixed commit.
/// </summary>
public sealed class ModelStore
{
    private const string Host = "huggingface.co";
    private const string ReadyMarker = ".verified";

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public ModelStore(string rootDirectory, HttpClient? http = null)
    {
        RootDirectory = rootDirectory;
        _http = http ?? CreateDefaultClient();
    }

    public static string DefaultRoot => Path.Combine(Settings.AppSettings.LocalDataDirectory, "models");

    public string RootDirectory { get; }

    /// <summary>Raised as bytes arrive: (pack id, bytes so far, total bytes).</summary>
    public event Action<string, long, long>? Progress;

    public string PackDirectory(ModelPack pack) => Path.Combine(RootDirectory, pack.Id, pack.Revision[..12]);

    public bool IsInstalled(ModelPack pack) => File.Exists(Path.Combine(PackDirectory(pack), ReadyMarker));

    public long InstalledBytes()
    {
        if (!Directory.Exists(RootDirectory)) return 0;
        return new DirectoryInfo(RootDirectory).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    public void DeleteAll()
    {
        if (Directory.Exists(RootDirectory))
            Directory.Delete(RootDirectory, recursive: true);
    }

    /// <summary>
    /// Makes sure the pack is on disk and verified. Concurrent callers share one download.
    /// <paramref name="ct"/> only stops this caller waiting: the shared download keeps going, so
    /// cancelling a background pre-download can't fail a translation waiting on the same pack.
    /// </summary>
    public Task<string> EnsureInstalledAsync(ModelPack pack, CancellationToken ct = default)
    {
        if (IsInstalled(pack)) return Task.FromResult(PackDirectory(pack));

        var lazy = _inFlight.GetOrAdd(pack.Id, _ => new Lazy<Task<string>>(() => DownloadAndForgetAsync(pack)));
        return lazy.Value.WaitAsync(ct);
    }

    private async Task<string> DownloadAndForgetAsync(ModelPack pack)
    {
        try { return await DownloadAsync(pack, CancellationToken.None).ConfigureAwait(false); }
        finally { _inFlight.TryRemove(pack.Id, out _); }
    }

    private async Task<string> DownloadAsync(ModelPack pack, CancellationToken ct)
    {
        var dir = PackDirectory(pack);
        Directory.CreateDirectory(dir);
        long total = pack.TotalSize, done = 0;

        foreach (var file in pack.Files)
        {
            var target = SafeCombine(dir, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target) && await HashMatchesAsync(target, file.Sha256, ct).ConfigureAwait(false))
            {
                done += file.Size;
                Progress?.Invoke(pack.Id, done, total);
                continue;
            }

            var uri = new Uri($"https://{Host}/{pack.Repo}/resolve/{pack.Revision}/{file.Path}");
            var temp = target + ".part";
            using (var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var output = File.Create(temp);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1 << 16];
                long written = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    written += read;
                    if (written > file.Size) throw new InvalidDataException($"{file.Path} is larger than expected.");
                    sha.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;
                    Progress?.Invoke(pack.Id, done, total);
                }

                var actual = Convert.ToHexStringLower(sha.GetHashAndReset());
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    output.Close();
                    File.Delete(temp);
                    throw new InvalidDataException($"{pack.Id}/{file.Path} failed its integrity check and was discarded.");
                }
            }
            File.Move(temp, target, overwrite: true);
        }

        await File.WriteAllTextAsync(Path.Combine(dir, ReadyMarker), pack.Revision, ct).ConfigureAwait(false);
        return dir;
    }

    private static async Task<bool> HashMatchesAsync(string path, string expected, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return string.Equals(Convert.ToHexStringLower(hash), expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rejects catalog paths that would escape the pack directory.</summary>
    internal static string SafeCombine(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Refusing unsafe model path '{relative}'.");
        return full;
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true, // huggingface.co redirects LFS files to its CDN; the hash check covers it
            MaxAutomaticRedirections = 5,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AlbionChatTranslator/2");
        return client;
    }
}
