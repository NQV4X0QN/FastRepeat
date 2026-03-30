using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FastRepeat.Models;
using FastRepeat.Native;

namespace FastRepeat;

/// <summary>
/// Main configuration window — Windows 11 Fluent Design styled.
/// Closing hides to tray. Update section checks GitHub Releases.
/// </summary>
internal sealed class MainForm : Form
{
    // ── Settings / engine refs ────────────────────────────────────────────
    private readonly AppSettings  _settings;
    private readonly HookManager  _hooks;
    private readonly RepeatEngine _engine;

    // ── Controls ──────────────────────────────────────────────────────────
    private readonly Panel         _header;
    private readonly Label         _titleLabel;
    private readonly Label         _statusDot;
    private readonly FluentButton  _toggleEnableBtn;
    private readonly ListView      _bindingsList;
    private readonly FluentButton  _addBtn;
    private readonly FluentButton  _removeBtn;
    private readonly FluentButton  _modeBtn;
    private readonly TrackBar      _speedSlider;
    private readonly NumericUpDown _speedNumeric;
    private readonly FluentButton  _lockBtn;
    private readonly FluentButton  _clearAllBtn;
    private readonly FluentButton  _updateBtn;
    private readonly Label         _versionLabel;

    // ── Update state ──────────────────────────────────────────────────────
    private string? _pendingUpdateUrl;
    private string? _pendingVersion;

    // ── Theme — Windows 11 Fluent Design System ───────────────────────────
    private static readonly Color LayerBg       = Color.FromArgb(243, 243, 243);
    private static readonly Color CardBg        = Color.FromArgb(255, 255, 255);
    private static readonly Color CardBorder    = Color.FromArgb(229, 229, 229);
    private static readonly Color Accent        = Color.FromArgb(0,   95, 184);  // #005FB8
    private static readonly Color AccentLight   = Color.FromArgb(0,  103, 192);
    private static readonly Color AccentHover   = Color.FromArgb(0,   83, 163);
    private static readonly Color AccentPress   = Color.FromArgb(0,   69, 137);
    private static readonly Color TextPrimary   = Color.FromArgb(28,   28,  28);
    private static readonly Color TextSecondary = Color.FromArgb(96,   96,  96);
    private static readonly Color TextTertiary  = Color.FromArgb(136, 136, 136);
    private static readonly Color SubtleBg      = Color.FromArgb(249, 249, 249);
    private static readonly Color SubtleHover   = Color.FromArgb(242, 242, 242);
    private static readonly Color SubtlePress   = Color.FromArgb(235, 235, 235);
    private static readonly Color BorderSubtle  = Color.FromArgb(218, 218, 218);
    private static readonly Color GreenAccent   = Color.FromArgb(15,  123,  15);
    private static readonly Color GreenHover    = Color.FromArgb(11,  100,  11);
    private static readonly Color RedAccent     = Color.FromArgb(196,  43,  28);
    private static readonly Color RedHover      = Color.FromArgb(172,  36,  22);
    private static readonly Color EnabledDot    = Color.FromArgb(15,  123,  15);
    private static readonly Color DisabledDot   = Color.FromArgb(160, 160, 160);

    private static readonly Font FontBody    = SafeFont("Segoe UI Variable Text",    9.5f);
    private static readonly Font FontCaption = SafeFont("Segoe UI Variable Text",    8.5f);
    private static readonly Font FontSemiBold= SafeFont("Segoe UI Variable Text",    9.5f, FontStyle.Bold);
    private static readonly Font FontTitle   = SafeFont("Segoe UI Variable Display", 13f,  FontStyle.Bold);
    private static readonly Font FontSubtitle= SafeFont("Segoe UI Variable Display", 10f,  FontStyle.Bold);

    public event EventHandler? SettingsChanged;

    public MainForm(AppSettings settings, HookManager hooks, RepeatEngine engine)
    {
        _settings = settings;
        _hooks    = hooks;
        _engine   = engine;

        // ── Window ────────────────────────────────────────────────────────
        Text            = "Fast Repeat";
        Size            = new Size(640, 660);
        MinimumSize     = new Size(560, 580);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = true;
        BackColor       = LayerBg;
        Font            = FontBody;
        Padding         = new Padding(0);

        // ── Header area ───────────────────────────────────────────────────
        _header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 72,
            BackColor = LayerBg,
            Padding   = new Padding(24, 16, 24, 8)
        };

