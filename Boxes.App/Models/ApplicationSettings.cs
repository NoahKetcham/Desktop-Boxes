using System.Collections.Generic;

namespace Boxes.App.Models;

public class ApplicationSettings
{
    public string ThemePreference { get; set; } = "System";
    public bool AutoSnapEnabled { get; set; } = true;
    public bool ShowBoxOutlines { get; set; } = true;
    public bool RunAtStartup { get; set; }
    public bool OneDriveLinked { get; set; }
    public bool GoogleDriveLinked { get; set; }
    public int BoxesTransparencyPercent { get; set; } = 100;
    public string? AccentHex { get; set; }
    public string BoxBackgroundColor { get; set; } = "#1C2235";
    public List<string> RecentAccentColors { get; set; } = new();
    public List<string> RecentBoxBackgroundColors { get; set; } = new();

    // Box Customization Settings
    public int BoxHeaderHeight { get; set; } = 40;
    public bool ShowBoxHeader { get; set; } = true;
    public bool ShowBoxTitle { get; set; } = true;
    public int BoxCornerRadius { get; set; } = 8;
    public int BoxIconSize { get; set; } = 48;
    public bool ShowShortcutLabels { get; set; } = true;
    public int BoxContentPadding { get; set; } = 12;
    public int BoxContentVerticalPadding { get; set; } = 12;

    // NoteHub
    public string? NoteHubDirectoryPath { get; set; }

    // Command Center
    public bool CommandCenterShowBorder { get; set; }
    public bool CommandCenterLocked { get; set; }
    public bool CommandCenterShowTitles { get; set; } = true;
    public List<CommandCenterActionSetting> CommandCenterActions { get; set; } = CommandCenterActionCatalog.CreateDefaultSettings();

    /// <summary>Source for action button colors: App Accent, System Accent, or Custom.</summary>
    public string CommandCenterActionButtonColorSource { get; set; } = "App Accent";

    /// <summary>Custom hex color when CommandCenterActionButtonColorSource is Custom.</summary>
    public string? CommandCenterActionButtonCustomColor { get; set; }

    /// <summary>Show background color on action buttons. When false, buttons are transparent.</summary>
    public bool CommandCenterActionButtonShowBackground { get; set; } = true;

    /// <summary>When true, padding and spacing are auto-computed from monitor; when false, manual slider values are used.</summary>
    public bool CommandCenterAutoScale { get; set; } = true;

    /// <summary>Button size scaling multiplier. 1.0 = default; &lt;1 = smaller, &gt;1 = larger.</summary>
    public double CommandCenterButtonScaleMultiplier { get; set; } = 1.0;

    /// <summary>Left padding around action buttons, in pixels.</summary>
    public int CommandCenterPaddingLeft { get; set; } = 6;

    /// <summary>Right padding around action buttons, in pixels.</summary>
    public int CommandCenterPaddingRight { get; set; } = 6;

    /// <summary>Vertical padding (top/bottom) around action buttons, in pixels.</summary>
    public int CommandCenterPaddingVertical { get; set; } = 4;

    /// <summary>Horizontal gap between action buttons, in pixels.</summary>
    public int CommandCenterActionSpacingHorizontal { get; set; }

    /// <summary>Vertical gap between action buttons, in pixels.</summary>
    public int CommandCenterActionSpacingVertical { get; set; }

    public void NormalizeCommandCenterSettings()
    {
        CommandCenterActions = CommandCenterActionCatalog.Normalize(CommandCenterActions);
    }
}

