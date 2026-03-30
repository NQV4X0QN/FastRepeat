using System.Drawing;
using System.Windows.Forms;
using FastRepeat.Models;

namespace FastRepeat;

/// <summary>
/// Configuration window for Fast Repeat.
/// Shows all assigned keys/buttons, speed control, and enable toggle.
/// Closing hides to the system tray rather than exiting.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly AppSettings  _settings;
    private readonly HookManager  _hooks;
    private readonly RepeatEngine _engine;

    // Controls
    private readonly Panel        _headerPanel;
    private readonly Label        _statusLabel;
    private readonly Button       _toggleEnableBtn;
    private readonly ListView     _bindingsList;
    private readonly Button       _addBtn;
    private readonly Button       _removeBtn;
    private readonly TrackBar     _speedSlider;
    private readonly NumericUpDown _speedNumeric;
    private readonly Button       _lockBtn;
    private readonly Button       _clearAllBtn;

    public event EventHandler? SettingsChanged;

    public MainForm(AppSettings settings, HookManager hooks, RepeatEngine engine)
    {
        _settings = settings;
        _hooks    = hooks;
        _engine   = engine;

        // ── Window properties ───────────────────────────────────────────────
        Text            = "Fast Repeat";
        Size            = new Size(520, 490);
        MinimumSize     = new Size(460, 440);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = true;
        Font            = new Font("Segoe UI", 9f);

        // ── Build UI ────────────────────────────────────────────────────────

        // Header / status strip
        _headerPanel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = Color.FromArgb(0, 120, 215),
            Padding   = new Padding(10, 0, 10, 0)
        };

        _statusLabel = new Label
        {
            ForeColor = Color.White,
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold)
        };

        _toggleEnableBtn = new Button
        {
            Width     = 90,
            Height    = 30,
            Dock      = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 90, 180),
            Font      = new Font("Segoe UI", 9f),
            // Vertically centre the button in the 40px header
            Padding   = new Padding(0, 4, 0, 4)
        };
        _toggleEnableBtn.FlatAppearance.BorderColor = Color.White;
        _toggleEnableBtn.Click += ToggleEnable;
        _headerPanel.Controls.Add(_statusLabel);
        _headerPanel.Controls.Add(_toggleEnableBtn);

        // Bindings group  (use && to display a literal & in a GroupBox label)
        var bindingsGroup = new GroupBox
        {
            Text    = "Assigned Keys && Mouse Buttons",
            Dock    = DockStyle.Fill,
            Padding = new Padding(8)
        };

        _bindingsList = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            MultiSelect   = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable
        };
        _bindingsList.Columns.Add("Key / Button", -2, HorizontalAlignment.Left);
        _bindingsList.SelectedIndexChanged += (_, _) => UpdateRemoveButton();

        // Button row — WrapContents=false so text never folds onto a second line
        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(0, 6, 0, 0)
        };

        _addBtn = MakeButton("Add Key / Button", autoSize: true);
        _addBtn.Click += AddBinding;

        _removeBtn = MakeButton("Remove", 90);
        _removeBtn.Click += RemoveBinding;
        _removeBtn.Enabled = false;

        btnPanel.Controls.Add(_addBtn);
        btnPanel.Controls.Add(_removeBtn);

        bindingsGroup.Controls.Add(_bindingsList);
        bindingsGroup.Controls.Add(btnPanel);

        // Speed group
        var speedGroup = new GroupBox
        {
            Text    = "Repeat Speed",
            Dock    = DockStyle.Bottom,
            Height  = 115,
            Padding = new Padding(8)
        };

        var speedRow = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 4,
            RowCount    = 2
        };
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // "25 ms"
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // slider
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // "600 ms"
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // numeric + lock

        speedRow.Controls.Add(new Label { Text = "25 ms",  TextAlign = ContentAlignment.MiddleRight, AutoSize = true }, 0, 0);

        _speedSlider = new TrackBar
        {
            Minimum      = 25,
            Maximum      = 600,
            TickFrequency= 25,
            SmallChange  = 5,
            LargeChange  = 25,
            Dock         = DockStyle.Fill
        };
        _speedSlider.ValueChanged += SpeedSliderChanged;
        speedRow.Controls.Add(_speedSlider, 1, 0);

        speedRow.Controls.Add(new Label { Text = "600 ms", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true }, 2, 0);

        var numericPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _speedNumeric = new NumericUpDown
        {
            Minimum   = 25,
            Maximum   = 600,
            Width     = 60,
            TextAlign = HorizontalAlignment.Center
        };
        _speedNumeric.ValueChanged += SpeedNumericChanged;
        numericPanel.Controls.Add(_speedNumeric);
        numericPanel.Controls.Add(new Label { Text = "ms", TextAlign = ContentAlignment.MiddleLeft, AutoSize = true, Padding = new Padding(2, 5, 0, 0) });
        speedRow.Controls.Add(numericPanel, 3, 0);

        // Lock + Clear row
        var actionRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 40,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents  = false,
            Padding       = new Padding(0, 6, 0, 0)
        };

        _lockBtn = MakeButton("Lock Speed", autoSize: true);
        _lockBtn.Click += ToggleLock;

        _clearAllBtn = MakeButton("Clear All", autoSize: true);
        _clearAllBtn.Click += ClearAll;

        actionRow.Controls.Add(_lockBtn);
        actionRow.Controls.Add(_clearAllBtn);

        speedGroup.Controls.Add(speedRow);
        speedGroup.Controls.Add(actionRow);

        // ── Compose layout ──────────────────────────────────────────────────
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        content.Controls.Add(bindingsGroup);

        Controls.Add(content);
        Controls.Add(speedGroup);
        Controls.Add(_headerPanel);

        // ── Populate from settings ──────────────────────────────────────────
        RefreshBindingsList();
        RefreshSpeedControls();
        RefreshStatusBar();

        // ── Close to tray ───────────────────────────────────────────────────
        FormClosing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    // ── Handlers ───────────────────────────────────────────────────────────

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
        using var dlg = new CaptureDialog(_hooks);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Captured == null) return;

        var binding = dlg.Captured;
        if (_settings.Bindings.Any(b => b.Id == binding.Id))
        {
            MessageBox.Show($"\"{binding.DisplayName}\" is already assigned.", "Already Assigned",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settings.Bindings.Add(binding);
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

    private void ApplySpeed(int ms)
    {
        _settings.RepeatIntervalMs = ms;
        Save();
    }

    private void ToggleLock(object? sender, EventArgs e)
    {
        _settings.IsSpeedLocked = !_settings.IsSpeedLocked;
        Save();
        RefreshSpeedControls();
    }

    // ── Refresh helpers ────────────────────────────────────────────────────

    private void RefreshBindingsList()
    {
        _bindingsList.Items.Clear();
        foreach (var b in _settings.Bindings)
        {
            var item = new ListViewItem(b.DisplayName) { Tag = b.Id };
            _bindingsList.Items.Add(item);
        }
        UpdateRemoveButton();
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
        _lockBtn.BackColor    = locked ? Color.FromArgb(200, 60, 60) : SystemColors.Control;
        _lockBtn.ForeColor    = locked ? Color.White : SystemColors.ControlText;
    }

    private void RefreshStatusBar()
    {
        bool en = _settings.IsEnabled;
        _statusLabel.Text    = en ? "● Fast Repeat — ENABLED" : "○ Fast Repeat — DISABLED";
        _toggleEnableBtn.Text = en ? "Disable" : "Enable";
        _headerPanel.BackColor = en ? Color.FromArgb(0, 120, 215) : Color.FromArgb(100, 100, 100);
    }

    private void UpdateRemoveButton() =>
        _removeBtn.Enabled = _bindingsList.SelectedItems.Count > 0;

    private void Save() => _settings.Save();

    private static Button MakeButton(string text, int width = 0, bool autoSize = false)
    {
        var btn = new Button
        {
            Text      = text,
            Height    = 30,
            FlatStyle = FlatStyle.System
        };
        if (autoSize)
        {
            btn.AutoSize     = true;
            btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btn.Padding      = new Padding(8, 0, 8, 0);
        }
        else
        {
            btn.Width = width;
        }
        return btn;
    }
}
