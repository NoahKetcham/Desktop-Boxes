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

    [ObservableProperty]
    private bool isSelected;
}
