using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class ConfirmDeleteDialogViewModel : ViewModelBase
{
    private Window? _window;

    private string _boxName = string.Empty;
    public string BoxName
    {
        get => _boxName;
        set
        {
            if (SetProperty(ref _boxName, value))
            {
                Message = $"Delete '{_boxName}'? Files in the target folder remain untouched.";
            }
        }
    }

    private string _message = string.Empty;
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ConfirmCommand { get; }

    public ConfirmDeleteDialogViewModel()
    {
        CancelCommand = new RelayCommand(Cancel);
        ConfirmCommand = new RelayCommand(Confirm);
    }

    public void SetWindow(Window window)
    {
        _window = window;
    }

    private void Cancel()
    {
        _window?.Close(false);
    }

    private void Confirm()
    {
        _window?.Close(true);
    }
}

