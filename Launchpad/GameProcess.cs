using System.Diagnostics;

namespace Launchpad;

internal static class GameProcess
{
    public static string ProcessName(GameMode mode) =>
        mode == GameMode.Legacy ? "GTA5" : "GTA5_Enhanced";

    public static int FindPid(GameMode mode)
    {
        foreach (var process in Process.GetProcessesByName(ProcessName(mode)))
        {
            using (process)
                return process.Id;
        }
        return 0;
    }
}
