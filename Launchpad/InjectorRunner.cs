namespace Launchpad;

/// <summary>
/// Bridges the UI to the injection and game-watch logic.
/// Supports both Legacy (GTA5.exe) and Enhanced (GTA5_Enhanced.exe) modes.
/// </summary>
internal sealed class InjectorRunner
{
    public event Action<int>? GameStarted;
    public event Action?      GameStopped;

    private GameMode                  _mode;
    private CancellationTokenSource?  _watchCts;

    public InjectorRunner(GameMode mode)
    {
        _mode = mode;
    }

    /// <summary>Switch the watched process without restarting the watcher loop.</summary>
    public void SetMode(GameMode mode) => _mode = mode;

    public void StartWatching()
    {
        _watchCts = new CancellationTokenSource();
        var token = _watchCts.Token;

        Task.Run(async () =>
        {
            int lastPid = 0;
            while (!token.IsCancellationRequested)
            {
                int pid = GameProcess.FindPid(_mode);
                if (pid != lastPid)
                {
                    if (pid != 0) GameStarted?.Invoke(pid);
                    else          GameStopped?.Invoke();
                    lastPid = pid;
                }
                try   { await Task.Delay(1000, token); }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    public void StopWatching()
    {
        _watchCts?.Cancel();
        _watchCts = null;
    }

    public async Task<int> InjectAsync(int pid, IReadOnlyList<string> dllPaths, Action<string> log)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LaunchpadInjector");
        return await Task.Run(() => DllInjector.InjectAll(pid, dllPaths, tempDir, log));
    }

    public async Task<string?> LaunchAsync(GameMode mode, LauncherId launcher)
    {
        return await Task.Run(() =>
        {
            StorefrontLauncher.TryLaunch(mode, launcher, out var error);
            return error;
        });
    }
}
