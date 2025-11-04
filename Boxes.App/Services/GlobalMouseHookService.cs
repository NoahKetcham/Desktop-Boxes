using System;
using System.Runtime.InteropServices;

namespace Boxes.App.Services;

internal static class GlobalMouseHookService
{
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static HookProc? _proc;
    private static Action<IntPtr>? _onMouseDown;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const uint GA_ROOT = 2;

    public static void Start(Action<IntPtr> onMouseDown)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (_hookHandle != IntPtr.Zero)
        {
            _onMouseDown = onMouseDown; // update callback
            return;
        }
        _onMouseDown = onMouseDown;
        _proc = HookCallback;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc!, IntPtr.Zero, 0);
    }

    public static void Stop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
        _proc = null;
        _onMouseDown = null;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _onMouseDown != null)
        {
            var msg = (int)wParam;
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
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
                    _onMouseDown?.Invoke(hwnd);
                }
                catch
                {
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
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
}


