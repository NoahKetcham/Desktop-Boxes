using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Boxes.App.Views;
using Boxes.App.ViewModels;
using Boxes.App.Views.Dialogs;
using System.Collections.Generic;

namespace Boxes.App.ViewModels;

public partial class DashboardPageViewModel : ViewModelBase
{
    public ObservableCollection<BoxSummaryViewModel> Boxes { get; } = new();
    public ObservableCollection<DesktopFileViewModel> ScannedFiles { get; } = new();

    [ObservableProperty]
    private BoxSummaryViewModel? selectedBox;

    [ObservableProperty]
    private bool hasScannedFiles;

    [ObservableProperty]
    private bool isDesktopClean;

    [ObservableProperty]
    private bool isCleaningDesktop;

    public string DesktopCleanupButtonText => IsDesktopClean ? "Restore Desktop" : "Clean Desktop";
    public bool CanToggleDesktopCleanup => !IsCleaningDesktop;

    public IAsyncRelayCommand NewBoxCommand { get; }
    public IAsyncRelayCommand EditBoxCommand { get; }
    public IAsyncRelayCommand DeleteBoxCommand { get; }
    public IAsyncRelayCommand<BoxSummaryViewModel?> OpenBoxCommand { get; }
    public IAsyncRelayCommand ScanDesktopCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> RemoveScannedFileCommand { get; }
    public IAsyncRelayCommand<BoxSummaryViewModel?> ConfigureShortcutsCommand { get; }
    public IAsyncRelayCommand ToggleDesktopCleanupCommand { get; }

    public DashboardPageViewModel()
    {
        NewBoxCommand = new AsyncRelayCommand(CreateNewBoxAsync);
        EditBoxCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedBox != null);
        DeleteBoxCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedBox != null);
        OpenBoxCommand = new AsyncRelayCommand<BoxSummaryViewModel?>(OpenBoxAsync);
        ScanDesktopCommand = new AsyncRelayCommand(ScanDesktopAsync);
        RemoveScannedFileCommand = new RelayCommand<DesktopFileViewModel?>(RemoveScannedFile);
        ConfigureShortcutsCommand = new AsyncRelayCommand<BoxSummaryViewModel?>(ConfigureShortcutsAsync);
        ToggleDesktopCleanupCommand = new AsyncRelayCommand(ToggleDesktopCleanupAsync);

        ScannedFiles.CollectionChanged += OnScannedFilesCollectionChanged;

        _ = InitializeAsync();
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

    partial void OnIsDesktopCleanChanged(bool value)
    {
        OnPropertyChanged(nameof(DesktopCleanupButtonText));
    }

    partial void OnIsCleaningDesktopChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggleDesktopCleanup));
    }

    private void OnScannedFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasScannedFiles = ScannedFiles.Count > 0;
    }

    private async Task InitializeAsync()
    {
        await LoadAsync();
        IsDesktopClean = await AppServices.DesktopCleanupService.IsDesktopCleanAsync().ConfigureAwait(false);
        OnPropertyChanged(nameof(DesktopCleanupButtonText));
        OnPropertyChanged(nameof(CanToggleDesktopCleanup));
    }

    private async Task ToggleDesktopCleanupAsync()
    {
        if (IsCleaningDesktop)
        {
            return;
        }

        try
        {
            IsCleaningDesktop = true;
            if (!IsDesktopClean)
            {
                var movedItems = await AppServices.DesktopCleanupService.CleanAsync();
                if (movedItems.Count > 0)
                {
                    await AppServices.ScannedFileService.MarkFilesAsArchivedAsync(movedItems.Select(item => (item.OriginalPath, item.ArchiveLocation)));
                    IsDesktopClean = true;
                }
            }
            else
            {
                var restored = await AppServices.DesktopCleanupService.RestoreAsync();
                if (restored)
                {
                    await AppServices.ScannedFileService.MarkFilesAsRestoredAsync();
                    IsDesktopClean = false;
                }
            }

            await ScanDesktopAsync();
            var boxes = await AppServices.BoxService.GetBoxesAsync();
            foreach (var box in boxes)
            {
                await AppServices.BoxWindowManager.UpdateAsync(box);
            }
        }
        finally
        {
            IsCleaningDesktop = false;
        }
    }

    private async Task MarkFilesAsRestoredAsync()
    {
        await AppServices.ScannedFileService.MarkFilesAsRestoredAsync();
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
        var storedShortcuts = await AppServices.ScannedFileService.GetStoredShortcutsAsync();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            ScannedFiles.Clear();
            foreach (var file in files)
            {
                var shortcut = storedShortcuts.FirstOrDefault(s => s.Id == file.Id);
                if (shortcut != null)
                {
                    file.ShortcutPath = Path.Combine(AppServices.ScannedFileService.ShortcutArchiveDirectory, shortcut.Id.ToString("N") + ".lnk");
                    file.IsArchived = true;
                }

                ScannedFiles.Add(new DesktopFileViewModel(file));
            }

            // Include any archived shortcuts that were not part of the current desktop scan (e.g., cleaned files)
            foreach (var shortcut in storedShortcuts)
            {
                if (files.Any(f => f.Id == shortcut.Id))
                {
                    continue;
                }

                var archived = new ScannedFile
                {
                    Id = shortcut.Id,
                    FileName = shortcut.FileName,
                    FilePath = shortcut.TargetPath,
                    ShortcutPath = Path.Combine(AppServices.ScannedFileService.ShortcutArchiveDirectory, shortcut.Id.ToString("N") + ".lnk"),
                    IsArchived = true
                };
                ScannedFiles.Add(new DesktopFileViewModel(archived));
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

    private async Task ConfigureShortcutsAsync(BoxSummaryViewModel? box)
    {
        box ??= SelectedBox;
        if (box is null)
        {
            return;
        }

        var scannedFiles = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var viewModel = new ShortcutSelectionViewModel(box.Name, scannedFiles, box.ShortcutIds);
        var dialog = new ShortcutSelectionDialog(viewModel);

        var owner = AppServices.MainWindowOwner;
        if (owner is null)
        {
            return;
        }

        var result = await dialog.ShowAsync(owner);
        if (result is null)
        {
            return;
        }

        box.ShortcutIds = result.Select(s => s.Id).ToList();
        box.ItemCount = box.ShortcutIds.Count;

        var updated = await AppServices.BoxService.AddOrUpdateAsync(box.ToModel());
        await AppServices.BoxWindowManager.UpdateAsync(updated);
        box.UpdateFromModel(updated);
    }

    public async Task CreateShortcutsAsync()
    {
        var files = ScannedFiles
            .Select(f => new ScannedFile { Id = f.Id, FileName = f.FileName, FilePath = f.FilePath, ShortcutPath = f.ShortcutPath, IsArchived = f.ShortcutPath != null })
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        await AppServices.ScannedFileService.CreateShortcutsAsync(files);
        await ScanDesktopAsync();
    }
}

