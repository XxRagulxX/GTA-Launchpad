using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Launchpad;

public partial class MainWindow : Window
{
    private LaunchpadSettings              _settings;
    private readonly InjectorRunner        _injector;
    private readonly StandUpdater          _standUpdater;
    private ObservableCollection<DllEntry> _dlls;
    private readonly DispatcherTimer       _autoInjectTimer;

    private int  _gtaPid;
    private bool _autoInjectArmed = true;

    // The Stand DLL entry that is pinned as first item in Legacy mode.
    // null when Enhanced mode or before the updater resolves the path.
    private DllEntry? _standDllEntry;

    public MainWindow()
    {
        InitializeComponent();

        _settings     = LaunchpadSettings.Load();
        _standUpdater = new StandUpdater();

        _dlls           = new ObservableCollection<DllEntry>();
        DllList.ItemsSource = _dlls;

        var launchers = GameLauncher.AvailableLaunchers;
        LauncherType.ItemsSource  = launchers;

        AutoInjectCheckBox.IsChecked   = _settings.AutoInject;
        AutoInjectDelaySeconds.Value   = _settings.AutoInjectDelaySeconds;
        AdvancedPanel.IsVisible        = _settings.Advanced;

        // Restore mode radio button
        if (_settings.Mode == GameMode.Enhanced)
            EnhancedModeBtn.IsChecked = true;
        else
            LegacyModeBtn.IsChecked = true;

        _autoInjectTimer      = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _autoInjectTimer.Tick += AutoInjectTimer_Tick;

        _injector              = new InjectorRunner(_settings.Mode);
        _injector.GameStarted += OnGameStarted;
        _injector.GameStopped += OnGameStopped;
        _injector.StartWatching();

        // Apply initial mode (launchers, DLL list, folder button label, etc.)
        ApplyMode(_settings.Mode, initialLoad: true);

        // For Legacy mode, kick off the Stand DLL check now.
        if (_settings.Mode == GameMode.Legacy)
            _ = RunStandUpdateAsync();

        Closing += (_, _) => { _injector.StopWatching(); SaveSettings(); };
    }

    // ── Mode switching ────────────────────────────────────────────────────────

    private void ModeBtn_Checked(object? sender, RoutedEventArgs e)
    {
        var newMode = (EnhancedModeBtn.IsChecked == true) ? GameMode.Enhanced : GameMode.Legacy;
        if (newMode == _settings.Mode) return;

        SaveSettings();
        _settings.Mode = newMode;
        _settings.Save();

        _injector.SetMode(newMode);
        _gtaPid          = 0;
        _autoInjectArmed = true;
        _autoInjectTimer.Stop();

        ApplyMode(newMode, initialLoad: false);

        if (newMode == GameMode.Legacy)
            _ = RunStandUpdateAsync();
        else
            InfoText.Text = "Ready to inject; just start the game.";
    }

    private void ApplyMode(GameMode mode, bool initialLoad)
    {
        bool isLegacy = mode == GameMode.Legacy;

        // Folder button label
        OpenFolderBtn.Content = isLegacy ? "Open Stand Folder" : "Open StandEnhanced Folder";

        // Check-for-Updates button is only useful in Legacy mode (auto-manages the DLL)
        CheckUpdateBtn.IsVisible = isLegacy;

        // Restore launcher selection for this mode
        var launchers = GameLauncher.AvailableLaunchers;
        LauncherType.ItemsSource  = launchers;
        LauncherType.SelectedItem = launchers.FirstOrDefault(l => l.Id == _settings.GameLauncher)
                                    ?? launchers[0];

        // Rebuild DLL list for the new mode
        _dlls.Clear();
        _standDllEntry = null;

        if (isLegacy)
        {
            // Pinned Stand DLL entry — path will be set once the updater resolves it.
            _standDllEntry = new DllEntry { Path = "Stand (checking for updates...)", Checked = true };
            _dlls.Add(_standDllEntry);
        }

        foreach (var entry in _settings.ActiveDlls)
            _dlls.Add(entry);

        // Reset inject/launch visibility to launch (no pid yet after mode change)
        ToggleInjectOrLaunch(gameRunning: false);
    }

    // ── Stand DLL update (Legacy) ────────────────────────────────────────────

