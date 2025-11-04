using System;
using System.Collections.Generic;

namespace Boxes.App.Models;

public class DesktopBuild
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<DesktopBox> Boxes { get; set; } = new();
}

