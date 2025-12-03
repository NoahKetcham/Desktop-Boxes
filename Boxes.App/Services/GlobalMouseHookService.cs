using System;
using System.Runtime.InteropServices;

namespace Boxes.App.Services;

internal static class GlobalMouseHookService
{
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static HookProc? _proc;
    private static readonly object _syncRoot = new();

    private static event EventHandler<GlobalMouseEventArgs>? _buttonDown;
    private static event EventHandler<GlobalMouseEventArgs>? _leftButtonDown;
    private static event EventHandler<GlobalMouseEventArgs>? _leftButtonUp;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const uint GA_ROOT = 2;

    public static event EventHandler<GlobalMouseEventArgs>? ButtonDown
    {
        add
        {
            lock (_syncRoot)
            {
                _buttonDown += value;
                EnsureHook();
            }
        }
        remove
        {
            lock (_syncRoot)
            {
                _buttonDown -= value;
                TryReleaseHook();
            }
        }
    }

    public static event EventHandler<GlobalMouseEventArgs>? LeftButtonDown
    {
        add
        {
            lock (_syncRoot)
            {
                _leftButtonDown += value;
                EnsureHook();
            }
        }
        remove
        {
            lock (_syncRoot)
            {
                _leftButtonDown -= value;
                TryReleaseHook();
            }
        }
    }

    public static event EventHandler<GlobalMouseEventArgs>? LeftButtonUp
    {
        add
        {
            lock (_syncRoot)
            {
                _leftButtonUp += value;
                EnsureHook();
            }
        }
        remove
        {
            lock (_syncRoot)
            {
                _leftButtonUp -= value;
                TryReleaseHook();
            }
        }
    }

    private static void EnsureHook()
    {
        if (_hookHandle != IntPtr.Zero || !OperatingSystem.IsWindows())
        {
            return;
        }

        _proc ??= HookCallback;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc!, IntPtr.Zero, 0);
    }

    private static void TryReleaseHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (_buttonDown == null && _leftButtonDown == null && _leftButtonUp == null)
        {
            if (OperatingSystem.IsWindows())
            {
                UnhookWindowsHookEx(_hookHandle);
            }
            _hookHandle = IntPtr.Zero;
            _proc = null;
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (_buttonDown != null || _leftButtonDown != null || _leftButtonUp != null))
        {
            var msg = (int)wParam;
            try
            {
                var hwnd = ResolveWindowHandle(lParam);
                switch (msg)
                {
                    case WM_LBUTTONDOWN:
                        Raise(_leftButtonDown, hwnd, GlobalMouseButton.Left);
                        Raise(_buttonDown, hwnd, GlobalMouseButton.Left);
                        break;
                    case WM_LBUTTONUP:
                        Raise(_leftButtonUp, hwnd, GlobalMouseButton.Left);
                        break;
                    case WM_RBUTTONDOWN:
                        Raise(_buttonDown, hwnd, GlobalMouseButton.Right);
                        break;
                    case WM_MBUTTONDOWN:
                        Raise(_buttonDown, hwnd, GlobalMouseButton.Middle);
                        break;
                    case WM_XBUTTONDOWN:
                        var button = GetXButton(lParam);
                        Raise(_buttonDown, hwnd, button);
                        break;
                }
            }
            catch
            {
                // Swallow exceptions from event handlers to avoid breaking the hook.
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static GlobalMouseButton GetXButton(IntPtr lParam)
    {
        try
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var highWord = (data.mouseData >> 16) & 0xFFFF;
            return highWord == 1 ? GlobalMouseButton.XButton1 : GlobalMouseButton.XButton2;
        }
        catch
        {
            return GlobalMouseButton.XButton1;
        }
    }

    private static IntPtr ResolveWindowHandle(IntPtr lParam)
    {
        try
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var pt = new POINT { x = data.pt.x, y = data.pt.y };
            var hwnd = WindowFromPoint(pt);
            if (hwnd != IntPtr.Zero)
            {
                hwnd = GetAncestor(hwnd, GA_ROOT);
            }
            return hwnd;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static void Raise(EventHandler<GlobalMouseEventArgs>? handler, IntPtr hwnd, GlobalMouseButton button)
    {
        if (handler == null)
        {
            return;
        }

        var args = new GlobalMouseEventArgs(hwnd, button);
        foreach (EventHandler<GlobalMouseEventArgs> subscriber in handler.GetInvocationList())
        {
            try
            {
                subscriber.Invoke(null, args);
            }
            catch
            {
                // Ignore individual subscriber failures
            }
        }
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    public enum GlobalMouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }

    public sealed class GlobalMouseEventArgs : EventArgs
    {
        internal GlobalMouseEventArgs(IntPtr windowHandle, GlobalMouseButton button)
        {
            WindowHandle = windowHandle;
            Button = button;
        }

        public IntPtr WindowHandle { get; }

        public GlobalMouseButton Button { get; }
    }
}


