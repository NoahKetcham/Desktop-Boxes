using Avalonia.Controls;
using Avalonia.Interactivity;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialogViewModel ViewModel { get; } = new();

    public ConfirmationDialog()
    {
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.Closed += (_, result) => Close(result);
    }
}
