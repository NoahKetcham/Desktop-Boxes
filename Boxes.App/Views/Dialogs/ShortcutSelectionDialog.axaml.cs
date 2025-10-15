using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
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
