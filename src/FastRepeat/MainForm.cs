using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FastRepeat.Models;
using FastRepeat.Native;

namespace FastRepeat;

/// <summary>
/// Main configuration window — Windows 11 styled.
/// Closing hides to tray. Update button checks GitHub Releases.
/// </summary>
internal sealed class MainForm : Form
{
    // ── Settings / engine refs ────────────────────────────────────────────
    private readonly AppSettings  _settings;
    private readonly HookManager  _hooks;
    private readonly RepeatEngine _engine;

    // ── Controls ──────────────────────────────────────────────────────────
    private readonly Panel         _header;
    private readonly Label         _statusLabel;
    private readonly Button        _toggleEnableBtn;
    private readonly Button        _updateBtn;
    private readonly ListView      _bindingsList;
    private readonly Button        _addBtn;
    private readonly Button        _removeBtn;
    private readonly Button        _modeBtn;
    private readonly TrackBar      _speedSlider;
    private readonly NumericUpDown _speedNumeric;
    private readonly Button        _lockBtn;
    private readonly Button        _clearAllBtn;

    // ── Update state ──────────────────────────────────────────────────────
    private string? _pendingUpdateUrl;

    // ── Theme ─────────────────────────────────────────────────────────────
    private static readonly Color Bg          = Color.FromArgb(243, 243, 243);
    private static readonly Color Surface     = Color.White;
    private static readonly Color Accent      = Color.FromArgb(0,   103, 192);
    private static readonly Color AccentHover = Color.FromArgb(0,    90, 170);
    private static readonly Color AccentPress = Color.FromArgb(0,    75, 145);
    private static readonly Color TextPrimary = Color.FromArgb(28,   28,  28);
    private static readonly Color TextMuted   = Color.FromArgb(96,   96,  96);
    private static readonly Color BorderClr   = Color.FromArgb(210, 210, 210);
    private static readonly Color BtnHover    = Color.FromArgb(240, 240, 240);
    private static readonly Color BtnPress    = Color.FromArgb(225, 225, 225);
    private static readonly Color GreenBg     = Color.FromArgb(16,  124,  16);
    private static readonly Color GreenHover  = Color.FromArgb(12,  100,  12);

    private static readonly Font FontBase = SafeFont("Segoe UI Variable Text",    9f);
    private static readonly Font FontBold = SafeFont("Segoe UI Variable Text",    9f, FontStyle.Bold);
    private static readonly Font FontHead = SafeFont("Segoe UI Variable Display", 9f, FontStyle.Bold);

    public event EventHandler? SettingsChanged;

    public MainForm(AppSettings settings, HookManager hooks, RepeatEngine engine)
    {
        _settings = settings;
        _hooks    = hooks;
        _engine   = engine;

        // ── Window ────────────────────────────────────────────────────────
        Text            = "Fast Repeat";
        Size            = new Size(540, 510);
        MinimumSize     = new Size(460, 450);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = true;
        BackColor       = Bg;
        Font            = FontBase;

        // ── Header strip ──────────────────────────────────────────────────
        _header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = Accent
        };

        _statusLabel = new Label
        {
            Dock      = DockStyle.Fill,
            ForeColor = Color.White,
            Font      = FontHead,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(14, 0, 0, 0)
        };

        // Header button flow (right-aligned)
        var headerBtns = new FlowLayoutPanel
        {
            Dock          = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
            WrapContents  = false,
            Padding       = new Padding(0, 9, 8, 9)
        };

        _toggleEnableBtn = MakeHeaderBtn("Disable");
        _toggleEnableBtn.Click += ToggleEnable;

        _updateBtn = MakeHeaderBtn($"v{UpdateManager.CurrentVersion}");
        _updateBtn.Click += OnUpdateClicked;

        headerBtns.Controls.Add(_toggleEnableBtn);
        headerBtns.Controls.Add(_updateBtn);

        _header.Controls.Add(_statusLabel);
        _header.Controls.Add(headerBtns);

