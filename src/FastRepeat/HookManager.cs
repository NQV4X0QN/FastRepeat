using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FastRepeat.Models;
using FastRepeat.Native;

namespace FastRepeat;

/// <summary>
/// Installs system-wide low-level keyboard and mouse hooks and raises events
/// whenever a key or mouse button is pressed or released.
/// </summary>
internal sealed class HookManager : IDisposable
{
    private IntPtr _keyHook  = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    // Keep delegate references alive to prevent GC collection
    private readonly NativeMethods.HookProc _keyProc;
    private readonly NativeMethods.HookProc _mouseProc;

    /// <summary>
    /// When non-null, the next key/mouse event (that is not Escape) is passed
    /// to this callback instead of firing the normal KeyDown/MouseDown events.
    /// The callback is automatically cleared after it fires once.
    /// </summary>
    public Action<KeyBinding>? CaptureCallback { get; set; }

    public event EventHandler<KeyBinding>? KeyPressed;
    public event EventHandler<KeyBinding>? KeyReleased;

    public HookManager()
    {
        _keyProc   = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    public void Install()
    {
        if (_keyHook != IntPtr.Zero) return;

        using var process = Process.GetCurrentProcess();
        using var module  = process.MainModule!;
        var hMod = NativeMethods.GetModuleHandle(module.ModuleName);

        _keyHook   = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyProc,   hMod, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL,    _mouseProc, hMod, 0);
    }

    public void Uninstall()
    {
        if (_keyHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyHook);
            _keyHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    public void Dispose() => Uninstall();

    // ── Keyboard callback ──────────────────────────────────────────────────

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var ks = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Skip events that we generated ourselves
            if (ks.dwExtraInfo != NativeMethods.FAST_REPEAT_MARKER)
            {
                var msg = (int)wParam;
                bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
                bool isUp   = msg == NativeMethods.WM_KEYUP   || msg == NativeMethods.WM_SYSKEYUP;

                var vk = (int)ks.vkCode;

                if (isDown)
                {
                    var binding = new KeyBinding { IsMouseButton = false, VirtualKeyCode = vk };

                    if (CaptureCallback != null && vk != (int)Keys.Escape)
                    {
                        var cb = CaptureCallback;
                        CaptureCallback = null;
                        cb(binding);
                    }
                    else if (CaptureCallback == null)
                    {
                        KeyPressed?.Invoke(this, binding);
                    }
                }
                else if (isUp)
                {
                    KeyReleased?.Invoke(this, new KeyBinding { IsMouseButton = false, VirtualKeyCode = vk });
                }
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // ── Mouse callback ─────────────────────────────────────────────────────

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var ms  = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            if (ms.dwExtraInfo != NativeMethods.FAST_REPEAT_MARKER)
            {
                var msg = (int)wParam;
                bool isDown = msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN
                                  or NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_XBUTTONDOWN;
                bool isUp   = msg is NativeMethods.WM_LBUTTONUP   or NativeMethods.WM_RBUTTONUP
                                  or NativeMethods.WM_MBUTTONUP   or NativeMethods.WM_XBUTTONUP;

                if (isDown || isUp)
                {
                    var btn = msg switch
                    {
                        NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_LBUTTONUP => MouseBtn.Left,
                        NativeMethods.WM_RBUTTONDOWN or NativeMethods.WM_RBUTTONUP => MouseBtn.Right,
                        NativeMethods.WM_MBUTTONDOWN or NativeMethods.WM_MBUTTONUP => MouseBtn.Middle,
                        NativeMethods.WM_XBUTTONDOWN or NativeMethods.WM_XBUTTONUP =>
                            (ms.mouseData >> 16) == NativeMethods.XBUTTON1 ? MouseBtn.X1 : MouseBtn.X2,
                        _ => MouseBtn.Left
                    };

                    var binding = new KeyBinding { IsMouseButton = true, MouseButton = btn };

                    if (isDown)
                    {
                        if (CaptureCallback != null)
                        {
                            var cb = CaptureCallback;
                            CaptureCallback = null;
                            cb(binding);
                        }
                        else
                        {
                            KeyPressed?.Invoke(this, binding);
                        }
                    }
                    else
                    {
                        KeyReleased?.Invoke(this, binding);
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
