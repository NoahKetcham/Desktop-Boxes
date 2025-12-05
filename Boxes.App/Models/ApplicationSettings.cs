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
}

