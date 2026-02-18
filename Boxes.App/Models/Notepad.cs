using System;

namespace Boxes.App.Models;

public class Notepad
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
