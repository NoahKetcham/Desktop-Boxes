using System;

namespace Boxes.App.Models;

public class DesktopBox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