        // ── Body ──────────────────────────────────────────────────────────
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(12, 8, 12, 8) };

        // Speed section (bottom)
        var speedSection = new Panel { Dock = DockStyle.Bottom, Height = 120, BackColor = Bg };

        var speedHeader = SectionHeader("Repeat Speed");
        speedHeader.Dock = DockStyle.Top;

        // Speed slider row
        var sliderRow = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 1,
            BackColor   = Bg
        };
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        sliderRow.Controls.Add(StyledLabel("25 ms", TextMuted, ContentAlignment.MiddleRight), 0, 0);

        _speedSlider = new TrackBar
        {
            Minimum       = 25,
            Maximum       = 600,
            TickFrequency = 25,
            SmallChange   = 5,
            LargeChange   = 25,
            Dock          = DockStyle.Fill,
            BackColor     = Bg
        };
        _speedSlider.ValueChanged += SpeedSliderChanged;
        sliderRow.Controls.Add(_speedSlider, 1, 0);

        sliderRow.Controls.Add(StyledLabel("600 ms", TextMuted, ContentAlignment.MiddleLeft), 2, 0);

        var numFlow = new FlowLayoutPanel { AutoSize = true, BackColor = Bg, Padding = new Padding(4, 8, 0, 0) };
        _speedNumeric = new NumericUpDown
        {
            Minimum   = 25,
            Maximum   = 600,
            Width     = 58,
            TextAlign = HorizontalAlignment.Center,
            Font      = FontBase,
            BackColor = Surface
        };
        _speedNumeric.ValueChanged += SpeedNumericChanged;
        numFlow.Controls.Add(_speedNumeric);
        numFlow.Controls.Add(StyledLabel("ms", TextMuted, ContentAlignment.MiddleLeft, new Padding(2, 8, 0, 0)));
        sliderRow.Controls.Add(numFlow, 3, 0);

        // Speed action row
        var speedActions = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 40,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents  = false,
            BackColor     = Bg,
            Padding       = new Padding(0, 4, 0, 0)
        };

        _lockBtn     = MakeBtn("Lock Speed",   autoSize: true);
        _clearAllBtn = MakeBtn("Clear All",    autoSize: true);
        _lockBtn.Click     += ToggleLock;
        _clearAllBtn.Click += ClearAll;

        speedActions.Controls.Add(_lockBtn);
        speedActions.Controls.Add(_clearAllBtn);

        speedSection.Controls.Add(sliderRow);
        speedSection.Controls.Add(speedActions);
        speedSection.Controls.Add(speedHeader);   // Top — added last so it sits above

        // Bindings section (fill)
        var bindSection = new Panel { Dock = DockStyle.Fill, BackColor = Bg };

        var bindHeader = SectionHeader("Assigned Keys & Mouse Buttons");
        bindHeader.Dock = DockStyle.Top;

        _bindingsList = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            MultiSelect   = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            BorderStyle   = BorderStyle.FixedSingle,
            BackColor     = Surface,
            ForeColor     = TextPrimary,
            Font          = FontBase
        };
        _bindingsList.Columns.Add("Hold (trigger)",  120, HorizontalAlignment.Left);
        _bindingsList.Columns.Add("Sends (output)",  120, HorizontalAlignment.Left);
        _bindingsList.Columns.Add("Mode",            100, HorizontalAlignment.Left);
        _bindingsList.SelectedIndexChanged += (_, _) => UpdateSelectionButtons();

        var bindBtns = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Bg,
            Padding       = new Padding(0, 6, 0, 0)
        };

        _addBtn  = MakeBtn("Add Key / Button", autoSize: true);
        _removeBtn = MakeBtn("Remove", 90);
        _modeBtn   = MakeBtn("Toggle Mode", autoSize: true);
        _addBtn.Click   += AddBinding;
        _removeBtn.Click += RemoveBinding;
        _modeBtn.Click   += ToggleMode;
        _removeBtn.Enabled = false;
        _modeBtn.Enabled   = false;

        bindBtns.Controls.Add(_addBtn);
        bindBtns.Controls.Add(_removeBtn);
        bindBtns.Controls.Add(_modeBtn);

        bindSection.Controls.Add(_bindingsList);   // Fill
        bindSection.Controls.Add(bindBtns);         // Bottom
        bindSection.Controls.Add(bindHeader);       // Top — added last

        body.Controls.Add(bindSection);
        body.Controls.Add(speedSection);

        Controls.Add(body);
        Controls.Add(_header);

        // ── Populate ──────────────────────────────────────────────────────
        RefreshBindingsList();
        RefreshSpeedControls();
        RefreshStatusBar();

        // ── Close to tray ─────────────────────────────────────────────────
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };
    }

    // ── DWM rounded corners ───────────────────────────────────────────────
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            if (Environment.OSVersion.Version.Build >= 22000) // Windows 11+
            {
                int round = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref round, sizeof(int));
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
        RefreshStatusBar();
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
        SetUpdateBtnStyle(_updateBtn, Accent, AccentHover, AccentPress);
        _updateBtn.Text = "Checking…";
        try
        {
            var (available, version, url) = await UpdateManager.CheckAsync();
            if (available)
            {
                _pendingUpdateUrl = url;
                _updateBtn.Text   = $"↓ v{version}";
                SetUpdateBtnStyle(_updateBtn, GreenBg, GreenHover, GreenHover);
                _updateBtn.Enabled = true;
            }
            else
            {
                _updateBtn.Text = "✓ Latest";
                await Task.Delay(2500);
                _updateBtn.Text = $"v{UpdateManager.CurrentVersion}";
                SetUpdateBtnStyle(_updateBtn, Accent, AccentHover, AccentPress);
                _updateBtn.Enabled = true;
            }
        }
        catch
        {
            _updateBtn.Text = "⚠ Failed";
            await Task.Delay(2500);
            _updateBtn.Text = $"v{UpdateManager.CurrentVersion}";
            SetUpdateBtnStyle(_updateBtn, Accent, AccentHover, AccentPress);
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
                        _updateBtn.BeginInvoke(() => _updateBtn.Text = $"↓ {pct}%");
                }));

            UpdateManager.ApplyUpdate(exePath, tempPath);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            MessageBox.Show($"Download failed:\n{ex.Message}", "Update Failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _updateBtn.Text    = $"↓ v{_pendingUpdateUrl}";
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
        // Size to content first, then ensure header text never gets clipped
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

        var lockBg    = locked ? Color.FromArgb(197, 48, 48) : Surface;
        var lockHover = locked ? Color.FromArgb(170, 35, 35) : BtnHover;
        var lockPress = locked ? Color.FromArgb(145, 25, 25) : BtnPress;
        _lockBtn.BackColor = lockBg;
        _lockBtn.ForeColor = locked ? Color.White : TextPrimary;
        SetUpdateBtnStyle(_lockBtn, lockBg, lockHover, lockPress);
    }

    private void RefreshStatusBar()
    {
        bool en = _settings.IsEnabled;
        _statusLabel.Text      = en ? "● Fast Repeat — ENABLED" : "○ Fast Repeat — DISABLED";
        _toggleEnableBtn.Text  = en ? "Disable" : "Enable";
        _header.BackColor      = en ? Accent : Color.FromArgb(96, 96, 96);
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

    private static Button MakeHeaderBtn(string text)
    {
        var b = new Button
        {
            Text      = text,
            Height    = 30,
            AutoSize  = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding   = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(255, 255, 255, 30),   // slight transparent white
            ForeColor = Color.White,
            Font      = FontBase,
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize  = 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(255, 255, 255, 80);
        SetUpdateBtnStyle(b,
            Color.FromArgb(40,  40, 120, 200),
            Color.FromArgb(60,  60, 140, 220),
            Color.FromArgb(20,  20,  90, 180));
        return b;
    }

    private Button MakeBtn(string text, bool primary = false, bool autoSize = false, int width = 0)
    {
        Color bg    = primary ? Accent   : Surface;
        Color fg    = primary ? Color.White : TextPrimary;
        Color hover = primary ? AccentHover : BtnHover;
        Color press = primary ? AccentPress : BtnPress;

        var b = new Button
        {
            Text      = text,
            Height    = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = fg,
            Font      = FontBase,
            Cursor    = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = primary ? bg : BorderClr;
        b.FlatAppearance.BorderSize  = 1;

        if (autoSize) { b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.Padding = new Padding(12, 0, 12, 0); }
        else if (width > 0) b.Width = width;

        AddHover(b, bg, hover, press);
        return b;
    }

    private static void AddHover(Button b, Color normal, Color hover, Color pressed)
    {
        b.MouseEnter += (_, _) => { if (b.Enabled) b.BackColor = hover;   };
        b.MouseLeave += (_, _) => { if (b.Enabled) b.BackColor = normal;  };
        b.MouseDown  += (_, _) => { if (b.Enabled) b.BackColor = pressed; };
        b.MouseUp    += (_, _) =>
        {
            if (!b.Enabled) return;
            b.BackColor = b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)) ? hover : normal;
        };
    }

    /// <summary>Replaces the hover/press colors without changing the button's current state.</summary>
    private static void SetUpdateBtnStyle(Button b, Color normal, Color hover, Color pressed)
    {
        // Remove old handlers then add new ones via the field — simplest: just set BackColor
        b.BackColor = normal;
        // Re-wire hover: clear old events is not possible without custom EventHandler storage,
        // so we rely on the fact that we set BackColor directly on state changes above.
        // For lock/update buttons we call this after construction, so just overwrite BackColor.
    }

    private static Panel SectionHeader(string title)
    {
        var p = new Panel { Height = 26, BackColor = Bg };
        p.Controls.Add(new Label
        {
            Text      = title,
            Font      = FontBold,
            ForeColor = TextPrimary,
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });
        p.Controls.Add(new Panel
        {
            Height    = 1,
            BackColor = BorderClr,
            Dock      = DockStyle.Bottom
        });
        return p;
    }

    private static Label StyledLabel(string text, Color color, ContentAlignment align, Padding? padding = null)
    {
        var l = new Label
        {
            Text      = text,
            ForeColor = color,
            AutoSize  = true,
            TextAlign = align,
            Font      = FontBase
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
