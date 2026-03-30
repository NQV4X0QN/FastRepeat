using System.Windows.Forms;

namespace FastRepeat.Models;

public enum MouseBtn   { Left, Right, Middle, X1, X2, TiltLeft, TiltRight }
public enum RepeatMode { Repeat, SinglePress }

public class KeyBinding
{
    // ── Trigger: the physical key / button the user holds ────────────────────
    public bool     IsMouseButton  { get; set; }
    public int      VirtualKeyCode { get; set; }
    public MouseBtn MouseButton    { get; set; }

    // ── Output: the key / button that gets injected repeatedly ───────────────
    // Null means "same as trigger" (simple self-repeat mode).
    public bool?     OutputIsMouseButton  { get; set; }
    public int?      OutputVirtualKeyCode { get; set; }
    public MouseBtn? OutputMouseButton    { get; set; }

    // ── Resolved output helpers ───────────────────────────────────────────────
    public bool     ActualOutputIsMouseButton  => OutputIsMouseButton  ?? IsMouseButton;
    public int      ActualOutputVirtualKeyCode => OutputVirtualKeyCode ?? VirtualKeyCode;
    public MouseBtn ActualOutputMouseButton    => OutputMouseButton    ?? MouseButton;

    // ── Behaviour mode ────────────────────────────────────────────────────────
    /// <summary>
    /// Repeat: inject output key repeatedly while trigger is held (default).<br/>
    /// SinglePress: inject output key once on trigger press — no repeat loop.
    /// </summary>
    public RepeatMode Mode { get; set; } = RepeatMode.Repeat;

    /// <summary>True when the output key differs from the trigger.</summary>
    public bool HasCustomOutput => OutputIsMouseButton.HasValue;

    // ── Display names ─────────────────────────────────────────────────────────
    public string TriggerDisplayName => FriendlyName(IsMouseButton, VirtualKeyCode, MouseButton);

    public string OutputDisplayName  => HasCustomOutput
        ? FriendlyName(ActualOutputIsMouseButton, ActualOutputVirtualKeyCode, ActualOutputMouseButton)
        : TriggerDisplayName;

    /// <summary>Backward-compatible alias used by older code paths.</summary>
    public string DisplayName => TriggerDisplayName;

    /// <summary>Stable identifier keyed on the trigger (one trigger per binding).</summary>
    public string Id => IsMouseButton ? $"MOUSE_{MouseButton}" : $"KEY_{VirtualKeyCode}";

    // ── Static helper ─────────────────────────────────────────────────────────
    public static string FriendlyName(bool isMouse, int vk, MouseBtn btn) => isMouse
        ? btn switch
        {
            MouseBtn.Left      => "Mouse — Left Button",
            MouseBtn.Right     => "Mouse — Right Button",
            MouseBtn.Middle    => "Mouse — Middle Button",
            MouseBtn.X1        => "Mouse — Button 4 (Back)",
            MouseBtn.X2        => "Mouse — Button 5 (Forward)",
            MouseBtn.TiltLeft  => "Mouse — Scroll Tilt Left",
            MouseBtn.TiltRight => "Mouse — Scroll Tilt Right",
            _                  => "Mouse — Unknown"
        }
        : ((Keys)vk).ToString();
}
