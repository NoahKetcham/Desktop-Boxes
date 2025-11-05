using System;
using Avalonia;
using Avalonia.Media;

namespace Boxes.App.Services;

public static class AccentService
{
    private static Color? _defaultAccent;

    private static void EnsureDefaults()
    {
        if (_defaultAccent.HasValue)
        {
            return;
        }

        var app = Application.Current;
        if (app is null)
        {
            _defaultAccent = Color.Parse("#FF3A8DFF");
            return;
        }

        if (app.Resources.TryGetResource("AccentBrush", null, out var brush) && brush is ISolidColorBrush solid)
        {
            _defaultAccent = solid.Color;
        }
        else
        {
            _defaultAccent = Color.Parse("#FF3A8DFF");
        }
    }

    public static bool TryApplyAccentHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            var color = Color.Parse(hex.Trim());
            ApplyAccent(color);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyAccent(Color accent)
    {
        EnsureDefaults();
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        // Base accent
        app.Resources["AccentBrush"] = new SolidColorBrush(accent);

        // Muted accent: 50% alpha overlay
        var muted = Color.FromArgb(0x80, accent.R, accent.G, accent.B);
        app.Resources["AccentMutedBrush"] = new SolidColorBrush(muted);
    }

    public static void ResetToDefault()
    {
        EnsureDefaults();
        if (_defaultAccent is Color c)
        {
            ApplyAccent(c);
        }
    }
}


