using System;
using System.Collections.Generic;

namespace Boxes.App.Models;

public class DesktopBox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public List<Guid> ShortcutIds { get; set; } = new();
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 240;
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }
    public string? CurrentPath { get; set; }
    public bool IsSnappedToTaskbar { get; set; }
    public bool IsCollapsed { get; set; }
    public double? ExpandedHeight { get; set; }
    public double? ExpandedPositionX { get; set; }
    public double? ExpandedPositionY { get; set; }
    public bool WasSnapExpanded { get; set; }
}

