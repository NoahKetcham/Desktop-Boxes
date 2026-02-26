using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class NewBoxDialogViewModel : ViewModelBase
{
    private Window? _window;

    private CreateItemType _selectedType = CreateItemType.Box;
    public CreateItemType SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                UpdateCanCreate();
                OnPropertyChanged(nameof(CreateButtonText));
                OnPropertyChanged(nameof(CreateButtonToolTip));
                OnPropertyChanged(nameof(IsBoxSelected));
                OnPropertyChanged(nameof(IsNotepadSelected));
            }
        }
    }

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

    public string CreateButtonText => SelectedType == CreateItemType.Notepad ? "Create Notepad" : "Create Box";
    public string CreateButtonToolTip => SelectedType == CreateItemType.Notepad ? "Create a new notepad" : "Create the new box";
    public bool IsBoxSelected => SelectedType == CreateItemType.Box;
    public bool IsNotepadSelected => SelectedType == CreateItemType.Notepad;

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

        var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select target folder",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            TargetPath = path;
        }
    }

    private void Cancel()
    {
        _window?.Close(null);
    }

    private void Create()
    {
        if (SelectedType == CreateItemType.Notepad)
        {
            _window?.Close(new CreateItemResult
            {
                Type = CreateItemType.Notepad,
                Notepad = new Notepad { Name = Name.Trim().Length > 0 ? Name.Trim() : "Notepad" }
            });
            return;
        }

        var box = new DesktopBox
        {
            Name = Name.Trim(),
            Description = Description.Trim(),
            TargetPath = TargetPath.Trim()
        };

        _window?.Close(new CreateItemResult { Type = CreateItemType.Box, Box = box });
    }

    private void UpdateCanCreate()
    {
        var newValue = SelectedType == CreateItemType.Notepad || !string.IsNullOrWhiteSpace(Name);
        if (CanCreate != newValue)
        {
            CanCreate = newValue;
            (CreateCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}

