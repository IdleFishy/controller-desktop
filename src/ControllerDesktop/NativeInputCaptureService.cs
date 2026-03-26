using ControllerDesktop.Interop;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ControllerDesktop.Services;

public sealed class NativeInputCaptureService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly NativeMethods.HookProc _keyboardProc;
    private readonly NativeMethods.HookProc _mouseProc;
    private readonly List<string> _tokens = new();
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private CaptureSnapshot _snapshot = CaptureSnapshot.Idle;
    private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

    public NativeInputCaptureService()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public CaptureSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return _snapshot;
        }
    }

    public void StartCapture()
    {
        lock (_syncRoot)
        {
            CancelCaptureInternal(false);
            _tokens.Clear();
            _startedAt = DateTimeOffset.UtcNow;
            _snapshot = new CaptureSnapshot(true, false, null, null, null, null);
            InstallHooks();
        }
    }

    public void CancelCapture()
    {
        lock (_syncRoot)
        {
            CancelCaptureInternal(true);
        }
    }

    private void CancelCaptureInternal(bool setSnapshot)
    {
        UninstallHooks();
        _tokens.Clear();
        if (setSnapshot)
        {
            _snapshot = CaptureSnapshot.Idle;
        }
    }

    private void InstallHooks()
    {
        if (_keyboardHook != IntPtr.Zero || _mouseHook != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc, moduleHandle, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc, moduleHandle, 0);
    }

    private void UninstallHooks()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < NativeMethods.HcAction)
        {
            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        lock (_syncRoot)
        {
            if (!_snapshot.Active)
            {
                return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var token = KeyTokenFromVirtualKey(data.vkCode);
            if (string.IsNullOrWhiteSpace(token))
            {
                return (IntPtr)1;
            }

            if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
            {
                if (!_tokens.Any(item => string.Equals(item, token, StringComparison.OrdinalIgnoreCase)))
                {
                    _tokens.Add(token);
                }

                if (!IsModifier(token))
                {
                    CompleteKeyboardCapture(OrderTokens(_tokens));
                }

                return (IntPtr)1;
            }

            if (message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
            {
                if (_tokens.Count == 1 && string.Equals(_tokens[0], token, StringComparison.OrdinalIgnoreCase))
                {
                    CompleteKeyboardCapture(_tokens);
                    return (IntPtr)1;
                }

                _tokens.RemoveAll(item => string.Equals(item, token, StringComparison.OrdinalIgnoreCase));
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < NativeMethods.HcAction)
        {
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        lock (_syncRoot)
        {
            if (!_snapshot.Active)
            {
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            if (DateTimeOffset.UtcNow - _startedAt < TimeSpan.FromMilliseconds(220))
            {
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
            switch (message)
            {
                case NativeMethods.WmLButtonDown:
                    CompleteMouseButtonCapture("Left");
                    return (IntPtr)1;
                case NativeMethods.WmMButtonDown:
                    CompleteMouseButtonCapture("Middle");
                    return (IntPtr)1;
                case NativeMethods.WmRButtonDown:
                    CompleteMouseButtonCapture("Right");
                    return (IntPtr)1;
                case NativeMethods.WmMouseWheel:
                    var delta = unchecked((short)((data.mouseData >> 16) & 0xffff));
                    CompleteMouseWheelCapture(delta >= 0 ? 1 : -1);
                    return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void CompleteKeyboardCapture(IEnumerable<string> tokens)
    {
        var parameter = string.Join("+", tokens.Where(token => !string.IsNullOrWhiteSpace(token)));
        _snapshot = new CaptureSnapshot(false, true, "keyboard", parameter, null, parameter);
        UninstallHooks();
        _tokens.Clear();
    }

    private void CompleteMouseButtonCapture(string button)
    {
        _snapshot = new CaptureSnapshot(false, true, "mouseButton", button, null, button);
        UninstallHooks();
        _tokens.Clear();
    }

    private void CompleteMouseWheelCapture(int direction)
    {
        var display = direction > 0 ? "滚轮上滚" : "滚轮下滚";
        _snapshot = new CaptureSnapshot(false, true, "mouseWheel", "Vertical", direction, display);
        UninstallHooks();
        _tokens.Clear();
    }

    private static List<string> OrderTokens(IEnumerable<string> tokens)
    {
        var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CTRL"] = 0,
            ["SHIFT"] = 1,
            ["ALT"] = 2,
            ["LWIN"] = 3
        };

        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(token => priority.TryGetValue(token, out var value) ? value : 99)
            .ThenBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsModifier(string token)
    {
        return token is "CTRL" or "SHIFT" or "ALT" or "LWIN";
    }

    private static string? KeyTokenFromVirtualKey(uint vkCode)
    {
        return vkCode switch
        {
            0x08 => "BACKSPACE",
            0x09 => "TAB",
            0x0D => "ENTER",
            0x10 or 0xA0 or 0xA1 => "SHIFT",
            0x11 or 0xA2 or 0xA3 => "CTRL",
            0x12 or 0xA4 or 0xA5 => "ALT",
            0x1B => "ESC",
            0x20 => "SPACE",
            0x21 => "PGUP",
            0x22 => "PGDN",
            0x23 => "END",
            0x24 => "HOME",
            0x25 => "LEFT",
            0x26 => "UP",
            0x27 => "RIGHT",
            0x28 => "DOWN",
            0x2D => "INSERT",
            0x2E => "DELETE",
            0x5B or 0x5C => "LWIN",
            >= 0x30 and <= 0x39 => ((char)vkCode).ToString(),
            >= 0x41 and <= 0x5A => ((char)vkCode).ToString(),
            >= 0x70 and <= 0x87 => $"F{vkCode - 0x6F}",
            _ => null
        };
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            CancelCaptureInternal(true);
        }
    }
}

public sealed record CaptureSnapshot(
    bool Active,
    bool Completed,
    string? InputKind,
    string? Parameter,
    int? Direction,
    string? DisplayText)
{
    public static CaptureSnapshot Idle { get; } = new(false, false, null, null, null, null);
}
