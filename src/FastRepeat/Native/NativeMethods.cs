using System.Runtime.InteropServices;

namespace FastRepeat.Native;

internal static class NativeMethods
{
    // Hook IDs
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    // Window messages – keyboard
    public const int WM_KEYDOWN    = 0x0100;
    public const int WM_KEYUP      = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;

    // Window messages – mouse
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP   = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP   = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP   = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP   = 0x020C;
    public const int WM_MOUSEHWHEEL = 0x020E;  // horizontal scroll / tilt

    public const uint XBUTTON1 = 0x0001;
    public const uint XBUTTON2 = 0x0002;

    // SendInput types
    public const uint INPUT_MOUSE    = 0;
    public const uint INPUT_KEYBOARD = 1;

    // Key event flags
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // Mouse event flags
    public const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
    public const uint MOUSEEVENTF_XDOWN      = 0x0080;
    public const uint MOUSEEVENTF_XUP        = 0x0100;
    public const uint MOUSEEVENTF_HWHEEL     = 0x1000;  // horizontal scroll (tilt)

    /// <summary>
    /// Unique marker placed in dwExtraInfo for events we generate ourselves,
    /// so the hook callback can skip them and avoid an infinite loop.
    /// </summary>
    public const nuint FAST_REPEAT_MARKER = 0x46524550; // ASCII "FREP"

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint  vkCode;
        public uint  scanCode;
        public uint  flags;
        public uint  time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint  mouseData;
        public uint  flags;
        public uint  time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint       type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT  mi;
        [FieldOffset(0)] public KEYBDINPUT  ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public nuint  dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int   dx;
        public int   dy;
        public uint  mouseData;
        public uint  dwFlags;
        public uint  time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ── DWM (Desktop Window Manager) ─────────────────────────────────────────
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND                   = 2;
    public const int DWMWA_BORDER_COLOR             = 34;
    public const int DWMWA_CAPTION_COLOR            = 35;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
