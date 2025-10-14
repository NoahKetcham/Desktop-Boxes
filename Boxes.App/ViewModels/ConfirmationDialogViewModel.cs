using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Boxes.App.ViewModels;

public partial class ConfirmationDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string message = string.Empty;

    public event EventHandler<bool>? Closed;

    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public ConfirmationDialogViewModel()
    {
        ConfirmCommand = new RelayCommand(() => Closed?.Invoke(this, true));
        CancelCommand = new RelayCommand(() => Closed?.Invoke(this, false));
    }
}
