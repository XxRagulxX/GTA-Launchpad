using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Launchpad;

internal enum GameMode   { Legacy = 0, Enhanced = 1 }
internal enum LauncherId { Steam = 0, EpicGames = 1, RockstarGames = 2 }

[DataContract]
internal sealed class DllEntry
{
    [DataMember] public string Path    { get; set; } = "";
    [DataMember] public bool   Checked { get; set; }
}

[DataContract]
internal sealed class LaunchpadSettings
{
    // Not persisted — always opens in Legacy mode.
    public GameMode Mode { get; set; } = GameMode.Legacy;
    [DataMember] public bool       AutoInject             { get; set; }
    [DataMember] public int        AutoInjectDelaySeconds { get; set; }
    [DataMember] public bool       Advanced               { get; set; }
    [DataMember] public LauncherId LegacyGameLauncher     { get; set; } = LauncherId.Steam;
    [DataMember] public LauncherId EnhancedGameLauncher   { get; set; } = LauncherId.Steam;
    [DataMember] public List<DllEntry> LegacyCustomDlls   { get; set; } = new();
    [DataMember] public List<DllEntry> EnhancedDlls       { get; set; } = new();

    // Last folder the user picked a DLL from — file dialog re-opens here.
    [DataMember(EmitDefaultValue = false)]
    public string? LastDllDirectory { get; set; }

    // Written only when non-null so old migration data is not persisted back.
    [DataMember(EmitDefaultValue = false)]
    public List<DllEntry>? Dlls { get; set; }

    // ── Computed convenience properties (not serialized) ──────────────────────

    [IgnoreDataMember]
    public LauncherId GameLauncher
    {
        get => Mode == GameMode.Legacy ? LegacyGameLauncher : EnhancedGameLauncher;
        set { if (Mode == GameMode.Legacy) LegacyGameLauncher = value; else EnhancedGameLauncher = value; }
    }

    [IgnoreDataMember]
    public List<DllEntry> ActiveDlls
        => Mode == GameMode.Legacy ? LegacyCustomDlls : EnhancedDlls;

    // ── Persistence ───────────────────────────────────────────────────────────

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Launchpad", "settings.json");

    private static readonly DataContractJsonSerializer Serializer =
        new(typeof(LaunchpadSettings));

    public static LaunchpadSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var stream = File.OpenRead(SettingsPath);
                var loaded = (LaunchpadSettings?)Serializer.ReadObject(stream);
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
        Dlls = null;
        var path = SettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        Serializer.WriteObject(stream, this);
    }
}
