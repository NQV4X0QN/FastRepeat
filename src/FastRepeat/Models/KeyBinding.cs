using System.Windows.Forms;

namespace FastRepeat.Models;

public enum MouseBtn { Left, Right, Middle, X1, X2 }

public class KeyBinding
{
    public bool     IsMouseButton  { get; set; }
    public int      VirtualKeyCode { get; set; }  // Used when IsMouseButton = false
    public MouseBtn MouseButton    { get; set; }  // Used when IsMouseButton = true

    /// <summary>Human-readable label shown in the UI.</summary>
    public string DisplayName => IsMouseButton
        ? MouseButton switch
        {
            MouseBtn.Left   => "Mouse — Left Button",
            MouseBtn.Right  => "Mouse — Right Button",
            MouseBtn.Middle => "Mouse — Middle Button",
            MouseBtn.X1     => "Mouse — Button 4 (X1)",
            MouseBtn.X2     => "Mouse — Button 5 (X2)",
            _               => $"Mouse — Unknown"
        }
        : ((Keys)VirtualKeyCode).ToString();

    /// <summary>Stable identifier used as dictionary key.</summary>
    public string Id => IsMouseButton ? $"MOUSE_{MouseButton}" : $"KEY_{VirtualKeyCode}";
}
