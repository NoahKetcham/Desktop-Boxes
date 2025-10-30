using System;
using System.Runtime.InteropServices;

namespace Boxes.App.Services;

/// <summary>
/// Service for retrieving Windows desktop icon metrics to match system appearance
/// </summary>
public static class DesktopIconMetricsService
{
    /// <summary>
    /// Gets the current desktop icon spacing and size from Windows system metrics
    /// </summary>
    /// <param name="itemMargin">The margin applied to each item in the XAML (defaults to 8px per side = 16 total)</param>
    public static DesktopIconMetrics GetDesktopIconMetrics(int itemMargin = 16)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Return default values for non-Windows platforms
            return new DesktopIconMetrics(75 - itemMargin, 105 - itemMargin, 48);
        }

        try
        {
            var horizontalSpacing = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXICONSPACING);
            var verticalSpacing = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYICONSPACING);
            
            // Get icon size from ICONMETRICS structure
            var iconMetrics = new NativeMethods.ICONMETRICS
            {
                cbSize = Marshal.SizeOf<NativeMethods.ICONMETRICS>()
            };

            var iconSize = 48; // Default fallback
            if (NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_GETICONMETRICS, 
                iconMetrics.cbSize, 
                ref iconMetrics, 
                0))
            {
                iconSize = iconMetrics.iHorzSpacing > 0 ? Math.Min(iconMetrics.iHorzSpacing - 27, 48) : 48;
                
                // Adjust based on actual Windows icon size (typically 32 or 48)
                if (iconSize < 40)
                    iconSize = 32;
                else
                    iconSize = 48;
            }

            // Subtract the XAML margin from system metrics to avoid double-spacing
            // The margin is applied on both sides, so we subtract the total (e.g., 8px left + 8px right = 16px)
            var adjustedHorizontalSpacing = Math.Max(horizontalSpacing - itemMargin, 48);
            var adjustedVerticalSpacing = Math.Max(verticalSpacing - itemMargin, 48);

            return new DesktopIconMetrics(
                horizontalSpacing > 0 ? adjustedHorizontalSpacing : 75 - itemMargin,
                verticalSpacing > 0 ? adjustedVerticalSpacing : 105 - itemMargin,
                iconSize
            );
        }
        catch
        {
            // Return safe defaults if system call fails
            return new DesktopIconMetrics(75 - itemMargin, 105 - itemMargin, 48);
        }
    }

    private static class NativeMethods
    {
        // System Metrics constants
        public const int SM_CXICONSPACING = 38;  // Horizontal icon spacing
        public const int SM_CYICONSPACING = 39;  // Vertical icon spacing

        // SystemParametersInfo constants
        public const uint SPI_GETICONMETRICS = 0x002D;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct ICONMETRICS
        {
            public int cbSize;
            public int iHorzSpacing;
            public int iVertSpacing;
            public int iTitleWrap;
            public LOGFONT lfFont;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SystemParametersInfo(
            uint uiAction,
            int uiParam,
            ref ICONMETRICS pvParam,
            int fWinIni);
    }
}

/// <summary>
/// Represents desktop icon spacing and size metrics
/// </summary>
public record DesktopIconMetrics(int HorizontalSpacing, int VerticalSpacing, int IconSize);

