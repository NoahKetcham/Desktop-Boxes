using System;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels;

public partial class ShortcutSelectionItemViewModel : ViewModelBase
{
    public ShortcutSelectionItemViewModel(ScannedFile file, bool selected)
    {
        File = file;
        isSelected = selected;
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

    [ObservableProperty]
    private bool isSelected;
}
