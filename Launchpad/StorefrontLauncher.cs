using System.Diagnostics;
using Microsoft.Win32;

namespace Launchpad;

internal static class StorefrontLauncher
{
    public static bool TryLaunch(GameMode mode, LauncherId launcher, out string? error)
    {
        return mode == GameMode.Legacy
            ? TryLaunchLegacy(launcher, out error)
            : TryLaunchEnhanced(launcher, out error);
    }

    // ── Legacy (GTA V) ───────────────────────────────────────────────────────

    private static bool TryLaunchLegacy(LauncherId launcher, out string? error)
    {
        switch (launcher)
        {
            case LauncherId.Steam:
                return TryOpenUrl("steam://run/271590", out error);

            case LauncherId.EpicGames:
                return TryOpenUrl(
                    "com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true",
                    out error);

            case LauncherId.RockstarGames:
                return TryLaunchRockstarLegacy(out error);

            default:
                error = $"Unknown launcher '{launcher}'.";
                return false;
        }
    }

    private static bool TryLaunchRockstarLegacy(out string? error)
    {
        try
        {
            string? installPath = null;
            using (var key = Registry.LocalMachine.OpenSubKey(
                       @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V"))
            {
                installPath = key?.GetValue("InstallFolder") as string;
            }

            if (installPath != null)
            {
                var exe = Path.Combine(installPath, "PlayGTAV.exe");
                if (File.Exists(exe)) { Process.Start(exe); error = null; return true; }
            }

            // Common fallback locations
            foreach (var root in new[] {
                         @"C:\Program Files\Rockstar Games",
                         @"C:\Program Files (x86)\Rockstar Games" })
            {
                var exe = Path.Combine(root, "Grand Theft Auto V", "PlayGTAV.exe");
                if (!File.Exists(exe)) continue;
                Process.Start(exe);
                error = null;
                return true;
            }

            error = "Couldn't find GTA V. Start it from the Rockstar Games Launcher — " +
                    "Launchpad will inject automatically when GTA5.exe starts.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to launch via Rockstar Games: {ex.Message}";
            return false;
        }
    }

    // ── Enhanced (GTA V Enhanced) ────────────────────────────────────────────

    private static bool TryLaunchEnhanced(LauncherId launcher, out string? error)
    {
        switch (launcher)
        {
            case LauncherId.Steam:
                return TryOpenUrl("steam://run/271590", out error);

            case LauncherId.EpicGames:
                return TryOpenUrl(
                    "com.epicgames.launcher://apps/9d2d0eb64d5c44529cece33fe2a46482?action=launch&silent=true",
                    out error);

            case LauncherId.RockstarGames:
                return TryLaunchRockstarEnhanced(out error);

            default:
                error = $"Unknown launcher '{launcher}'.";
                return false;
        }
    }

    private static bool TryLaunchRockstarEnhanced(out string? error)
    {
        try
        {
            string? installPath = null;
            using (var key = Registry.LocalMachine.OpenSubKey(
                       @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV Enhanced"))
            {
                installPath = key?.GetValue("InstallFolder") as string;
            }

            if (installPath != null)
            {
                foreach (var exe in new[] { "PlayGTAV.exe", "GTA5_Enhanced.exe" })
                {
                    var full = Path.Combine(installPath, exe);
                    if (!File.Exists(full)) continue;
                    Process.Start(full);
                    error = null;
                    return true;
                }
            }

            // Common fallback locations
            foreach (var root in new[] {
                         @"C:\Program Files\Rockstar Games",
                         @"C:\Program Files (x86)\Rockstar Games" })
            {
                foreach (var dir in new[] { "Grand Theft Auto V Enhanced", "Grand Theft Auto V" })
                {
                    foreach (var exe in new[] { "PlayGTAV.exe", "GTA5_Enhanced.exe" })
                    {
                        var full = Path.Combine(root, dir, exe);
                        if (!File.Exists(full)) continue;
                        Process.Start(full);
                        error = null;
                        return true;
                    }
                }
            }

            error = "Couldn't find GTA V Enhanced. Start it from the Rockstar Games Launcher — " +
                    "Launchpad will inject automatically when GTA5_Enhanced.exe starts.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to launch via Rockstar Games: {ex.Message}";
            return false;
        }
    }

    // ── Shared ───────────────────────────────────────────────────────────────

    private static bool TryOpenUrl(string url, out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
