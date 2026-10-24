using System.Net.Http;

namespace Launchpad;

/// <summary>
/// Checks stand.sh for the latest Stand DLL version and downloads it into
/// %APPDATA%\Stand\Bin\ — mirroring the senpai branch behaviour, async.
/// </summary>
internal sealed class StandUpdater
{
    // Keep the same version string as senpai so the server knows our age.
    public const string LaunchpadVersion = "1.9.3";

    private static readonly HttpClient Http = new();

    public string StandDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand");

    public string BinDir => Path.Combine(StandDir, "Bin");

    /// <summary>Path to the downloaded Stand DLL, or null if not yet resolved.</summary>
    public string? StandDllPath { get; private set; }

    /// <summary>Stand version string (e.g. "105.4"), set after a successful check.</summary>
    public string? StandVersion { get; private set; }

    /// <summary>Latest Launchpad version string returned by the server.</summary>
    public string? ServerLaunchpadVersion { get; private set; }

    public StandUpdater()
    {
        Directory.CreateDirectory(BinDir);
    }

    /// <summary>
    /// Checks versions.txt, downloads the DLL if needed.
    /// Returns (needsLaunchpadUpdate, error).  error is null on success.
    /// progress reports 0-100 during download.
    /// </summary>
    public async Task<(bool needsLaunchpadUpdate, string? error)> CheckAndDownloadAsync(
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        string versionsText;
        try
        {
            using var resp = await Http.GetAsync("https://stand.sh/versions.txt", ct);
            resp.EnsureSuccessStatusCode();
            versionsText = await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            // Offline — try to reuse an existing DLL in the Bin folder.
            var existing = FindExistingDll();
            if (existing != null)
            {
                StandDllPath = existing.FullName;
                StandVersion = GetVersionFromDll(existing);
                return (false, null);
            }
            return (false, "Failed to check for updates. Ensure you are connected to the internet.");
        }

        var parts = versionsText.Trim().Split(':');
        if (parts.Length < 2)
            return (false, "Unexpected response from stand.sh — please try again.");

        ServerLaunchpadVersion = parts[0];
        StandVersion           = parts[1];

        string dllPath = Path.Combine(BinDir, $"Stand {StandVersion}.dll");
        StandDllPath   = dllPath;

        if (!File.Exists(dllPath))
        {
            var err = await DownloadDllAsync(dllPath, progress, ct);
            if (err != null) return (false, err);

            // Delete old Stand DLL versions.
            foreach (var file in new DirectoryInfo(BinDir).GetFiles())
            {
                if (IsStandDll(file) && GetVersionFromDll(file) != StandVersion)
                    try { file.Delete(); } catch { }
            }
        }

        bool needsUpdate = ServerLaunchpadVersion != LaunchpadVersion;
        return (needsUpdate, null);
    }

    private async Task<string?> DownloadDllAsync(string destPath, IProgress<int>? progress,
                                                  CancellationToken ct)
    {
        string tmpPath = destPath + ".tmp";
        try
        {
            string url = $"https://stand.sh/Stand%20{Uri.EscapeDataString(StandVersion!)}.dll";
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            using var src  = await response.Content.ReadAsStreamAsync();
            using var dest = File.Create(tmpPath);

            var buf        = new byte[16384];
            long downloaded = 0;
            int  read;
            while ((read = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                await dest.WriteAsync(buf, 0, read, ct);
                downloaded += read;
                if (total > 0)
                    progress?.Report((int)(downloaded * 100 / total.Value));
            }
        }
        catch (OperationCanceledException) { return "Download cancelled."; }
        catch (Exception ex)
        {
            try { File.Delete(tmpPath); } catch { }
            return $"Download failed: {ex.Message}";
        }

        if (new FileInfo(tmpPath).Length < 1024)
        {
            try { File.Delete(tmpPath); } catch { }
            return "Stand DLL download appears to have failed (file too small). " +
                   "Your antivirus may be interfering — add an exclusion and try again.";
        }

        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(tmpPath, destPath);
        return null;
    }

    private FileInfo? FindExistingDll()
        => new DirectoryInfo(BinDir).GetFiles()
                                    .FirstOrDefault(IsStandDll);

    private static bool IsStandDll(FileInfo f)
        => f.Name.StartsWith("Stand ", StringComparison.Ordinal) &&
           f.Name.EndsWith(".dll",    StringComparison.OrdinalIgnoreCase);

    private static string GetVersionFromDll(FileInfo f)
        => f.Name.Substring(6, f.Name.Length - 10); // "Stand X.Y.Z.dll" → "X.Y.Z"

    /// <summary>Open the Stand data folder in Explorer.</summary>
    public void OpenStandFolder()
    {
        Directory.CreateDirectory(StandDir);
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(StandDir) { UseShellExecute = true });
    }
}
