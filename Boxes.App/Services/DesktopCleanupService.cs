using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Boxes.App.Services;

/// <summary>
/// Toggles Windows desktop icon visibility using the built-in "Show Desktop Icons"
/// setting (right-click desktop → View → Show desktop icons). This replaces the
/// previous archive-based approach that physically moved files off the desktop.
/// </summary>
public class DesktopCleanupService
{
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string HideIconsValue = "HideIcons";
    private const uint WM_COMMAND = 0x0111;
    private const int TOGGLE_DESKTOP_ICONS = 0x7402;

    /// <summary>
    /// Returns true when desktop icons are currently hidden.
    /// </summary>
    public Task<bool> IsDesktopCleanAsync()
    {
        return Task.FromResult(AreDesktopIconsHidden());
    }

    /// <summary>
    /// Hides desktop icons (equivalent to unchecking "Show desktop icons").
    /// </summary>
    public Task CleanAsync()
    {
        SetDesktopIconsHidden(true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows desktop icons (equivalent to checking "Show desktop icons").
    /// </summary>
    public Task<bool> RestoreAsync()
    {
        if (!AreDesktopIconsHidden())
        {
            return Task.FromResult(false);
        }

        SetDesktopIconsHidden(false);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Resets state by ensuring desktop icons are visible.
    /// </summary>
    public Task ResetAsync()
    {
        if (AreDesktopIconsHidden())
        {
            SetDesktopIconsHidden(false);
        }

        return Task.CompletedTask;
    }

    private static bool AreDesktopIconsHidden()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: false);
        if (key == null)
        {
            return false;
        }

        var value = key.GetValue(HideIconsValue);
        return value is int intValue && intValue == 1;
    }

    private static void SetDesktopIconsHidden(bool hidden)
    {
        // Write the registry value so the state persists across Explorer restarts.
        using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: true);
        key?.SetValue(HideIconsValue, hidden ? 1 : 0, RegistryValueKind.DWord);

        // Toggle the live desktop view by sending the shell command.
        RefreshDesktopIconVisibility();
    }

    /// <summary>
    /// Sends the WM_COMMAND toggle to SHELLDLL_DefView, which is the same
    /// command that the desktop context menu fires for "Show desktop icons".
    /// </summary>
    private static void RefreshDesktopIconVisibility()
    {
        var progman = FindWindow("Progman", null);
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (defView == IntPtr.Zero)
        {
            // When a wallpaper slideshow is active, SHELLDLL_DefView lives
            // under a WorkerW window instead of Progman.
            IntPtr workerW = IntPtr.Zero;
            do
            {
                workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null);
                if (workerW != IntPtr.Zero)
                {
                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                }
            } while (defView == IntPtr.Zero && workerW != IntPtr.Zero);
        }

        if (defView != IntPtr.Zero)
        {
            SendMessage(defView, WM_COMMAND, (IntPtr)TOGGLE_DESKTOP_ICONS, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
