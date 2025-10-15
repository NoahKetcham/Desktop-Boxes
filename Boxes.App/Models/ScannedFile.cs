using System;

namespace Boxes.App.Models;

public class ScannedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ShortcutPath { get; set; }
    public bool IsArchived { get; set; }
    public string? ArchivedContentPath { get; set; }
}
