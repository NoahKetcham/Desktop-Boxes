namespace Boxes.App.Models;

public class ApplicationSettings
{
    public string ThemePreference { get; set; } = "System";
    public bool AutoSnapEnabled { get; set; } = true;
    public bool ShowBoxOutlines { get; set; } = true;
    public bool OneDriveLinked { get; set; }
    public bool GoogleDriveLinked { get; set; }
    public int BoxesTransparencyPercent { get; set; } = 100;
}

