using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launchpad;

[JsonSerializable(typeof(LaunchpadSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext { }

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
    // Top-level mode selector
    public GameMode Mode { get; set; } = GameMode.Legacy;

    // Shared settings
    public bool AutoInject             { get; set; }
    public int  AutoInjectDelaySeconds { get; set; }
    public bool Advanced               { get; set; }

    // Per-mode launcher preference
    public LauncherId LegacyGameLauncher   { get; set; } = LauncherId.Steam;
    public LauncherId EnhancedGameLauncher { get; set; } = LauncherId.Steam;

    // Legacy: only user-added custom DLLs; the Stand DLL itself is auto-managed
    public List<DllEntry> LegacyCustomDlls { get; set; } = new();

    // Enhanced: all user DLLs
    public List<DllEntry> EnhancedDlls { get; set; } = new();

    // Migration: old single "Dlls" list (Enhanced branch) -> EnhancedDlls
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DllEntry>? Dlls { get; set; }

    // Convenience: active launcher for the current mode
    [JsonIgnore]
    public LauncherId GameLauncher
    {
        get => Mode == GameMode.Legacy ? LegacyGameLauncher : EnhancedGameLauncher;
        set { if (Mode == GameMode.Legacy) LegacyGameLauncher = value; else EnhancedGameLauncher = value; }
    }

    // Convenience: active DLL list for the current mode
    [JsonIgnore]
    public List<DllEntry> ActiveDlls
        => Mode == GameMode.Legacy ? LegacyCustomDlls : EnhancedDlls;

    // ── Persistence ──────────────────────────────────────────────────────────

    private static string SettingsPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Launchpad", "settings.json");

    public static LaunchpadSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var text   = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize(text, AppJsonContext.Default.LaunchpadSettings);
                if (loaded != null)
                {
                    // Migrate old "Dlls" field -> EnhancedDlls
                    if (loaded.Dlls is { Count: > 0 } && loaded.EnhancedDlls.Count == 0)
                    {
                        loaded.EnhancedDlls = loaded.Dlls;
                        loaded.Dlls = null;
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
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, AppJsonContext.Default.LaunchpadSettings));
    }
}
