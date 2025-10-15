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

    public DesktopFileViewModel(ScannedFile model)
    {
        Id = model.Id;
        FilePath = model.FilePath;
        FileName = model.FileName;
        ShortcutPath = model.ShortcutPath;
        IsArchived = model.IsArchived;
        ArchivedContentPath = model.ArchivedContentPath;
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
