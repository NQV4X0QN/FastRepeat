using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FastRepeat.Models;
using Microsoft.Win32;

namespace FastRepeat;

/// <summary>
/// Application context that manages the system tray icon and lazily creates
/// the configuration window. This is the root of the application lifecycle.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string StartupRegKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegName = "FastRepeat";

    private readonly AppSettings  _settings;
    private readonly HookManager  _hooks;
    private readonly RepeatEngine _engine;
    private readonly NotifyIcon   _tray;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _startupItem;

    private MainForm? _form;

    public TrayApp()
    {
        _settings = AppSettings.Load();
        _hooks    = new HookManager();
        _engine   = new RepeatEngine(_hooks, _settings);

        // Sync the registry with the persisted setting on startup
        SyncStartupRegistry(_settings.RunAtStartup);

        // ── Tray context menu ───────────────────────────────────────────────
        var menu = new ContextMenuStrip();

        var openItem = (ToolStripMenuItem)menu.Items.Add("Open Fast Repeat");
        openItem.Font  = new Font(openItem.Font, FontStyle.Bold);
        openItem.Click += (_, _) => ShowForm();

        menu.Items.Add(new ToolStripSeparator());

        _enableItem = new ToolStripMenuItem("Enabled") { CheckOnClick = true, Checked = _settings.IsEnabled };
        _enableItem.Click += ToggleEnable;
        menu.Items.Add(_enableItem);

        _startupItem = new ToolStripMenuItem("Run at Startup") { CheckOnClick = true, Checked = _settings.RunAtStartup };
        _startupItem.Click += ToggleStartup;
        menu.Items.Add(_startupItem);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Exit").Click += (_, _) => Shutdown();

        // ── Tray icon ───────────────────────────────────────────────────────
        _tray = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible          = true
        };
        _tray.DoubleClick += (_, _) => ShowForm();
        _tray.BalloonTipTitle = "Fast Repeat";

        UpdateTray();
        _hooks.Install();
    }

    // ── Show / hide window ─────────────────────────────────────────────────

    private void ShowForm()
    {
        if (_form == null || _form.IsDisposed)
        {
            _form = new MainForm(_settings, _hooks, _engine);
            _form.SettingsChanged += (_, _) => UpdateTray();
            _form.FormClosed      += (_, _) => _form = null;
        }

        _form.Show();
        _form.BringToFront();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    // ── Tray helpers ───────────────────────────────────────────────────────

    private void ToggleEnable(object? sender, EventArgs e)
    {
        _settings.IsEnabled = _enableItem.Checked;
        _engine.IsEnabled   = _settings.IsEnabled;
        if (!_settings.IsEnabled) _engine.StopAll();
        _settings.Save();
        UpdateTray();

        // Keep the open form in sync if it's visible
        if (_form != null && !_form.IsDisposed)
        {
            _form.Close(); // triggers Hide()
            ShowForm();
        }
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        _settings.RunAtStartup = _startupItem.Checked;
        _settings.Save();
        SyncStartupRegistry(_settings.RunAtStartup);
    }

    /// <summary>
    /// Adds or removes the HKCU Run registry entry to control Windows login startup.
    /// Uses the installed EXE path so it works correctly after self-install.
    /// </summary>
    private static void SyncStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Application.ExecutablePath;
                key.SetValue(StartupRegName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(StartupRegName, throwOnMissingValue: false);
            }
        }
        catch { /* non-critical — user may not have registry access */ }
    }

    private void UpdateTray()
    {
        bool en = _settings.IsEnabled;
        int  n  = _settings.Bindings.Count;

        _tray.Icon = BuildIcon(en);
        _tray.Text = en
            ? $"Fast Repeat  •  {n} key{(n == 1 ? "" : "s")} active"
            : "Fast Repeat  •  Disabled";

        _enableItem.Checked = en;
    }

    private void Shutdown()
    {
        _tray.Visible = false;
        _engine.Dispose();
        _hooks.Dispose();
        Application.Exit();
    }

    // ── Icon generation ────────────────────────────────────────────────────

    private static Icon BuildIcon(bool enabled)
    {
        using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var bg = enabled ? Color.FromArgb(0, 95, 184) : Color.FromArgb(128, 128, 128);
        var fg = Color.White;

        using var bgBrush = new SolidBrush(bg);
        using var fgBrush = new SolidBrush(fg);

        using var path = new GraphicsPath();
        path.AddArc(1, 1, 4, 4, 180, 90);
        path.AddArc(11, 1, 4, 4, 270, 90);
        path.AddArc(11, 11, 4, 4, 0, 90);
        path.AddArc(1, 11, 4, 4, 90, 90);
        path.CloseFigure();
        g.FillPath(bgBrush, path);

        using var font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.DrawString("F", font, fgBrush, 2.5f, 1.5f);

        if (enabled)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(15, 123, 15));
            g.FillEllipse(dotBrush, 10, 10, 5, 5);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _engine.Dispose();
            _hooks.Dispose();
        }
        base.Dispose(disposing);
    }
}
