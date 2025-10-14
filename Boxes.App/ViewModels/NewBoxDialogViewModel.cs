using System.Threading.Tasks;
using Avalonia.Controls;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class NewBoxDialogViewModel : ViewModelBase
{
    private Window? _window;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                UpdateCanCreate();
            }
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _targetPath = string.Empty;
    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    private bool _canCreate;
    public bool CanCreate
    {
        get => _canCreate;
        private set => SetProperty(ref _canCreate, value);
    }

    public IAsyncRelayCommand BrowseCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CreateCommand { get; }

    public NewBoxDialogViewModel()
    {
        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        CancelCommand = new RelayCommand(Cancel);
        CreateCommand = new RelayCommand(Create, () => CanCreate);
    }

    public void SetWindow(Window window)
    {
        _window = window;
    }

    private async Task BrowseAsync()
    {
        if (_window == null)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Select target folder"
        };

        var result = await dialog.ShowAsync(_window);
        if (!string.IsNullOrWhiteSpace(result))
        {
            TargetPath = result;
        }
    }

    private void Cancel()
    {
        _window?.Close(null);
    }

    private void Create()
    {
        var box = new DesktopBox
        {
            Name = Name.Trim(),
            Description = Description.Trim(),
            TargetPath = TargetPath.Trim()
        };

        _window?.Close(box);
    }

    private void UpdateCanCreate()
    {
        var newValue = !string.IsNullOrWhiteSpace(Name);
        if (CanCreate != newValue)
        {
            CanCreate = newValue;
            (CreateCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}

