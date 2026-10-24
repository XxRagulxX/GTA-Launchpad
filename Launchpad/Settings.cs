using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launchpad;

internal enum GameMode
{
    Legacy   = 0,
    Enhanced = 1,
}

internal enum LauncherId
{
    Steam         = 0,
    EpicGames     = 1,
    RockstarGames = 2,
}

internal sealed class DllEntry
{
    public string Path    { get; set; } = "";
    public bool   Checked { get; set; }
}

internal sealed class LaunchpadSettings
{
    // Active game mode
    public GameMode Mode { get; set; } = GameMode.Legacy;

    // Shared settings
    public bool AutoInject             { get; set; }
    public int  AutoInjectDelaySeconds { get; set; }
    public bool Advanced               { get; set; }

    // Per-mode launcher preference
    public LauncherId LegacyGameLauncher   { get; set; } = LauncherId.Steam;
    public LauncherId EnhancedGameLauncher { get; set; } = LauncherId.Steam;

    // Legacy: user-added custom DLLs only (Stand DLL is auto-managed)
    public List<DllEntry> LegacyCustomDlls { get; set; } = new();

    // Enhanced: all user DLLs
    public List<DllEntry> EnhancedDlls { get; set; } = new();

    // Migration: old single "Dlls" list from the previous Enhanced-only version
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DllEntry>? Dlls { get; set; }

    // ── Computed convenience properties (not serialized) ──────────────────────

    [JsonIgnore]
    public LauncherId GameLauncher
    {
        get => Mode == GameMode.Legacy ? LegacyGameLauncher : EnhancedGameLauncher;
        set { if (Mode == GameMode.Legacy) LegacyGameLauncher = value; else EnhancedGameLauncher = value; }
    }

    [JsonIgnore]
    public List<DllEntry> ActiveDlls
        => Mode == GameMode.Legacy ? LegacyCustomDlls : EnhancedDlls;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Launchpad", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented       = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LaunchpadSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<LaunchpadSettings>(
                    File.ReadAllText(SettingsPath), JsonOptions);

                if (loaded != null)
                {
                    // Migrate old single "Dlls" field → EnhancedDlls
                    if (loaded.Dlls is { Count: > 0 } && loaded.EnhancedDlls.Count == 0)
                    {
                        loaded.EnhancedDlls = loaded.Dlls;
                        loaded.Dlls         = null;
                    }
                    return loaded;
                }
            }
        }
        catch { }
        return new LaunchpadSettings();
    }

    public void Save()
    {
        Dlls = null; // never write old field
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