    private async Task RunStandUpdateAsync()
    {
        InfoText.Text = "Checking for updates...";

        var progress = new Progress<int>(pct =>
        {
            DownloadProgress.IsVisible = true;
            DownloadProgress.Value     = pct;
        });

        var (needsLaunchpadUpdate, error) = await _standUpdater.CheckAndDownloadAsync(progress);

        DownloadProgress.IsVisible = false;

        if (error != null)
        {
            InfoText.Text = error;
        }
        else
        {
            // Update the pinned Stand DLL entry with the real path.
            if (_standDllEntry != null && _standUpdater.StandDllPath != null)
            {
                _standDllEntry.Path = $"Stand {_standUpdater.StandVersion}";
                // Trigger UI refresh for the DllEntry binding
                var idx = _dlls.IndexOf(_standDllEntry);
                if (idx >= 0) { _dlls.RemoveAt(idx); _dlls.Insert(idx, _standDllEntry); }
            }

            InfoText.Text = "Ready to inject; just start the game.";

            if (needsLaunchpadUpdate && _standUpdater.ServerLaunchpadVersion != null)
            {
                var msg = $"Launchpad {_standUpdater.ServerLaunchpadVersion} is available. " +
                          "Visit stand.sh to download it.";
                InfoText.Text = msg;
            }
        }
    }

    // ── Game state ────────────────────────────────────────────────────────────

    private void OnGameStarted(int pid)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gtaPid = pid;
            ToggleInjectOrLaunch(gameRunning: true);

