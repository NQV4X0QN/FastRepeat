using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FastRepeat.Models;

namespace FastRepeat;

/// <summary>
/// Application context that manages the system tray icon and lazily creates
/// the configuration window. This is the root of the application lifecycle.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly AppSettings  _settings;
    private readonly HookManager  _hooks;
    private readonly RepeatEngine _engine;
    private readonly NotifyIcon   _tray;
    private readonly ToolStripMenuItem _enableItem;

    private MainForm? _form;

    public TrayApp()
    {
        _settings = AppSettings.Load();
        _hooks    = new HookManager();
        _engine   = new RepeatEngine(_hooks, _settings);

        // ── Tray context menu ───────────────────────────────────────────────
        var menu = new ContextMenuStrip();

        var openItem = (ToolStripMenuItem)menu.Items.Add("Open Fast Repeat");
        openItem.Font  = new Font(openItem.Font, FontStyle.Bold);
        openItem.Click += (_, _) => ShowForm();

        menu.Items.Add(new ToolStripSeparator());

        _enableItem = new ToolStripMenuItem("Enabled") { CheckOnClick = true, Checked = _settings.IsEnabled };
        _enableItem.Click += ToggleEnable;
        menu.Items.Add(_enableItem);

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
        g.Clear(Color.Transparent);

        var bg  = enabled ? Color.FromArgb(0, 120, 215) : Color.FromArgb(110, 110, 110);
        var fg  = Color.White;

        using var bgBrush = new SolidBrush(bg);
        using var fgBrush = new SolidBrush(fg);

        // Rounded-ish square
        g.FillRectangle(bgBrush, 1, 1, 14, 14);

        // "F" letterform
        using var font = new Font("Arial", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
        g.DrawString("F", font, fgBrush, 2f, 2f);

        // Small dot bottom-right to indicate active
        if (enabled)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(80, 255, 80));
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
