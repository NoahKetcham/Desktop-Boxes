using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels;

public partial class ShortcutSelectionItemViewModel : ViewModelBase
{
    public ShortcutSelectionItemViewModel(ScannedFile file, bool selected)
    {
        File = file;
        isSelected = selected;
        _ = LoadIconAsync();
    }

    public ScannedFile File { get; }

    public string FileName => File.FileName;
    public bool IsFolder => File.ItemType == ScannedItemType.Folder;
    public string TypeLabel => File.ItemType switch
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

    [ObservableProperty]
    private bool isSelected;

    private async Task LoadIconAsync()
    {
        try
        {
            var iconService = AppServices.ShellIconService;
            // For shortcuts, use FilePath (target) instead of ShortcutPath (.lnk file) to get the actual icon without overlay
            string? pathToResolve = File.ItemType == ScannedItemType.Shortcut 
                ? File.FilePath 
                : (File.ShortcutPath ?? File.ArchivedContentPath ?? File.FilePath);

            var fileExists = !string.IsNullOrWhiteSpace(pathToResolve) && (System.IO.File.Exists(pathToResolve) || System.IO.Directory.Exists(pathToResolve));
            if (!fileExists)
            {
                pathToResolve = File.FilePath;
            }

            var icon = await iconService.GetIconAsync(pathToResolve, File.ItemType == ScannedItemType.Folder);
            Icon = icon;
        }
        catch
        {
            Icon = null;
        }
    }
}
