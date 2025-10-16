using System;

namespace Boxes.App.Models;

public enum ScannedItemType
{
    File,
    Folder,
    Shortcut
}

public class ScannedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ScannedItemType ItemType { get; set; } = ScannedItemType.File;
    public Guid? ParentId { get; set; }
    public string? ShortcutPath { get; set; }
    public bool IsArchived { get; set; }
    public string? ArchivedContentPath { get; set; }
    public Guid? RootId { get; set; }
}
