using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class DesktopBuildNameDialogViewModel : ViewModelBase
{
    private Window? _window;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private bool canSave;

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand SaveCommand { get; }

    public DesktopBuildNameDialogViewModel()
    {
        CancelCommand = new RelayCommand(Cancel);
        SaveCommand = new RelayCommand(Save, () => CanSave);
    }

    public void SetWindow(Window window)
    {
        _window = window;
    }

    partial void OnNameChanged(string value)
    {
        CanSave = !string.IsNullOrWhiteSpace(value);
        (SaveCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void Cancel()
    {
        _window?.Close(null);
    }

    private void Save()
    {
        _window?.Close(Name.Trim());
    }
}

