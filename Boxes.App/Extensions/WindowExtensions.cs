using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Boxes.App.Extensions;

public static class WindowExtensions
{
    public static void SendToDesktopBackground(this Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle is null || platformHandle.Handle == IntPtr.Zero)
        {
            return;
        }

        var hwnd = platformHandle.Handle;
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return;
        }

        if (NativeMethods.SendMessageTimeout(progman, NativeMethods.WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out _) == IntPtr.Zero)
        {
            return;
        }

        IntPtr workerw = IntPtr.Zero;
        NativeMethods.EnumWindows((topHandle, lParam) =>
        {
            var shell = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shell != IntPtr.Zero)
            {
                workerw = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        var targetParent = workerw != IntPtr.Zero ? workerw : progman;
        NativeMethods.SetParent(hwnd, targetParent);
        NativeMethods.SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        SendMessageTimeoutFlags fuFlags, uint uTimeout, out IntPtr lpdwResult);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;

    public const uint WM_SPAWN_WORKER = 0x052C;

    [Flags]
    public enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000,
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x0008
    }
}

