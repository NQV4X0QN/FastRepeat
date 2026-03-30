using System.Windows.Forms;
using FastRepeat.Models;

namespace FastRepeat;

/// <summary>
/// Modal dialog that waits for the user to press any key or mouse button,
/// then returns the captured binding. Press Escape to cancel.
/// </summary>
internal sealed class CaptureDialog : Form
{
    private readonly HookManager _hooks;
    public KeyBinding? Captured { get; private set; }

    public CaptureDialog(HookManager hooks,
                         string title       = "Assign Key or Button",
                         string instruction = "Press any key or mouse button.\n\nPress  Esc  to cancel.")
    {
        _hooks = hooks;

        Text            = title;
        Size            = new System.Drawing.Size(360, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        ShowInTaskbar   = false;

        var label = new Label
        {
            Text      = instruction,
            Dock      = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Font      = new System.Drawing.Font("Segoe UI", 10f)
        };
        Controls.Add(label);

        KeyPreview = true;
        KeyDown   += OnKeyDown;
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
}
