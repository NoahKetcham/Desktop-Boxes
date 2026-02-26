using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class InputDialogViewModel : ViewModelBase
{
    private Window? _window;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private bool _canConfirm;

    public string Title { get; set; } = "Input";
    public string Prompt { get; set; } = "Enter value:";
    public string Watermark { get; set; } = "";
    public string ConfirmButtonText { get; set; } = "OK";

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ConfirmCommand { get; }

    public InputDialogViewModel()
    {
        CancelCommand = new RelayCommand(Cancel);
        ConfirmCommand = new RelayCommand(Confirm, () => CanConfirm);
    }

    public void SetWindow(Window window)
    {
        _window = window;
    }

    partial void OnValueChanged(string value)
    {
        CanConfirm = !string.IsNullOrWhiteSpace(value);
        (ConfirmCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void Cancel()
    {
        _window?.Close(null);
    }

    private void Confirm()
    {
        _window?.Close(Value.Trim());
    }
}
