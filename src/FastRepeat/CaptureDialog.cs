using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using FastRepeat.Models;
using FastRepeat.Native;

namespace FastRepeat;

/// <summary>
/// Modal dialog that waits for the user to press any key or mouse button,
/// then returns the captured binding. Press Escape to cancel.
/// Styled to match the Windows 11 Fluent Design of MainForm.
/// </summary>
internal sealed class CaptureDialog : Form
{
    private readonly HookManager _hooks;
    public KeyBinding? Captured { get; private set; }

    // ── Theme (matches MainForm) ──────────────────────────────────────────
    private static readonly Color LayerBg     = Color.FromArgb(243, 243, 243);
    private static readonly Color CardBg      = Color.FromArgb(255, 255, 255);
    private static readonly Color CardBorder  = Color.FromArgb(229, 229, 229);
    private static readonly Color TextPrimary = Color.FromArgb(28,   28,  28);
    private static readonly Color TextMuted   = Color.FromArgb(96,   96,  96);
    private static readonly Color Accent      = Color.FromArgb(0,    95, 184);

    public CaptureDialog(HookManager hooks,
                         string title       = "Assign Key or Button",
                         string instruction = "Press any key or mouse button.\n\nPress  Esc  to cancel.")
    {
        _hooks = hooks;

        Text            = title;
        Size            = new Size(420, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        ShowInTaskbar   = false;
        BackColor       = LayerBg;

        // Rounded card panel for content
        var card = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = CardBg,
            Margin    = new Padding(20),
            Padding   = new Padding(24)
        };

        // Use a wrapper to add margin since Panel.Margin doesn't work with Dock.Fill
        var wrapper = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = LayerBg,
            Padding   = new Padding(20, 16, 20, 16)
        };

        var instructionLabel = new Label
        {
            Text      = instruction,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = TextPrimary,
            Font      = SafeFont("Segoe UI Variable Text", 10.5f)
        };

        var hint = new Label
        {
            Text      = "Waiting for input…",
            Dock      = DockStyle.Bottom,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Accent,
            Font      = SafeFont("Segoe UI Variable Text", 8.5f, FontStyle.Italic)
        };

        card.Controls.Add(instructionLabel);
        card.Controls.Add(hint);
        wrapper.Controls.Add(card);
        Controls.Add(wrapper);

        KeyPreview = true;
        KeyDown   += OnKeyDown;
    }

    // ── DWM rounded corners ───────────────────────────────────────────────
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int round = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref round, sizeof(int));
            }
        }
        catch { }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _hooks.CaptureCallback = OnCapture;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _hooks.CaptureCallback = null;
        base.OnFormClosing(e);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _hooks.CaptureCallback = null;
            DialogResult = DialogResult.Cancel;
        }
    }

    private void OnCapture(KeyBinding binding)
    {
        Captured     = binding;
        DialogResult = DialogResult.OK;
    }

    private static Font SafeFont(string name, float size, FontStyle style = FontStyle.Regular)
    {
        try { var f = new Font(name, size, style); if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f; }
        catch { }
        return new Font("Segoe UI", size, style);
    }
}
