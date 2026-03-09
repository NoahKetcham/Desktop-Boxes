using System;

namespace Boxes.App.Services;

/// <summary>
/// Computes Command Center padding and spacing values scaled to the current monitor's resolution and DPI.
/// Reference baseline: 2560x1600 @ 175% scaling.
/// </summary>
public static class CommandCenterScaleService
{
    // Reference: 2560x1600 @ 1.75 scaling
    private const double RefScaling = 1.75;

    // Ideal values at reference
    private const double RefPadLeft = 6;
    private const double RefPadRight = -4;
    private const double RefPadVertical = 4;
    private const double RefSpacingH = -6;
    private const double RefSpacingV = -6;

    /// <summary>
    /// Computes padding and spacing values scaled for the given screen and render scaling.
    /// </summary>
    public static (int PadLeft, int PadRight, int PadV, int SpacingH, int SpacingV)
        ComputeForScreen(double screenWidth, double screenHeight, double renderScaling)
    {
        var factor = renderScaling / RefScaling;

        return (
            (int)Math.Round(RefPadLeft * factor),
            (int)Math.Round(RefPadRight * factor),
            (int)Math.Round(RefPadVertical * factor),
            (int)Math.Round(RefSpacingH * factor),
            (int)Math.Round(RefSpacingV * factor)
        );
    }
}
