using System.Windows.Forms;
using System.Drawing;

namespace Launchpad;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    // ── Colours (matching senpai's dark theme) ────────────────────────────────
    private static readonly Color BgColor         = Color.FromArgb(17,  17,  17);
    private static readonly Color ButtonBgColor   = Color.FromArgb(37,  40,  43);
    private static readonly Color ButtonBorderColor = Color.FromArgb(22, 25, 29);
    private static readonly Color ArrowBgColor    = Color.FromArgb(17,  17,  17);

    // ── Controls ──────────────────────────────────────────────────────────────
    // Simple (left) panel controls — always visible
    internal RadioButton radLegacy         = null!;
    internal RadioButton radEnhanced       = null!;
    internal Button      InjectBtn         = null!;
    internal Button      LaunchBtn         = null!;
    internal ComboBox    LauncherType      = null!;
    internal CheckBox    AutoInjectCheckBox = null!;
    internal Button      OpenFolderBtn     = null!;
    internal Button      CheckUpdateBtn    = null!;
    internal Button      AdvancedBtn       = null!;
    internal Label       InfoText          = null!;
    internal ProgressBar progressBar1      = null!;

    // Advanced (right) panel and its controls — hidden until Advanced clicked
    internal Panel           pnlAdvanced           = null!;
    internal Label           AutoInjectDelayLabel   = null!;
    internal NumericUpDown   AutoInjectDelaySeconds = null!;
    internal Button          UpBtn                  = null!;
    internal Button          DownBtn                = null!;
    internal Button          AddBtn                 = null!;
    internal Button          RemoveBtn              = null!;
    internal ListView        DllList                = null!;
    internal Button          MoveUpBtn              = null!;
    internal Button          MoveDownBtn            = null!;
    internal OpenFileDialog  CustomDllDialog        = null!;

    // Timers
    internal System.Windows.Forms.Timer ProcessScanTimer = null!;
    internal System.Windows.Forms.Timer AutoInjectTimer  = null!;
    internal System.Windows.Forms.Timer GameClosedTimer  = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // ── Timers ────────────────────────────────────────────────────────────
        ProcessScanTimer           = new System.Windows.Forms.Timer(components) { Interval = 1000 };
        ProcessScanTimer.Tick     += ProcessScanTimer_Tick;

        AutoInjectTimer            = new System.Windows.Forms.Timer(components) { Interval = 1 };
        AutoInjectTimer.Tick      += AutoInjectTimer_Tick;

        GameClosedTimer            = new System.Windows.Forms.Timer(components) { Interval = 10000 };
        GameClosedTimer.Tick      += GameClosedTimer_Tick;

        // ── File dialog ───────────────────────────────────────────────────────
        CustomDllDialog         = new OpenFileDialog { Filter = "DLL files|*.dll|All files|*.*", Multiselect = true };
        CustomDllDialog.FileOk += CustomDllDialog_FileOk;

        // ── Mode radio buttons (y=12) ─────────────────────────────────────────
        radLegacy = new RadioButton
        {
            Text      = "GTA V Legacy",
            Location  = new Point(12, 12),
            Size      = new Size(118, 18),
            Checked   = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
        };
        radLegacy.CheckedChanged += radMode_CheckedChanged;

        radEnhanced = new RadioButton
        {
            Text      = "GTA V Enhanced",
            Location  = new Point(134, 12),
            Size      = new Size(126, 18),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
        };
        radEnhanced.CheckedChanged += radMode_CheckedChanged;

        // ── Inject button (visible when game is running) ── y=38 ─────────────
        InjectBtn = new Button
        {
            Text      = "Inject",
            Location  = new Point(12, 38),
            Size      = new Size(248, 23),
            Visible   = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        InjectBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        InjectBtn.Click += InjectBtn_Click;

        // ── Launch button + launcher combobox (y=38) ─────────────────────────
        LaunchBtn = new Button
        {
            Text      = "Launch",
            Location  = new Point(12, 38),
            Size      = new Size(50, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
        };
        LaunchBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        LaunchBtn.Click += LaunchBtn_Click;

        LauncherType = new ComboBox
        {
            Location         = new Point(66, 38),
            Size             = new Size(194, 23),
            DropDownStyle    = ComboBoxStyle.DropDownList,
            BackColor        = ButtonBgColor,
            ForeColor        = Color.White,
            FlatStyle        = FlatStyle.Flat,
        };
        LauncherType.SelectedIndexChanged += LauncherType_SelectedIndexChanged;

        // ── Auto-inject checkbox (y=69) ───────────────────────────────────────
        AutoInjectCheckBox = new CheckBox
        {
            Text      = "Automatically inject when game starts.",
            Location  = new Point(12, 69),
            Size      = new Size(248, 17),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
        };
        AutoInjectCheckBox.CheckedChanged += AutoInjectCheckBox_CheckedChanged;

        // ── Open Stand/Enhanced folder button (y=93) ──────────────────────────
        OpenFolderBtn = new Button
        {
            Text      = "Open Stand Folder",
            Location  = new Point(12, 93),
            Size      = new Size(248, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        OpenFolderBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        OpenFolderBtn.Click += OpenFolderBtn_Click;

        // ── Check for Updates button — Legacy mode only (y=123) ───────────────
        CheckUpdateBtn = new Button
        {
            Text      = "Check for Updates",
            Location  = new Point(12, 123),
            Size      = new Size(248, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        CheckUpdateBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        CheckUpdateBtn.Click += CheckUpdateBtn_Click;

        // ── Advanced toggle button (y=153) ────────────────────────────────────
        AdvancedBtn = new Button
        {
            Text      = "Advanced",
            Location  = new Point(12, 153),
            Size      = new Size(248, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        AdvancedBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        AdvancedBtn.Click += AdvancedBtn_Click;

        // ── Status label (y=185) ──────────────────────────────────────────────
        InfoText = new Label
        {
            Text        = "Checking for updates...",
            Location    = new Point(9, 185),
            Size        = new Size(251, 26),
            ForeColor   = Color.White,
            AutoSize    = false,
            AutoEllipsis = false,
        };

        // ── Download progress bar (y=218, hidden) ─────────────────────────────
        progressBar1 = new ProgressBar
        {
            Location = new Point(12, 218),
            Size     = new Size(248, 6),
            Visible  = false,
        };

        // ── Advanced panel (right side, hidden by default) ────────────────────
        pnlAdvanced = new Panel
        {
            Location  = new Point(272, 0),
            Size      = new Size(495, 230),
            Visible   = false,
            BackColor = BgColor,
        };

        AutoInjectDelayLabel = new Label
        {
            Text      = "Automatic Injection Delay (Seconds):",
            Location  = new Point(0, 16),
            AutoSize  = true,
            ForeColor = Color.White,
        };

        AutoInjectDelaySeconds = new NumericUpDown
        {
            Location  = new Point(187, 12),
            Size      = new Size(45, 22),
            Minimum   = 0,
            Maximum   = 60,
            BackColor = ArrowBgColor,
            ForeColor = Color.White,
        };

        UpBtn = new Button
        {
            Text      = "↑",
            Location  = new Point(236, 12),
            Size      = new Size(20, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ArrowBgColor,
            ForeColor = Color.White,
            TabStop   = false,
        };
        UpBtn.FlatAppearance.BorderColor = ArrowBgColor;
        UpBtn.Click += UpBtn_Click;

        DownBtn = new Button
        {
            Text      = "↓",
            Location  = new Point(260, 12),
            Size      = new Size(20, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ArrowBgColor,
            ForeColor = Color.White,
            TabStop   = false,
        };
        DownBtn.FlatAppearance.BorderColor = ArrowBgColor;
        DownBtn.Click += DownBtn_Click;

        AddBtn = new Button
        {
            Text      = "Add",
            Location  = new Point(284, 12),
            Size      = new Size(50, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TabStop   = false,
        };
        AddBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        AddBtn.Click += AddBtn_Click;

        RemoveBtn = new Button
        {
            Text      = "Remove",
            Location  = new Point(338, 12),
            Size      = new Size(65, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
            TabStop   = false,
        };
        RemoveBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        RemoveBtn.Click += RemoveBtn_Click;

        // ── DLL list ──────────────────────────────────────────────────────────
        var col = new ColumnHeader { Width = -1, Text = "DLL" };
        DllList = new ListView
        {
            Location        = new Point(0, 43),
            Size            = new Size(463, 122),
            CheckBoxes      = true,
            FullRowSelect   = true,
            View            = View.Details,
            BackColor       = ArrowBgColor,
            ForeColor       = Color.White,
            BorderStyle     = BorderStyle.FixedSingle,
            AllowDrop       = true,
            MultiSelect     = true,
            HeaderStyle     = ColumnHeaderStyle.None,
        };
        DllList.Columns.Add(col);
        DllList.DragOver  += DllList_DragOver;
        DllList.DragDrop  += DllList_DragDrop;
        DllList.KeyUp     += DllList_KeyUp;

        MoveUpBtn = new Button
        {
            Text      = "Move Up",
            Location  = new Point(0, 172),
            Size      = new Size(65, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
        };
        MoveUpBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        MoveUpBtn.Click += MoveUpBtn_Click;

        MoveDownBtn = new Button
        {
            Text      = "Move Down",
            Location  = new Point(69, 172),
            Size      = new Size(75, 23),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonBgColor,
            ForeColor = Color.White,
        };
        MoveDownBtn.FlatAppearance.BorderColor = ButtonBorderColor;
        MoveDownBtn.Click += MoveDownBtn_Click;

        pnlAdvanced.Controls.AddRange(new Control[]
        {
            AutoInjectDelayLabel, AutoInjectDelaySeconds,
            UpBtn, DownBtn, AddBtn, RemoveBtn,
            DllList, MoveUpBtn, MoveDownBtn,
        });

        // ── Form setup ────────────────────────────────────────────────────────
        SuspendLayout();

        BackColor       = BgColor;
        ForeColor       = Color.White;
        Text            = "Stand Launchpad";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(272, 230);

        Controls.AddRange(new Control[]
        {
            radLegacy, radEnhanced,
            InjectBtn, LaunchBtn, LauncherType,
            AutoInjectCheckBox,
            OpenFolderBtn, CheckUpdateBtn, AdvancedBtn,
            InfoText, progressBar1,
            pnlAdvanced,
        });

        ResumeLayout(false);
    }
}
