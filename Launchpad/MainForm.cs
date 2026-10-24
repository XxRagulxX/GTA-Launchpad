using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Launchpad;

public partial class MainForm : Form
{
    // ── Dark title bar ────────────────────────────────────────────────────────
    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr,
        int[] value, uint cbSize);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (DwmSetWindowAttribute(Handle, 19, new[] { 1 }, 4) != 0)
            DwmSetWindowAttribute(Handle, 20, new[] { 1 }, 4);
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly LaunchpadSettings _settings;
    private readonly StandUpdater      _updater;

    private int    _gtaPid;
    private bool   _gameWasOpen;
    private bool   _canAutoInject  = true;
    private bool   _anyInjected;

    // Resolved Stand DLL path (Legacy mode only; null until updater finishes)
    private string? _standDllPath;

    // Simple mode form width; advanced adds pnlAdvanced.Width
    private const int WidthSimple = 272;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainForm()
    {
        _settings = LaunchpadSettings.Load();
        _updater  = new StandUpdater();

        InitializeComponent();

        // Launcher combobox — same options for both modes
        var launchers = new[]
        {
            new LauncherOption(LauncherId.Steam,         "Steam"),
            new LauncherOption(LauncherId.EpicGames,     "Epic Games"),
            new LauncherOption(LauncherId.RockstarGames, "Rockstar Games"),
        };
        LauncherType.DataSource    = launchers;
        LauncherType.DisplayMember = "Name";

        // Restore settings
        AutoInjectCheckBox.Checked  = _settings.AutoInject;
        AutoInjectDelaySeconds.Value = Math.Min(_settings.AutoInjectDelaySeconds, 60);

        // Restore launcher selection
        LauncherType.SelectedIndex = launchers
            .Select((l, i) => (l, i))
            .FirstOrDefault(x => x.l.Id == _settings.GameLauncher).i;

        // Restore mode
        if (_settings.Mode == GameMode.Enhanced)
            radEnhanced.Checked = true;
        // else radLegacy is already Checked by designer

        // Restore advanced panel visibility
        if (_settings.Advanced)
            ShowAdvanced(false); // false = don't toggle, just restore

        ToggleInjectOrLaunch(false);

        // Apply mode UI (folder label, update btn visibility, DLL list)
        ApplyMode();

        // Start process scanner
        ProcessScanTimer.Start();

        // For Legacy mode, kick off Stand DLL check immediately
        if (_settings.Mode == GameMode.Legacy)
            _ = CheckStandDllAsync();
    }

    // ── Mode switching ────────────────────────────────────────────────────────

    private void radMode_CheckedChanged(object? sender, EventArgs e)
    {
        if (sender is RadioButton rb && !rb.Checked) return;

        var newMode = radEnhanced.Checked ? GameMode.Enhanced : GameMode.Legacy;
        if (newMode == _settings.Mode) return;

        SaveSettings();
        _settings.Mode   = newMode;
        _gtaPid          = 0;
        _canAutoInject   = true;
        _anyInjected     = false;

        AutoInjectTimer.Stop();
        ToggleInjectOrLaunch(false);

        ApplyMode();

        if (newMode == GameMode.Legacy)
            _ = CheckStandDllAsync();
        else
            InfoText.Text = "Ready to inject; just start the game.";

        SaveSettings();
    }

    private void ApplyMode()
    {
        bool isLegacy = _settings.Mode == GameMode.Legacy;

        // Folder button
        OpenFolderBtn.Text = isLegacy ? "Open Stand Folder" : "Open StandEnhanced Folder";

        // Check-for-Updates button only applies to Legacy (Stand DLL managed)
        CheckUpdateBtn.Visible = isLegacy;

        // Rebuild DLL list
        RebuildDllList();

        // Update launcher selection to saved preference for this mode
        var launchers = (LauncherOption[])LauncherType.DataSource!;
        int idx = Array.FindIndex(launchers, l => l.Id == _settings.GameLauncher);
        if (idx >= 0) LauncherType.SelectedIndex = idx;
    }

    // ── Stand DLL check / download (Legacy) ──────────────────────────────────

    private async Task CheckStandDllAsync()
    {
        InfoText.Text            = "Checking for updates...";
        progressBar1.Visible     = true;
        CheckUpdateBtn.Enabled   = false;

        var progress = new Progress<int>(pct => { progressBar1.Value = pct; });
        var (needsLaunchpadUpdate, error) = await _updater.CheckAndDownloadAsync(progress);

        progressBar1.Visible   = false;
        CheckUpdateBtn.Enabled = true;

        if (error != null)
        {
            InfoText.Text = error;
        }
        else
        {
            _standDllPath = _updater.StandDllPath;

            // Update the pinned Stand DLL item in the list
            if (DllList.Items.Count > 0 && _settings.Mode == GameMode.Legacy)
            {
                DllList.Items[0].Text = $"Stand {_updater.StandVersion}";
                DllList.Items[0].Tag  = _standDllPath;
                DllList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            }

            InfoText.Text = _gtaPid != 0 ? "Ready to inject." : "Ready to inject; just start the game.";

            if (needsLaunchpadUpdate && _updater.ServerLaunchpadVersion != null)
            {
                if (MessageBox.Show(
                        $"Launchpad {_updater.ServerLaunchpadVersion} is available. Would you like to go to stand.sh?",
                        "Stand Launchpad", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://stand.sh") { UseShellExecute = true });
                }
            }
        }
    }

    // ── DLL list management ───────────────────────────────────────────────────

    private void RebuildDllList()
    {
        DllList.Items.Clear();
        _standDllPath = null;

        if (_settings.Mode == GameMode.Legacy)
        {
            // Pinned Stand DLL — path resolved once updater finishes
            var standItem = new ListViewItem("Stand (checking...)")
            {
                Checked = true,
                Tag     = null, // set after CheckStandDllAsync
            };
            DllList.Items.Add(standItem);
        }

        foreach (var entry in _settings.ActiveDlls)
        {
            DllList.Items.Add(new ListViewItem(entry.Path)
            {
                Checked = entry.Checked,
                Tag     = entry.Path,
            });
        }

        DllList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
    }

    private void AddBtn_Click(object? sender, EventArgs e) => CustomDllDialog.ShowDialog();

    private void CustomDllDialog_FileOk(object? sender, System.ComponentModel.CancelEventArgs e)
        => AddDll(CustomDllDialog.FileName);

    private void AddDll(string path)
    {
        DllList.Items.Add(new ListViewItem(path)
        {
            Checked = true,
            Tag     = path,
        });
        SaveSettings();
    }

    private void RemoveBtn_Click(object? sender, EventArgs e) => RemoveSelectedDlls();

    private void RemoveSelectedDlls()
    {
        int minRemovable = _settings.Mode == GameMode.Legacy ? 1 : 0;

        foreach (ListViewItem item in DllList.SelectedItems.Cast<ListViewItem>().ToList())
        {
            if (item.Index < minRemovable) continue;
            DllList.Items.Remove(item);
        }
        SaveSettings();
    }

    private void UpBtn_Click(object? sender, EventArgs e)   => MoveSelected(-1);
    private void DownBtn_Click(object? sender, EventArgs e) => MoveSelected(1);

    private void MoveSelected(int dir)
    {
        if (DllList.SelectedItems.Count != 1) return;
        var item     = DllList.SelectedItems[0];
        int idx      = item.Index;
        int minIdx   = _settings.Mode == GameMode.Legacy ? 1 : 0;
        int newIdx   = idx + dir;
        if (newIdx < minIdx || newIdx >= DllList.Items.Count) return;

        DllList.BeginUpdate();
        bool wasChecked = item.Checked;
        DllList.Items.RemoveAt(idx);
        DllList.Items.Insert(newIdx, item);
        DllList.Items[newIdx].Checked  = wasChecked;
        DllList.Items[newIdx].Selected = true;
        DllList.EndUpdate();
        SaveSettings();
    }

    private void MoveUpBtn_Click(object? sender, EventArgs e)   => MoveSelected(-1);
    private void MoveDownBtn_Click(object? sender, EventArgs e) => MoveSelected(1);

    // Drag-drop .dll files onto the list
    private void DllList_DragOver(object? sender, DragEventArgs e)
        => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;

    private void DllList_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var f in files) AddDll(f);
    }

    private void DllList_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete) RemoveSelectedDlls();
    }

    // ── Process scanning (WinForms Timers) ────────────────────────────────────

    private void ProcessScanTimer_Tick(object? sender, EventArgs e)
    {
        if (UpdateGtaPid())
            ProcessGtaPidUpdate(_canAutoInject);
    }

    private bool UpdateGtaPid()
    {
        int newPid = GameProcess.FindPid(_settings.Mode);

        if (newPid != 0)
        {
            if (_gtaPid == newPid) return false;
            _gtaPid      = newPid;
            _gameWasOpen = true;
            return true;
        }

        AutoInjectTimer.Stop();
        bool changed = _gtaPid != 0;
        _gtaPid = 0;
        return changed;
    }

    private void ProcessGtaPidUpdate(bool canAutoInject)
    {
        bool gameRunning = _gtaPid != 0;
        ToggleInjectOrLaunch(gameRunning);

        if (gameRunning)
        {
            if (AutoInjectCheckBox.Checked && canAutoInject)
            {
                int delaySec = (int)AutoInjectDelaySeconds.Value;
                if (_settings.Advanced && delaySec > 0)
                {
                    InfoText.Text            = "Automatically injecting in a few seconds...";
                    AutoInjectTimer.Interval = delaySec * 1000;
                    AutoInjectTimer.Start();
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
        }
        else
        {
            InfoText.Text = "Ready to inject; just start the game.";
            if (_gameWasOpen)
            {
                _gameWasOpen   = false;
                _canAutoInject = false;
                GameClosedTimer.Start();
            }
        }
    }

    private void AutoInjectTimer_Tick(object? sender, EventArgs e)
    {
        AutoInjectTimer.Stop();
        _ = InjectAsync();
    }

    private void GameClosedTimer_Tick(object? sender, EventArgs e)
    {
        GameClosedTimer.Stop();
        _canAutoInject = true;
    }

    // ── Injection ─────────────────────────────────────────────────────────────

    private void InjectBtn_Click(object? sender, EventArgs e) => _ = InjectAsync();

    private async Task InjectAsync()
    {
        if (_gtaPid == 0) return;

        AutoInjectTimer.Stop();
        ProcessScanTimer.Stop();
        InjectBtn.Enabled = false;

        var dlls = GetDllPaths();

        // If the Stand DLL is missing, try re-downloading before injecting.
        if (_settings.Mode == GameMode.Legacy &&
            _standDllPath != null &&
            dlls.Contains(_standDllPath) &&
            !File.Exists(_standDllPath))
        {
            InfoText.Text = "Stand DLL missing, re-downloading...";
            await CheckStandDllAsync();
        }

        InfoText.Text = "Injecting...";

        string tempDir   = Path.Combine(Path.GetTempPath(), "LaunchpadInjector");
        int    injected  = await Task.Run(
            () => DllInjector.InjectAll(_gtaPid, dlls, tempDir, Console.WriteLine));

        if (dlls.Count == 0)
        {
            InfoText.Text = _settings.Mode == GameMode.Enhanced
                ? "No DLLs selected. Use Advanced to add a DLL first."
                : "Stand DLL not ready yet — wait for the update check.";
        }
        else
        {
            InfoText.Text = $"Injected {injected}/{dlls.Count} DLLs.";
            if (injected == 0 && !_anyInjected)
            {
                MessageBox.Show(
                    "No DLL was injected.\n1. Ensure that BattlEye is disabled.\n2. If it still doesn't work, try running the Launchpad as Administrator.",
                    "Stand Launchpad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        if (injected > 0) _anyInjected = true;

        InjectBtn.Enabled = true;
        ProcessScanTimer.Start();
    }

    private List<string> GetDllPaths()
    {
        // Legacy simple mode: inject only the auto-managed Stand DLL
        if (_settings.Mode == GameMode.Legacy && !_settings.Advanced)
        {
            if (_standDllPath != null)
                return new List<string> { _standDllPath };
            return new List<string>();
        }

        // Enhanced (any mode) + Legacy advanced: inject all checked DLLs from the list
        return DllList.Items
            .Cast<ListViewItem>()
            .Where(i => i.Checked)
            .Select(i => i.Tag as string ?? i.Text)
            .Where(p => p.Length > 0)
            .ToList();
    }

    // ── Launch game ───────────────────────────────────────────────────────────

    private async void LaunchBtn_Click(object? sender, EventArgs e)
    {
        if (LauncherType.SelectedItem is not LauncherOption option) return;
        InfoText.Text = "Launching...";
        var error     = await Task.Run(() =>
        {
            StorefrontLauncher.TryLaunch(_settings.Mode, option.Id, out var err);
            return err;
        });
        if (error != null) InfoText.Text = error;
    }

    // ── Open folder ───────────────────────────────────────────────────────────

    private void OpenFolderBtn_Click(object? sender, EventArgs e)
    {
        if (_settings.Mode == GameMode.Legacy)
        {
            _updater.OpenStandFolder();
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

    // ── Check for Updates button ──────────────────────────────────────────────

    private async void CheckUpdateBtn_Click(object? sender, EventArgs e)
    {
        CheckUpdateBtn.Enabled = false;
        await CheckStandDllAsync();
        CheckUpdateBtn.Enabled = true;
    }

    // ── Advanced panel toggle ─────────────────────────────────────────────────

    private void AdvancedBtn_Click(object? sender, EventArgs e)
    {
        ShowAdvanced(!pnlAdvanced.Visible);
        _settings.Advanced = pnlAdvanced.Visible;
        SaveSettings();
    }

    private void ShowAdvanced(bool show)
    {
        pnlAdvanced.Visible = show;
        MinimizeBox         = show;
        // Keep InjectBtn text simple vs "Inject Stand X.Y.Z"
        Width = show
            ? WidthSimple + pnlAdvanced.Width + (Width - ClientSize.Width)
            : WidthSimple + (Width - ClientSize.Width);
    }

    // ── Toggle inject / launch row ────────────────────────────────────────────

    private void ToggleInjectOrLaunch(bool gameRunning)
    {
        InjectBtn.Visible    = gameRunning;
        LaunchBtn.Visible    = !gameRunning;
        LauncherType.Visible = !gameRunning;
    }

    // ── Settings persistence ──────────────────────────────────────────────────

    private void LauncherType_SelectedIndexChanged(object? sender, EventArgs e) => SaveSettings();

    private void AutoInjectCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (!AutoInjectCheckBox.Checked) AutoInjectTimer.Stop();
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settings.AutoInject             = AutoInjectCheckBox.Checked;
        _settings.AutoInjectDelaySeconds = (int)AutoInjectDelaySeconds.Value;

        if (LauncherType.SelectedItem is LauncherOption opt)
            _settings.GameLauncher = opt.Id;

        // Persist per-mode DLL list (skip pinned Stand DLL at index 0 in Legacy mode)
        _settings.ActiveDlls.Clear();
        int startIdx = _settings.Mode == GameMode.Legacy ? 1 : 0;
        foreach (ListViewItem item in DllList.Items.Cast<ListViewItem>().Skip(startIdx))
        {
            _settings.ActiveDlls.Add(new DllEntry
            {
                Path    = item.Tag as string ?? item.Text,
                Checked = item.Checked,
            });
        }

        _settings.Save();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }
}

// ── LauncherOption (replaces deleted GameLauncher.cs) ────────────────────────

internal sealed record LauncherOption(LauncherId Id, string Name)
{
    public override string ToString() => Name;
}
