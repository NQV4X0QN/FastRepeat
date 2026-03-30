using System.Diagnostics;
using System.Text.Json;

namespace FastRepeat;

/// <summary>
/// Checks GitHub Releases for a newer version and self-updates by downloading
/// the new EXE, writing a batch-file trampoline, and exiting the current process.
/// </summary>
internal static class UpdateManager
{
    public const string CurrentVersion = "1.5.6";

    private const string Owner     = "NQV4X0QN";
    private const string Repo      = "FastRepeat";
    private const string AssetName = "FastRepeat.exe";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", $"FastRepeat/{CurrentVersion}" } },
        Timeout = TimeSpan.FromSeconds(30)
    };

    // ── Version check ──────────────────────────────────────────────────────

    /// <returns>(available, latestVersionString, downloadUrl)</returns>
    public static async Task<(bool Available, string Version, string DownloadUrl)> CheckAsync()
    {
        var url  = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
        var json = await Http.GetStringAsync(url);

        using var doc  = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag       = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "0.0.0";
        var latestVer = new Version(tag);
        var currentVer= new Version(CurrentVersion);

        if (latestVer <= currentVer)
            return (false, tag, string.Empty);

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (name.Equals(AssetName, StringComparison.OrdinalIgnoreCase))
            {
                var dlUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                return (true, tag, dlUrl);
            }
        }

        return (false, tag, string.Empty);
    }

    // ── Download ───────────────────────────────────────────────────────────

    public static async Task DownloadAsync(string url, string savePath, IProgress<int>? progress = null)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var    total      = response.Content.Headers.ContentLength ?? -1L;
        using var stream  = await response.Content.ReadAsStreamAsync();
        using var file    = File.Create(savePath);

        var   buffer     = new byte[81920];
        long  downloaded = 0;
        int   read;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (total > 0)
                progress?.Report((int)(downloaded * 100 / total));
        }
    }

    // ── Apply (batch trampoline) ───────────────────────────────────────────

    /// <summary>
    /// Writes a batch file that waits for this process to exit, moves the new EXE
    /// over the old one, restarts it, then self-deletes.
    /// Always targets the installed location so updates go to the right place.
    /// </summary>
    public static void ApplyUpdate(string currentExe, string newExe)
    {
        // Always update the installed copy
        var targetExe = Program.InstalledExePath;
        var bat = Path.Combine(Path.GetTempPath(), "FastRepeat_update.bat");

        File.WriteAllText(bat, $"""
            @echo off
            ping -n 3 127.0.0.1 > nul
            move /y "{newExe}" "{targetExe}"
            start "" "{targetExe}" --updated
            del "%~f0"
            """);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
        {
            WindowStyle     = ProcessWindowStyle.Hidden,
            CreateNoWindow  = true,
            UseShellExecute = true
        });

        // Force-exit the process. Application.Exit() alone is not enough because
        // MainForm's FormClosing handler cancels the close and the TrayApp keeps
        // the message loop alive. Environment.Exit guarantees process termination
        // so the batch trampoline can replace the EXE.
        Environment.Exit(0);
    }
}
