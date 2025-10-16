using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Boxes.App.Models;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class ShortcutSelectionDialog : Window
{
    public ShortcutSelectionViewModel ViewModel { get; }

    public ShortcutSelectionDialog(ShortcutSelectionViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        ViewModel.CloseRequested += (_, result) => Close(result);
    }

    private void ShortcutItem_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ToggleButton toggle && toggle.DataContext is ShortcutSelectionItemViewModel item)
        {
            if (item.File.ItemType == ScannedItemType.Folder)
            {
                ViewModel.EnterFolderCommand.Execute(item);
            }
            else
            {
                item.IsSelected = !item.IsSelected;
            }
        }
    }

    public async Task<IReadOnlyList<ScannedFile>?> ShowAsync(Window owner)
    {
        var result = await ShowDialog<bool?>(owner);
        if (result is true)
        {
            return ViewModel.GetSelectedFiles().ToList();
        }

        return null;
    }
}