            if (AutoInjectCheckBox.IsChecked == true && _autoInjectArmed)
            {
                var delay = (int)(AutoInjectDelaySeconds.Value ?? 0);
                if (_settings.Advanced && delay > 0)
                {
                    InfoText.Text             = "Automatically injecting in a few seconds...";
                    _autoInjectTimer.Interval = TimeSpan.FromSeconds(delay);
                    _autoInjectTimer.Start();
                }
                else
                {
                    _ = InjectAsync();
                }
            }
            else
            {
                InfoText.Text = "Ready to inject.";
            }
        });
    }

    private void OnGameStopped()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _gtaPid = 0;
            _autoInjectTimer.Stop();
            ToggleInjectOrLaunch(gameRunning: false);
            InfoText.Text = "Ready to inject; just start the game.";

            _autoInjectArmed = false;
            DispatcherTimer.RunOnce(() => _autoInjectArmed = true, TimeSpan.FromSeconds(3));
        });
    }

    private void AutoInjectTimer_Tick(object? sender, EventArgs e)
    {
        _autoInjectTimer.Stop();
        _ = InjectAsync();
    }

    private void ToggleInjectOrLaunch(bool gameRunning)
    {
        InjectBtn.IsVisible  = gameRunning;
        LaunchRow.IsVisible  = !gameRunning;
    }

    // ── Inject ────────────────────────────────────────────────────────────────

    private async void InjectBtn_Click(object? sender, RoutedEventArgs e) => await InjectAsync();

    private async Task InjectAsync()
    {
        if (_gtaPid == 0) return;

        _autoInjectTimer.Stop();
        InjectBtn.IsEnabled = false;
        InfoText.Text       = "Injecting...";

        // Collect DLL paths to inject
        List<string> paths;
        if (_settings.Advanced)
        {
            paths = _dlls.Where(d => d.Checked).Select(RealPath).ToList();
        }
        else
        {
            // Simple mode: inject only the Stand DLL (Legacy) or the first checked DLL (Enhanced)
            if (_settings.Mode == GameMode.Legacy)
            {
                paths = _standUpdater.StandDllPath != null
                    ? new List<string> { _standUpdater.StandDllPath }
                    : new List<string>();
            }
            else
            {
                paths = _dlls.Where(d => d.Checked).Select(RealPath).Take(1).ToList();
            }
        }

        int injected = await _injector.InjectAsync(_gtaPid, paths, line => Console.WriteLine(line));
        InfoText.Text       = $"Injected {injected}/{paths.Count} DLLs.";
        InjectBtn.IsEnabled = true;
    }

    // Resolve a display-label entry to its real file path.
    private string RealPath(DllEntry e)
    {
        if (_settings.Mode == GameMode.Legacy && e == _standDllEntry)
            return _standUpdater.StandDllPath ?? e.Path;
        return e.Path;
    }

    // ── DLL list ──────────────────────────────────────────────────────────────

    private async void AddBtn_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title          = "Select a DLL to inject",
            AllowMultiple  = true,
            FileTypeFilter = new[] { new FilePickerFileType("DLL files") { Patterns = new[] { "*.dll" } } },
        });

        foreach (var file in files)
            _dlls.Add(new DllEntry { Path = file.Path.LocalPath, Checked = true });

        SaveSettings();
    }

    private void RemoveBtn_Click(object? sender, RoutedEventArgs e)
    {
        // Cannot remove the pinned Stand DLL entry in Legacy mode
        foreach (var item in DllList.SelectedItems!.Cast<DllEntry>().ToList())
        {
            if (item == _standDllEntry) continue;
            _dlls.Remove(item);
        }
        SaveSettings();
    }

    private void UpBtn_Click(object? sender, RoutedEventArgs e)   => MoveSelected(-1);
    private void DownBtn_Click(object? sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        if (DllList.SelectedItems!.Count != 1) return;
        int index    = _dlls.IndexOf((DllEntry)DllList.SelectedItems[0]!);
        int newIndex = index + direction;
        // Index 0 is the pinned Stand DLL in Legacy mode — don't allow moving past it
        int minIndex = _standDllEntry != null ? 1 : 0;
        if (newIndex < minIndex || newIndex >= _dlls.Count) return;
        _dlls.Move(index, newIndex);
        DllList.SelectedIndex = newIndex;
        SaveSettings();
    }

    // ── Delay ─────────────────────────────────────────────────────────────────

    private void DelayUpBtn_Click(object? sender, RoutedEventArgs e)
    {
        if ((AutoInjectDelaySeconds.Value ?? 0) < 60)
            AutoInjectDelaySeconds.Value = (AutoInjectDelaySeconds.Value ?? 0) + 1;
        SaveSettings();
    }

    private void DelayDownBtn_Click(object? sender, RoutedEventArgs e)
    {
        if ((AutoInjectDelaySeconds.Value ?? 0) > 0)
            AutoInjectDelaySeconds.Value = (AutoInjectDelaySeconds.Value ?? 0) - 1;
        SaveSettings();
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    private async void LaunchBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (LauncherType.SelectedItem is not LauncherOption option) return;
        InfoText.Text = "Launching...";
        var error     = await _injector.LaunchAsync(_settings.Mode, option.Id);
        InfoText.Text = error ?? "Launched.";
    }

    // ── Check for Updates (Legacy) ────────────────────────────────────────────

    private async void CheckUpdateBtn_Click(object? sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        await RunStandUpdateAsync();
        CheckUpdateBtn.IsEnabled = true;
    }

    // ── Advanced panel ────────────────────────────────────────────────────────

    private void AdvancedBtn_Click(object? sender, RoutedEventArgs e)
    {
        AdvancedPanel.IsVisible = !AdvancedPanel.IsVisible;
        _settings.Advanced      = AdvancedPanel.IsVisible;
        SaveSettings();
    }

    // ── Open Stand / Game Folder ──────────────────────────────────────────────

    private void OpenFolderBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_settings.Mode == GameMode.Legacy)
        {
            _standUpdater.OpenStandFolder();
        }
        else
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StandEnhanced");
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    // ── Settings persistence ──────────────────────────────────────────────────

    private void LauncherType_SelectionChanged(object? sender, SelectionChangedEventArgs e) => SaveSettings();

    private void AutoInjectCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (AutoInjectCheckBox.IsChecked != true)
            _autoInjectTimer.Stop();
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.AutoInject             = AutoInjectCheckBox.IsChecked == true;
        _settings.AutoInjectDelaySeconds = (int)(AutoInjectDelaySeconds.Value ?? 0);
        if (LauncherType.SelectedItem is LauncherOption option)
            _settings.GameLauncher = option.Id;

        // Persist per-mode DLL list (exclude the pinned Stand DLL entry)
        _settings.ActiveDlls.Clear();
        foreach (var d in _dlls)
        {
            if (d == _standDllEntry) continue;
            _settings.ActiveDlls.Add(d);
        }

        _settings.Save();
    }
}
