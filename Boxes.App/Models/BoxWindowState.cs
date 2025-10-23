using System;

namespace Boxes.App.Models;

public enum WindowMode
{
    Normal,
    Taskbar
}

public class BoxWindowState
{
    public Guid BoxId { get; set; }
    public WindowMode Mode { get; set; } = WindowMode.Normal;

    public double Width { get; set; }
    public double Height { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    // Taskbar mode presentation
    public bool IsCollapsed { get; set; } = true;
    public double ExpandedHeight { get; set; } = 240;
    public double ExpandedPosX { get; set; }
    public double ExpandedPosY { get; set; }

    // Last normal window placement
    public double NormalWidth { get; set; }
    public double NormalHeight { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public string? CurrentPath { get; set; }
}


