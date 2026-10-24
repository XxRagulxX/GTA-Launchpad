namespace Launchpad;

/// <summary>
/// Synchronous step-by-step log written to %TEMP%\LaunchpadCrash.txt.
/// Each Write call flushes immediately so even a kernel-level kill leaves evidence.
/// </summary>
internal static class CrashLog
{
    private static readonly string Path =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LaunchpadCrash.txt");

    private static readonly object Lock = new();

    public static void Clear()
    {
        lock (Lock)
            try { File.WriteAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] Launchpad started\n"); }
            catch { }
    }

    public static void Write(string message)
    {
        lock (Lock)
            try { File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); }
            catch { }
    }

    public static string FilePath => Path;
}
