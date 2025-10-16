using System;
using System.IO;
using Boxes.App.Models;

namespace Boxes.App.ViewModels;

public class DesktopFileViewModel : ViewModelBase
{
    public Guid Id { get; }
    public string FilePath { get; }
    public string FileName { get; }
    public string? ShortcutPath { get; private set; }
    public bool IsArchived { get; private set; }
    public string? ArchivedContentPath { get; private set; }
    public ScannedItemType ItemType { get; }
    public Guid? ParentId { get; }
    public bool IsFolder => ItemType == ScannedItemType.Folder;
    public bool IsFile => ItemType == ScannedItemType.File;
    public string DisplayName => FileName;
    public string TypeLabel => ItemType switch
    {
        ScannedItemType.Folder => "Folder",
        ScannedItemType.Shortcut => "Shortcut",
        _ => "File"
    };

    public DesktopFileViewModel(ScannedFile model)
    {
        Id = model.Id;
        FilePath = model.FilePath;
        FileName = model.FileName;
        ShortcutPath = model.ShortcutPath;
        IsArchived = model.IsArchived;
        ArchivedContentPath = model.ArchivedContentPath;
        ItemType = model.ItemType;
        ParentId = model.ParentId;
    }

    public void SetShortcutPath(string? path)
    {
        ShortcutPath = path;
        OnPropertyChanged(nameof(ShortcutPath));
    }

    public void SetArchivedState(bool isArchived, string? archivedContentPath)
    {
        IsArchived = isArchived;
        ArchivedContentPath = archivedContentPath;
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(ArchivedContentPath));
    }
}
