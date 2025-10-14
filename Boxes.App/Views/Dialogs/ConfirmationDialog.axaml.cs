using Avalonia.Controls;
using Avalonia.Interactivity;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class ConfirmationDialog : Window
{
    private readonly ConfirmationDialogViewModel _viewModel = new();

    public ConfirmationDialog()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Closed += (_, result) => Close(result);
    }

    public string Message
    {
        get => _viewModel.Message;
        set => _viewModel.Message = value;
    }
}
