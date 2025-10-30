using System;
using System.IO;
using System.Linq;
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
    /// Resolves shortcut information including target path and custom icon location
    /// </summary>
    private static (string? TargetPath, string? IconLocation) ResolveShortcutInfo(string shortcutPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (null, null);
        }

        try
        {
            if (!File.Exists(shortcutPath))
            {
                return (null, null);
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return (null, null);
            }

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return (null, null);
            }

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            string targetPath = shortcut.TargetPath;
            string iconLocation = shortcut.IconLocation ?? string.Empty;
            
            // Also check for URL shortcuts
            if (Path.GetExtension(shortcutPath).Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var lines = File.ReadAllLines(shortcutPath);
                    string? iconIndexStr = null;
                    
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        {
                            targetPath = line.Substring(4).Trim();
                        }
                        else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                        {
                            iconLocation = line.Substring(9).Trim();
                        }
                        else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                        {
                            iconIndexStr = line.Substring(10).Trim();
                        }
                    }
                    
                    // Combine icon location with index if both are present
                    if (!string.IsNullOrWhiteSpace(iconLocation) && !string.IsNullOrWhiteSpace(iconIndexStr))
                    {
                        iconLocation = $"{iconLocation},{iconIndexStr}";
                    }
                }
                catch
                {
                    // Ignore errors reading URL file
                }
            }

            // If custom icon location exists and is valid, use it
            if (!string.IsNullOrWhiteSpace(iconLocation))
            {
                // Parse icon location format: "path,index" or just "path"
                var iconParts = iconLocation.Split(',');
                var iconPath = iconParts[0].Trim();
                
                // Remove quotes if present
                if (iconPath.StartsWith("\"") && iconPath.EndsWith("\""))
                {
                    iconPath = iconPath.Substring(1, iconPath.Length - 2);
                }

                if (File.Exists(iconPath) || Directory.Exists(iconPath))
                {
                    return (targetPath, iconLocation);
                }
            }

            return (!string.IsNullOrWhiteSpace(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath))) 
                ? (targetPath, null) 
                : (null, null);
        }
        catch
        {
            return (null, null);
        }
    }

    private static AvaloniaBitmap? GetIconInternal(string? path, bool isDirectory, IconSize size)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        string? iconPath = null;
        int iconIndex = 0;

        // For shortcut files, check for custom icon location first
        if (!string.IsNullOrWhiteSpace(path) && 
            (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)))
        {
            var (targetPath, iconLocation) = ResolveShortcutInfo(path);
            
            // If shortcut has custom icon location, use it
            if (!string.IsNullOrWhiteSpace(iconLocation))
            {
                var iconParts = iconLocation.Split(',');
                iconPath = iconParts[0].Trim();
                
                // Remove quotes if present
                if (iconPath.StartsWith("\"") && iconPath.EndsWith("\""))
                {
                    iconPath = iconPath.Substring(1, iconPath.Length - 2);
                }

                // Parse icon index if present
                if (iconParts.Length > 1 && int.TryParse(iconParts[1].Trim(), out var parsedIndex))
                {
                    iconIndex = parsedIndex;
                }

                // Update path and directory flag
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    path = iconPath;
                    isDirectory = Directory.Exists(iconPath);
                }
            }
            else if (!string.IsNullOrWhiteSpace(targetPath))
            {
                // No custom icon, use resolved target
                path = targetPath;
                isDirectory = Directory.Exists(targetPath);
            }
        }

        var flags = SHGFI_ICON;
        flags |= size == IconSize.Small ? SHGFI_SMALLICON : SHGFI_LARGEICON;

        var attributes = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        
        // If we have a specific icon index, we need to use a different approach
        // SHGetFileInfo doesn't support icon index directly, so we'll use ExtractIconEx
        if (iconIndex != 0 && !string.IsNullOrWhiteSpace(iconPath))
        {
            return ExtractIconFromIndex(iconPath, iconIndex, size);
        }

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

    /// <summary>
    /// Extracts an icon from a file using a specific icon index
    /// </summary>
    private static AvaloniaBitmap? ExtractIconFromIndex(string filePath, int iconIndex, IconSize size)
    {
        try
        {
            var largeIcon = IntPtr.Zero;
            var smallIcon = IntPtr.Zero;
            var count = ExtractIconEx(filePath, iconIndex, ref largeIcon, ref smallIcon, 1);

            if (count == 0)
            {
                return null;
            }

            var iconHandle = size == IconSize.Large ? largeIcon : smallIcon;
            if (iconHandle == IntPtr.Zero)
            {
                // Fallback to the other size if preferred size not available
                iconHandle = size == IconSize.Large ? smallIcon : largeIcon;
            }

            if (iconHandle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using DrawingIcon icon = DrawingIcon.FromHandle(iconHandle);
                using DrawingBitmap bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                return new AvaloniaBitmap(stream);
            }
            finally
            {
                // Only destroy the icon we used
                if (largeIcon != IntPtr.Zero && iconHandle == largeIcon)
                {
                    DestroyIcon(largeIcon);
                }
                else if (smallIcon != IntPtr.Zero && iconHandle == smallIcon)
                {
                    DestroyIcon(smallIcon);
                }
                else
                {
                    // Destroy both if we're not sure
                    if (largeIcon != IntPtr.Zero) DestroyIcon(largeIcon);
                    if (smallIcon != IntPtr.Zero) DestroyIcon(smallIcon);
                }
            }
        }
        catch
        {
            return null;
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

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex, ref IntPtr phiconLarge, ref IntPtr phiconSmall, int nIcons);

    #endregion
}

