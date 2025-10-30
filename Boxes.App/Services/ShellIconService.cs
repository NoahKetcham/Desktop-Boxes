using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using System.Drawing.Imaging;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using DrawingIcon = System.Drawing.Icon;
using DrawingBitmap = System.Drawing.Bitmap;

namespace Boxes.App.Services;

public sealed class ShellIconService
{
    public enum IconSize
    {
        Small,
        Large
    }

    public Task<AvaloniaBitmap?> GetIconAsync(string? path, bool isDirectory, IconSize size = IconSize.Large)
    {
        return Task.Run(() => GetIconInternal(path, isDirectory, size));
    }

    /// <summary>
    /// Resolves the target path of a shortcut (.lnk) file
    /// </summary>
    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        try
        {
            if (!File.Exists(shortcutPath))
            {
                return null;
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string targetPath = shortcut.TargetPath;
            
            // Also check for URL shortcuts
            if (string.IsNullOrWhiteSpace(targetPath) && Path.GetExtension(shortcutPath).Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var lines = File.ReadAllLines(shortcutPath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        {
                            targetPath = line.Substring(4).Trim();
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore errors reading URL file
                }
            }

            return !string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)) 
                ? targetPath 
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static AvaloniaBitmap? GetIconInternal(string? path, bool isDirectory, IconSize size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        // For shortcut files, resolve the target first to avoid overlay arrows
        if (!string.IsNullOrWhiteSpace(path) && 
            (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)))
        {
            var resolvedTarget = ResolveShortcutTarget(path);
            if (!string.IsNullOrWhiteSpace(resolvedTarget))
            {
                path = resolvedTarget;
                isDirectory = Directory.Exists(resolvedTarget);
            }
        }

        var flags = SHGFI_ICON;
        flags |= size == IconSize.Small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

        var attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        var useFileAttributes = string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path));

        if (useFileAttributes)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        // Removed SHGFI_ADDOVERLAYS and SHGFI_LINKOVERLAY to show actual desktop icons without shortcut arrow overlay

        var info = new SHFILEINFO();
        var result = SHGetFileInfo(path ?? string.Empty, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using DrawingIcon icon = DrawingIcon.FromHandle(info.hIcon);
            using DrawingBitmap bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    #region Native

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint SHGFI_ADDOVERLAYS = 0x000000020;
    private const uint SHGFI_LINKOVERLAY = 0x000008000;

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    #endregion
}

