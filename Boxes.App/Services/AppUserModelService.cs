using System;
using System.Runtime.InteropServices;

namespace Boxes.App.Services;

internal static class AppUserModelService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    public static void TrySetCurrentProcessAppUserModelId(string appId)
    {
        try
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(appId))
            {
                _ = SetCurrentProcessExplicitAppUserModelID(appId);
            }
        }
        catch
        {
            // Ignore on older systems
        }
    }
}


