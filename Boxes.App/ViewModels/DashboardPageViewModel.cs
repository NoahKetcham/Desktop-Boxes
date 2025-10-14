using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class DashboardPageViewModel : ViewModelBase
{
    public ObservableCollection<BoxSummaryViewModel> Boxes { get; } = new();
    public ObservableCollection<DesktopFileViewModel> ScannedFiles { get; } = new();

    [ObservableProperty]
    private BoxSummaryViewModel? selectedBox;

    [ObservableProperty]
    private bool hasScannedFiles;

    public IAsyncRelayCommand NewBoxCommand { get; }
    public IAsyncRelayCommand EditBoxCommand { get; }
    public IAsyncRelayCommand DeleteBoxCommand { get; }
    public IAsyncRelayCommand<BoxSummaryViewModel?> OpenBoxCommand { get; }
    public IAsyncRelayCommand ScanDesktopCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> RemoveScannedFileCommand { get; }

    public DashboardPageViewModel()
    {
        NewBoxCommand = new AsyncRelayCommand(CreateNewBoxAsync);
        EditBoxCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedBox != null);
        DeleteBoxCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedBox != null);
        OpenBoxCommand = new AsyncRelayCommand<BoxSummaryViewModel?>(OpenBoxAsync);
        ScanDesktopCommand = new AsyncRelayCommand(ScanDesktopAsync);
        RemoveScannedFileCommand = new RelayCommand<DesktopFileViewModel?>(RemoveScannedFile);

        ScannedFiles.CollectionChanged += OnScannedFilesCollectionChanged;

        _ = LoadAsync();
    }

    partial void OnSelectedBoxChanged(BoxSummaryViewModel? value)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            EditBoxCommand.NotifyCanExecuteChanged();
            DeleteBoxCommand.NotifyCanExecuteChanged();
        }
        else
        {
            _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                EditBoxCommand.NotifyCanExecuteChanged();
                DeleteBoxCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private void OnScannedFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasScannedFiles = ScannedFiles.Count > 0;
    }

    private async Task LoadAsync()
    {
        await AppServices.BoxService.InitializeAsync().ConfigureAwait(false);
        var boxes = await AppServices.BoxService.GetBoxesAsync();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Boxes.Clear();
            foreach (var box in boxes)
            {
                Boxes.Add(BoxSummaryViewModel.FromModel(box));
            }

            SelectedBox = Boxes.FirstOrDefault();
        });

        foreach (var box in boxes)
        {
            await AppServices.BoxWindowManager.ShowAsync(box);
        }
    }

    private async Task CreateNewBoxAsync()
    {
        var model = await DialogService.ShowNewBoxDialogAsync().ConfigureAwait(false);
        if (model is null)
        {
            return;
        }

        var persisted = await AppServices.BoxService.AddOrUpdateAsync(model);
        await AppServices.BoxWindowManager.ShowAsync(persisted);
        var viewModel = BoxSummaryViewModel.FromModel(persisted);
        Boxes.Add(viewModel);
        SelectedBox = viewModel;
    }

    private async Task OpenBoxAsync(BoxSummaryViewModel? viewModel)
    {
        var target = viewModel ?? SelectedBox;
        if (target is null)
        {
            return;
        }

        await AppServices.BoxWindowManager.ShowAsync(target.ToModel());
    }

    private async Task EditSelectedAsync()
    {
        if (SelectedBox == null)
        {
            return;
        }

        var updated = await AppServices.BoxService.AddOrUpdateAsync(SelectedBox.ToModel());
        await AppServices.BoxWindowManager.UpdateAsync(updated);
        SelectedBox.UpdateFromModel(updated);
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedBox == null)
        {
            return;
        }

        var confirmed = await DialogService.ShowDeleteConfirmationAsync(SelectedBox.Name);
        if (!confirmed)
        {
            return;
        }

        var toRemove = SelectedBox;
        await AppServices.BoxService.DeleteAsync(toRemove.Id);
        await AppServices.BoxWindowManager.CloseAsync(toRemove.Id);
        Boxes.Remove(toRemove);
        SelectedBox = Boxes.FirstOrDefault();
    }

    private async Task ScanDesktopAsync()
    {
        var files = await AppServices.ScannedFileService.ScanAndSaveAsync();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            ScannedFiles.Clear();
            foreach (var file in files)
            {
                ScannedFiles.Add(new DesktopFileViewModel(file));
            }
            HasScannedFiles = ScannedFiles.Count > 0;
        });
    }

    private void RemoveScannedFile(DesktopFileViewModel? file)
    {
        if (file is null)
        {
            return;
        }

        if (ScannedFiles.Contains(file))
        {
            ScannedFiles.Remove(file);
        }
    }

    public async Task CreateShortcutsAsync(string destinationFolder)
    {
        var files = ScannedFiles
            .Select(f => new ScannedFile { FileName = f.FileName, FilePath = f.FilePath })
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        await AppServices.ScannedFileService.CreateShortcutsAsync(files, destinationFolder);
    }
}

