using Velopack;
using Velopack.Sources;

namespace AlbionChatTranslator;

/// <summary>
/// Silent auto-update from GitHub Releases (Velopack). Velopack verifies each package's
/// SHA-256 against the release feed before applying it. Does nothing when running from
/// a dev build that was not installed through Setup.exe.
/// </summary>
public sealed class Updater
{
    public const string RepositoryUrl = "https://github.com/MrYellowHeart/AlbionChatTranslator";

    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    /// <summary>Downloads a newer release in the background. Returns its version, or null.</summary>
    public async Task<string?> DownloadUpdateIfAvailableAsync()
    {
        try
        {
            _manager ??= new UpdateManager(new GithubSource(RepositoryUrl, null, false));
            if (!_manager.IsInstalled) return null;

            var info = await _manager.CheckForUpdatesAsync();
            if (info is null) return null;

            await _manager.DownloadUpdatesAsync(info);
            _pending = info;
            return info.TargetFullRelease.Version.ToString();
        }
        catch (Exception e)
        {
            // Offline, rate-limited by GitHub, etc. Never bother the user about it.
            System.Diagnostics.Debug.WriteLine($"[Updater] {e.Message}");
            return null;
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _pending is not null)
            _manager.ApplyUpdatesAndRestart(_pending);
    }
}