        var headerLeft = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            AutoSize      = false,
            BackColor     = Color.Transparent
        };

        _statusDot = new Label
        {
            Text      = "●",
            Font      = new Font("Segoe UI", 11f),
            ForeColor = EnabledDot,
            AutoSize  = true,
            Padding   = new Padding(0, 6, 4, 0)
        };

        _titleLabel = new Label
        {
            Text      = "Fast Repeat",
            Font      = FontTitle,
            ForeColor = TextPrimary,
            AutoSize  = true,
            Padding   = new Padding(0, 4, 0, 0)
        };

        headerLeft.Controls.Add(_statusDot);
        headerLeft.Controls.Add(_titleLabel);

        var headerRight = new FlowLayoutPanel
        {
            Dock          = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 4, 0, 0)
        };

        _toggleEnableBtn = MakeBtn("Disable", accent: true);
        _toggleEnableBtn.MinimumSize = new Size(90, 0);
        _toggleEnableBtn.Click += ToggleEnable;

        headerRight.Controls.Add(_toggleEnableBtn);

        _header.Controls.Add(headerLeft);
        _header.Controls.Add(headerRight);

        // ── Scrollable body ───────────────────────────────────────────────
        var body = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = LayerBg,
            Padding   = new Padding(20, 4, 20, 12),
            AutoScroll = true
        };

        // ── Card: Key Bindings ────────────────────────────────────────────
        var bindCard = MakeCard(DockStyle.Fill);

        var bindTitle = new Label
        {
            Text      = "Assigned Keys & Mouse Buttons",
            Font      = FontSubtitle,
            ForeColor = TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 32,
            Padding   = new Padding(0, 4, 0, 4)
        };

        _bindingsList = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            MultiSelect   = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            BorderStyle   = BorderStyle.None,
            BackColor     = CardBg,
            ForeColor     = TextPrimary,
            Font          = FontBody
        };
        _bindingsList.Columns.Add("Hold (trigger)",  140, HorizontalAlignment.Left);
        _bindingsList.Columns.Add("Sends (output)",  140, HorizontalAlignment.Left);
        _bindingsList.Columns.Add("Mode",            110, HorizontalAlignment.Left);
        _bindingsList.SelectedIndexChanged += (_, _) => UpdateSelectionButtons();

        var bindBtns = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 8, 0, 0)
        };

        _addBtn    = MakeBtn("Add Key / Button", accent: true);
        _removeBtn = MakeBtn("Remove");
        _modeBtn   = MakeBtn("Toggle Mode");
        _addBtn.Click    += AddBinding;
        _removeBtn.Click += RemoveBinding;
        _modeBtn.Click   += ToggleMode;
        _removeBtn.Enabled = false;
        _modeBtn.Enabled   = false;

        bindBtns.Controls.Add(_addBtn);
        bindBtns.Controls.Add(_removeBtn);
        bindBtns.Controls.Add(_modeBtn);

        // Separator line above buttons
        var bindSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = CardBorder };

        bindCard.Controls.Add(_bindingsList);   // Fill
        bindCard.Controls.Add(bindSep);          // Bottom separator
        bindCard.Controls.Add(bindBtns);         // Bottom
        bindCard.Controls.Add(bindTitle);        // Top

        // ── Card: Speed ───────────────────────────────────────────────────
        var speedCard = MakeCard(DockStyle.Bottom, height: 140);

        var speedTitle = new Label
        {
            Text      = "Repeat Speed",
            Font      = FontSubtitle,
            ForeColor = TextPrimary,
            Dock      = DockStyle.Top,
            Height    = 32,
            Padding   = new Padding(0, 4, 0, 4)
        };

        var sliderRow = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 1,
            BackColor   = Color.Transparent
        };
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        sliderRow.Controls.Add(MakeLabel("25 ms", TextTertiary), 0, 0);

        _speedSlider = new TrackBar
        {
            Minimum       = 25,
            Maximum       = 600,
            TickFrequency = 25,
            SmallChange   = 5,
            LargeChange   = 25,
            Dock          = DockStyle.Fill,
            BackColor     = CardBg
        };
        _speedSlider.ValueChanged += SpeedSliderChanged;
        sliderRow.Controls.Add(_speedSlider, 1, 0);

        sliderRow.Controls.Add(MakeLabel("600 ms", TextTertiary), 2, 0);

        var numFlow = new FlowLayoutPanel
        {
            AutoSize     = true,
            BackColor    = Color.Transparent,
            Padding      = new Padding(4, 6, 0, 0),
            WrapContents = false
        };
        _speedNumeric = new NumericUpDown
        {
            Minimum   = 25,
            Maximum   = 600,
            Width     = 62,
            TextAlign = HorizontalAlignment.Center,
            Font      = FontBody,
            BackColor = SubtleBg,
            BorderStyle = BorderStyle.FixedSingle
        };
        _speedNumeric.ValueChanged += SpeedNumericChanged;
        numFlow.Controls.Add(_speedNumeric);
        numFlow.Controls.Add(MakeLabel("ms", TextTertiary, new Padding(2, 6, 0, 0)));
        sliderRow.Controls.Add(numFlow, 3, 0);

        var speedActions = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 44,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 8, 0, 0)
        };

        _lockBtn     = MakeBtn("Lock Speed");
        _clearAllBtn = MakeBtn("Clear All");
        _lockBtn.Click     += ToggleLock;
        _clearAllBtn.Click += ClearAll;

        speedActions.Controls.Add(_lockBtn);
        speedActions.Controls.Add(_clearAllBtn);

        var speedSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = CardBorder };

        speedCard.Controls.Add(sliderRow);
        speedCard.Controls.Add(speedSep);
        speedCard.Controls.Add(speedActions);
        speedCard.Controls.Add(speedTitle);

        // ── Footer: Update section ────────────────────────────────────────
        var footer = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 50,
            BackColor = LayerBg,
            Padding   = new Padding(24, 8, 24, 8)
        };

        _versionLabel = new Label
        {
            Text      = $"v{UpdateManager.CurrentVersion}",
            Font      = FontCaption,
            ForeColor = TextTertiary,
            AutoSize  = true,
            Dock      = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(0, 4, 0, 0)
        };

        var footerRight = new FlowLayoutPanel
        {
            Dock          = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 0, 0, 0)
        };

        _updateBtn = MakeBtn("Check for Updates");
        _updateBtn.Click += OnUpdateClicked;
        footerRight.Controls.Add(_updateBtn);

        footer.Controls.Add(_versionLabel);
        footer.Controls.Add(footerRight);

        // Spacer between speed card and bindings card
        var cardSpacer = new Panel { Dock = DockStyle.Bottom, Height = 12, BackColor = LayerBg };

        body.Controls.Add(bindCard);        // Fill
        body.Controls.Add(cardSpacer);      // Bottom spacer
        body.Controls.Add(speedCard);       // Bottom card

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(_header);

        // ── Populate ──────────────────────────────────────────────────────
        RefreshBindingsList();
        RefreshSpeedControls();
        RefreshStatusDisplay();

        // ── Close to tray ─────────────────────────────────────────────────
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };
    }

    // ── DWM: Mica backdrop + rounded corners ──────────────────────────────
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            if (Environment.OSVersion.Version.Build >= 22000) // Windows 11+
            {
                // Rounded corners
                int round = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref round, sizeof(int));

                // Dark border color (subtle)
                int borderColor = ColorTranslator.ToWin32(Color.FromArgb(200, 200, 200));
                NativeMethods.DwmSetWindowAttribute(Handle,
                    NativeMethods.DWMWA_BORDER_COLOR,
                    ref borderColor, sizeof(int));
            }
        }
        catch { /* non-critical */ }
    }

    // ── Handlers ──────────────────────────────────────────────────────────

    private void ToggleEnable(object? sender, EventArgs e)
    {
        _settings.IsEnabled = !_settings.IsEnabled;
        _engine.IsEnabled   = _settings.IsEnabled;
        if (!_settings.IsEnabled) _engine.StopAll();
        Save();
        RefreshStatusDisplay();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddBinding(object? sender, EventArgs e)
    {
        using var triggerDlg = new CaptureDialog(_hooks,
            title:       "Step 1 / 2 — Trigger Key",
            instruction: "Press the key or button you will HOLD.\n\nPress  Esc  to cancel.");
        if (triggerDlg.ShowDialog(this) != DialogResult.OK || triggerDlg.Captured == null) return;

        var trigger = triggerDlg.Captured;
        if (_settings.Bindings.Any(b => b.Id == trigger.Id))
        {
            MessageBox.Show($"\"{trigger.TriggerDisplayName}\" is already assigned.",
                "Already Assigned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var choice = MessageBox.Show(
            $"Trigger: {trigger.TriggerDisplayName}\n\n" +
            "Yes  → repeat the same key\nNo   → pick a different output key",
            "Step 2 / 2 — What to Repeat?",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel) return;

        if (choice == DialogResult.No)
        {
            using var outDlg = new CaptureDialog(_hooks,
                title:       "Step 2 / 2 — Output Key",
                instruction: "Press the key or button to REPEAT while holding the trigger.\n\nPress  Esc  to cancel.");
            if (outDlg.ShowDialog(this) != DialogResult.OK || outDlg.Captured == null) return;
            var output = outDlg.Captured;
            trigger.OutputIsMouseButton  = output.IsMouseButton;
            trigger.OutputVirtualKeyCode = output.VirtualKeyCode;
            trigger.OutputMouseButton    = output.MouseButton;
        }

        _settings.Bindings.Add(trigger);
        Save();
        RefreshBindingsList();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveBinding(object? sender, EventArgs e)
    {
        if (_bindingsList.SelectedItems.Count == 0) return;
        var id = (string)_bindingsList.SelectedItems[0].Tag!;
        _settings.Bindings.RemoveAll(b => b.Id == id);
        Save();
        RefreshBindingsList();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleMode(object? sender, EventArgs e)
    {
        if (_bindingsList.SelectedItems.Count == 0) return;
        var id      = (string)_bindingsList.SelectedItems[0].Tag!;
        var binding = _settings.Bindings.FirstOrDefault(b => b.Id == id);
        if (binding == null) return;
        binding.Mode = binding.Mode == RepeatMode.Repeat ? RepeatMode.SinglePress : RepeatMode.Repeat;
        Save();
        RefreshBindingsList();
        foreach (ListViewItem item in _bindingsList.Items)
            if ((string)item.Tag! == id) { item.Selected = true; break; }
    }

    private void ClearAll(object? sender, EventArgs e)
    {
        if (_settings.Bindings.Count == 0) return;
        if (MessageBox.Show("Remove all assigned keys and buttons?", "Clear All",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _settings.Bindings.Clear();
        _engine.StopAll();
        Save();
        RefreshBindingsList();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SpeedSliderChanged(object? sender, EventArgs e)
    {
        if (_settings.IsSpeedLocked) return;
        _speedNumeric.ValueChanged -= SpeedNumericChanged;
        _speedNumeric.Value         = _speedSlider.Value;
        _speedNumeric.ValueChanged += SpeedNumericChanged;
        ApplySpeed(_speedSlider.Value);
    }

    private void SpeedNumericChanged(object? sender, EventArgs e)
    {
        if (_settings.IsSpeedLocked) return;
        _speedSlider.ValueChanged -= SpeedSliderChanged;
        _speedSlider.Value         = (int)_speedNumeric.Value;
        _speedSlider.ValueChanged += SpeedSliderChanged;
        ApplySpeed((int)_speedNumeric.Value);
    }

    private void ApplySpeed(int ms) { _settings.RepeatIntervalMs = ms; Save(); }

    private void ToggleLock(object? sender, EventArgs e)
    {
        _settings.IsSpeedLocked = !_settings.IsSpeedLocked;
        Save();
        RefreshSpeedControls();
    }

    // ── Update ────────────────────────────────────────────────────────────

    private async void OnUpdateClicked(object? sender, EventArgs e)
    {
        if (_pendingUpdateUrl != null) { await StartDownload(); return; }
        await CheckForUpdate();
    }

    private async Task CheckForUpdate()
    {
        _updateBtn.Enabled = false;
        _updateBtn.Text = "Checking…";
        try
        {
            var (available, version, url) = await UpdateManager.CheckAsync();
            if (available)
            {
                _pendingUpdateUrl = url;
                _pendingVersion   = version;
                _updateBtn.Text   = $"⬇ Download v{version}";
                _updateBtn.SetAccentColors(GreenAccent, GreenHover, GreenHover);
                _updateBtn.Enabled = true;
                _versionLabel.Text = $"v{UpdateManager.CurrentVersion}  →  v{version} available";
                _versionLabel.ForeColor = GreenAccent;
            }
            else
            {
                _updateBtn.Text = "✓ Up to date";
                _versionLabel.Text = $"v{UpdateManager.CurrentVersion} — latest";
                _versionLabel.ForeColor = TextTertiary;
                await Task.Delay(3000);
                _updateBtn.Text = "Check for Updates";
                _updateBtn.Enabled = true;
            }
        }
        catch
        {
            _updateBtn.Text = "⚠ Check failed";
            await Task.Delay(3000);
            _updateBtn.Text = "Check for Updates";
            _updateBtn.Enabled = true;
        }
    }

    private async Task StartDownload()
    {
        if (_pendingUpdateUrl == null) return;
        _updateBtn.Enabled = false;

        var exePath  = Application.ExecutablePath;
        var tempPath = exePath + ".new";

        try
        {
            await UpdateManager.DownloadAsync(_pendingUpdateUrl, tempPath,
                new Progress<int>(pct =>
                {
                    if (_updateBtn.IsHandleCreated)
                        _updateBtn.BeginInvoke(() => _updateBtn.Text = $"⬇ Downloading {pct}%");
                }));

            UpdateManager.ApplyUpdate(exePath, tempPath);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            MessageBox.Show($"Download failed:\n{ex.Message}", "Update Failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _updateBtn.Text    = $"⬇ Download v{_pendingVersion}";
            _updateBtn.Enabled = true;
        }
    }

    // ── Refresh helpers ───────────────────────────────────────────────────

    private void RefreshBindingsList()
    {
        _bindingsList.Items.Clear();
        foreach (var b in _settings.Bindings)
        {
            var item = new ListViewItem(b.TriggerDisplayName) { Tag = b.Id };
            item.SubItems.Add(b.OutputDisplayName);
            item.SubItems.Add(b.Mode == RepeatMode.SinglePress ? "Single Press" : "Repeat");
            _bindingsList.Items.Add(item);
        }
        AutoSizeColumns();
        UpdateSelectionButtons();
    }

    private void AutoSizeColumns()
    {
        for (int i = 0; i < _bindingsList.Columns.Count; i++)
            _bindingsList.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
        for (int i = 0; i < _bindingsList.Columns.Count; i++)
        {
            int contentW = _bindingsList.Columns[i].Width;
            _bindingsList.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.HeaderSize);
            _bindingsList.Columns[i].Width = Math.Max(_bindingsList.Columns[i].Width, contentW);
        }
    }

    private void RefreshSpeedControls()
    {
        bool locked = _settings.IsSpeedLocked;

        _speedSlider.ValueChanged  -= SpeedSliderChanged;
        _speedNumeric.ValueChanged -= SpeedNumericChanged;
        _speedSlider.Value          = Math.Clamp(_settings.RepeatIntervalMs, 25, 600);
        _speedNumeric.Value         = _speedSlider.Value;
        _speedSlider.ValueChanged  += SpeedSliderChanged;
        _speedNumeric.ValueChanged += SpeedNumericChanged;

        _speedSlider.Enabled  = !locked;
        _speedNumeric.Enabled = !locked;
        _lockBtn.Text         = locked ? "Unlock Speed" : "Lock Speed";

        if (locked)
            _lockBtn.SetAccentColors(RedAccent, RedHover, RedHover);
        else
            _lockBtn.SetSubtleColors(SubtleBg, SubtleHover, SubtlePress, TextPrimary);
    }

    private void RefreshStatusDisplay()
    {
        bool en = _settings.IsEnabled;
        _statusDot.ForeColor      = en ? EnabledDot : DisabledDot;
        _toggleEnableBtn.Text     = en ? "Disable" : "Enable";

        if (en)
            _toggleEnableBtn.SetAccentColors(Accent, AccentHover, AccentPress);
        else
            _toggleEnableBtn.SetAccentColors(GreenAccent, GreenHover, GreenHover);
    }

    private void UpdateSelectionButtons()
    {
        bool has = _bindingsList.SelectedItems.Count > 0;
        _removeBtn.Enabled = has;
        _modeBtn.Enabled   = has;

        if (has)
        {
            var id  = (string)_bindingsList.SelectedItems[0].Tag!;
            var b   = _settings.Bindings.FirstOrDefault(x => x.Id == id);
            _modeBtn.Text = b?.Mode == RepeatMode.SinglePress ? "Set Repeat" : "Set Single Press";
        }
        else
        {
            _modeBtn.Text = "Toggle Mode";
        }
    }

    private void Save() => _settings.Save();

    // ── Factory helpers ───────────────────────────────────────────────────

    private static Panel MakeCard(DockStyle dock, int height = 0)
    {
        var card = new RoundedPanel
        {
            Dock      = dock,
            BackColor = CardBg,
            Padding   = new Padding(16, 8, 16, 8),
            Radius    = 8,
            BorderColor = CardBorder
        };
        if (height > 0) card.Height = height;
        return card;
    }

    private static FluentButton MakeBtn(string text, bool accent = false)
    {
        var b = new FluentButton
        {
            Text     = text,
            Height   = 32,
            AutoSize = true,
            Margin   = new Padding(0, 0, 6, 0),
            Padding  = new Padding(14, 0, 14, 0),
            Font     = FontBody,
            Cursor   = Cursors.Hand,
            Radius   = 4
        };

        if (accent)
            b.SetAccentColors(Accent, AccentHover, AccentPress);
        else
            b.SetSubtleColors(SubtleBg, SubtleHover, SubtlePress, TextPrimary);

        return b;
    }

    private static Label MakeLabel(string text, Color color, Padding? padding = null)
    {
        var l = new Label
        {
            Text      = text,
            ForeColor = color,
            AutoSize  = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = FontCaption
        };
        if (padding.HasValue) l.Padding = padding.Value;
        return l;
    }

    private static Font SafeFont(string name, float size, FontStyle style = FontStyle.Regular)
    {
        try { var f = new Font(name, size, style); if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f; }
        catch { }
        return new Font("Segoe UI", size, style);
    }
}

// ── Custom Controls ───────────────────────────────────────────────────────

/// <summary>
/// Owner-drawn button with rounded corners matching Windows 11 Fluent style.
/// </summary>
internal class FluentButton : Control
{
    private Color _normalBg;
    private Color _hoverBg;
    private Color _pressBg;
    private Color _normalFg;
    private Color _borderColor;
    private bool  _isHovering;
    private bool  _isPressed;

    public int Radius { get; set; } = 4;

    public FluentButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.SupportsTransparentBackColor, true);
        DoubleBuffered = true;
        _normalBg    = Color.FromArgb(249, 249, 249);
        _hoverBg     = Color.FromArgb(242, 242, 242);
        _pressBg     = Color.FromArgb(235, 235, 235);
        _normalFg    = Color.FromArgb(28,  28,  28);
        _borderColor = Color.FromArgb(218, 218, 218);
    }

    public void SetAccentColors(Color normal, Color hover, Color press)
    {
        _normalBg    = normal;
        _hoverBg     = hover;
        _pressBg     = press;
        _normalFg    = Color.White;
        _borderColor = normal;
        Invalidate();
    }

    public void SetSubtleColors(Color normal, Color hover, Color press, Color fg)
    {
        _normalBg    = normal;
        _hoverBg     = hover;
        _pressBg     = press;
        _normalFg    = fg;
        _borderColor = Color.FromArgb(218, 218, 218);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var bg   = !Enabled ? Color.FromArgb(245, 245, 245)
                 : _isPressed ? _pressBg
                 : _isHovering ? _hoverBg
                 : _normalBg;
        var fg   = !Enabled ? Color.FromArgb(160, 160, 160) : _normalFg;
        var border = !Enabled ? Color.FromArgb(230, 230, 230) : _borderColor;

        using var path      = RoundedRect(rect, Radius);
        using var bgBrush   = new SolidBrush(bg);
        using var borderPen = new Pen(border, 1f);
        using var fgBrush   = new SolidBrush(fg);

        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        // Bottom edge highlight (Win11 depth cue)
        if (Enabled && !_isPressed)
        {
            var bottomColor = Color.FromArgb(30, 0, 0, 0);
            using var bottomPen = new Pen(bottomColor, 1f);
            g.DrawLine(bottomPen, Radius, Height - 1, Width - Radius - 1, Height - 1);
        }

        // Text
        var textSize = g.MeasureString(Text, Font);
        var textX    = (Width - textSize.Width) / 2;
        var textY    = (Height - textSize.Height) / 2;
        g.DrawString(Text, Font, fgBrush, textX, textY);
    }

    protected override void OnMouseEnter(EventArgs e)  { _isHovering = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e)  { _isHovering = false; _isPressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)  { if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)    { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        using var g = CreateGraphics();
        var textSize = g.MeasureString(Text, Font);
        return new Size(
            (int)textSize.Width + Padding.Horizontal + 4,
            Math.Max(Height, (int)textSize.Height + Padding.Vertical + 4));
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int r)
    {
        var path = new GraphicsPath();
        int d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Panel with rounded corners and optional border for card-style layout.
/// </summary>
internal class RoundedPanel : Panel
{
    public int   Radius      { get; set; } = 8;
    public Color BorderColor { get; set; } = Color.FromArgb(229, 229, 229);

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path    = RoundedRect(rect, Radius);
        using var bgBrush = new SolidBrush(BackColor);
        using var pen     = new Pen(BorderColor, 1f);

        // Clip children to rounded region
        Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), Radius));

        g.FillPath(bgBrush, path);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int r)
    {
        var path = new GraphicsPath();
        int d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
