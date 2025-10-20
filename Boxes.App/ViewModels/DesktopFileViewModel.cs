using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Boxes.App.Models;
using Boxes.App.Services;

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
    public Bitmap? Icon
    {
        get => _icon;
        private set
        {
            if (_icon == value)
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
        }
    }

    private Bitmap? _icon;

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
        _ = LoadIconAsync();
    }

    internal void UpdateFromModel(ScannedFile model)
    {
        ShortcutPath = model.ShortcutPath;
        IsArchived = model.IsArchived;
        ArchivedContentPath = model.ArchivedContentPath;
        OnPropertyChanged(nameof(ShortcutPath));
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(ArchivedContentPath));
        _ = LoadIconAsync();
    }

    private async Task LoadIconAsync()
    {
        try
        {
            var iconService = AppServices.ShellIconService;
            string? pathToResolve = ShortcutPath ?? ArchivedContentPath ?? FilePath;

            var fileExists = !string.IsNullOrWhiteSpace(pathToResolve) && (System.IO.File.Exists(pathToResolve) || System.IO.Directory.Exists(pathToResolve));
            if (!fileExists)
            {
                pathToResolve = FilePath;
            }

            var icon = await iconService.GetIconAsync(pathToResolve, ItemType == ScannedItemType.Folder);
            Icon = icon;
        }
        catch
        {
            Icon = null;
        }
    }

    public async Task RefreshIconAsync()
    {
        Icon = null;
        await LoadIconAsync().ConfigureAwait(false);
    }

    public void SetShortcutPath(string? path)
    {
        ShortcutPath = path;
        OnPropertyChanged(nameof(ShortcutPath));
        _ = LoadIconAsync();
    }

    public void SetArchivedState(bool isArchived, string? archivedContentPath)
    {
        IsArchived = isArchived;
        ArchivedContentPath = archivedContentPath;
        OnPropertyChanged(nameof(IsArchived));
        OnPropertyChanged(nameof(ArchivedContentPath));
        _ = LoadIconAsync();
    }
}
