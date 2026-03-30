using System.Runtime.InteropServices;
using FastRepeat.Models;
using FastRepeat.Native;

namespace FastRepeat;

/// <summary>
/// Listens to HookManager events and, for any key/button in the bindings list,
/// injects synthetic repeat events at the configured interval using SendInput.
/// </summary>
internal sealed class RepeatEngine : IDisposable
{
    private readonly HookManager _hooks;
    private readonly AppSettings _settings;

    private readonly Dictionary<string, CancellationTokenSource> _active = [];
    private readonly object _lock = new();

    public bool IsEnabled { get; set; } = true;

    public RepeatEngine(HookManager hooks, AppSettings settings)
    {
        _hooks    = hooks;
        _settings = settings;
        IsEnabled = settings.IsEnabled;

        _hooks.KeyPressed  += OnPressed;
        _hooks.KeyReleased += OnReleased;
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnPressed(object? sender, KeyBinding e)
    {
        if (!IsEnabled) return;
        if (!_settings.Bindings.Any(b => b.Id == e.Id)) return;

        lock (_lock)
        {
            if (_active.ContainsKey(e.Id)) return; // already repeating
            var cts = new CancellationTokenSource();
            _active[e.Id] = cts;
            _ = RunRepeatAsync(e, cts.Token);
        }
    }

    private void OnReleased(object? sender, KeyBinding e)
    {
        StopKey(e.Id);
    }

    public void StopAll()
    {
        List<string> keys;
        lock (_lock) { keys = [.. _active.Keys]; }
        foreach (var k in keys) StopKey(k);
    }

    private void StopKey(string id)
    {
        lock (_lock)
        {
            if (_active.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _active.Remove(id);
            }
        }
    }

    // ── Repeat loop ────────────────────────────────────────────────────────

    private async Task RunRepeatAsync(KeyBinding binding, CancellationToken ct)
    {
        try
        {
            // Initial delay = one interval period, so quick taps don't fire a repeat
            await Task.Delay(_settings.RepeatIntervalMs, ct);

            while (!ct.IsCancellationRequested)
            {
                Send(binding);
                await Task.Delay(_settings.RepeatIntervalMs, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch { /* swallow unexpected errors to avoid crashing the engine */ }
    }

    // ── SendInput helpers ──────────────────────────────────────────────────

    private static void Send(KeyBinding binding)
    {
        // Always inject the *output* key — may differ from the trigger
        if (binding.ActualOutputIsMouseButton)
            SendMouseClick(binding.ActualOutputMouseButton);
        else
            SendKeyPress((ushort)binding.ActualOutputVirtualKeyCode);
    }

    private static void SendKeyPress(ushort vk)
    {
        var inputs = new NativeMethods.INPUT[2];

        inputs[0].type     = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = vk;
        inputs[0].u.ki.dwExtraInfo = NativeMethods.FAST_REPEAT_MARKER;

        inputs[1].type          = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk      = vk;
        inputs[1].u.ki.dwFlags  = NativeMethods.KEYEVENTF_KEYUP;
        inputs[1].u.ki.dwExtraInfo = NativeMethods.FAST_REPEAT_MARKER;

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendMouseClick(MouseBtn btn)
    {
        // Scroll tilt events are a single pulse, not a down+up pair
        if (btn is MouseBtn.TiltLeft or MouseBtn.TiltRight)
        {
            // WHEEL_DELTA = 120; negative = left, positive = right
            uint delta = btn == MouseBtn.TiltRight ? 120u : unchecked((uint)(int)-120);
            var tiltInput = new NativeMethods.INPUT[1];
            tiltInput[0].type            = NativeMethods.INPUT_MOUSE;
            tiltInput[0].u.mi.dwFlags    = NativeMethods.MOUSEEVENTF_HWHEEL;
            tiltInput[0].u.mi.mouseData  = delta;
            tiltInput[0].u.mi.dwExtraInfo = NativeMethods.FAST_REPEAT_MARKER;
            NativeMethods.SendInput(1, tiltInput, Marshal.SizeOf<NativeMethods.INPUT>());
            return;
        }

        (uint downFlag, uint upFlag, uint data) = btn switch
        {
            MouseBtn.Left   => (NativeMethods.MOUSEEVENTF_LEFTDOWN,   NativeMethods.MOUSEEVENTF_LEFTUP,   0u),
            MouseBtn.Right  => (NativeMethods.MOUSEEVENTF_RIGHTDOWN,  NativeMethods.MOUSEEVENTF_RIGHTUP,  0u),
            MouseBtn.Middle => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP, 0u),
            MouseBtn.X1     => (NativeMethods.MOUSEEVENTF_XDOWN,      NativeMethods.MOUSEEVENTF_XUP,      NativeMethods.XBUTTON1 << 16),
            MouseBtn.X2     => (NativeMethods.MOUSEEVENTF_XDOWN,      NativeMethods.MOUSEEVENTF_XUP,      NativeMethods.XBUTTON2 << 16),
            _               => (NativeMethods.MOUSEEVENTF_LEFTDOWN,   NativeMethods.MOUSEEVENTF_LEFTUP,   0u)
        };

        var inputs = new NativeMethods.INPUT[2];

        inputs[0].type             = NativeMethods.INPUT_MOUSE;
        inputs[0].u.mi.dwFlags     = downFlag;
        inputs[0].u.mi.mouseData   = data;
        inputs[0].u.mi.dwExtraInfo = NativeMethods.FAST_REPEAT_MARKER;

        inputs[1].type             = NativeMethods.INPUT_MOUSE;
        inputs[1].u.mi.dwFlags     = upFlag;
        inputs[1].u.mi.mouseData   = data;
        inputs[1].u.mi.dwExtraInfo = NativeMethods.FAST_REPEAT_MARKER;

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    public void Dispose()
    {
        _hooks.KeyPressed  -= OnPressed;
        _hooks.KeyReleased -= OnReleased;
        StopAll();
    }
}
