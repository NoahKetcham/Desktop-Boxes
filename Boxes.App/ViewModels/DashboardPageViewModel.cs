using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
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
    public ObservableCollection<DesktopFileViewModel> CurrentScannedItems { get; } = new();
    public ObservableCollection<DesktopFileViewModel> ScanNavigationStack { get; } = new();

    public string CurrentScanPath => ScanNavigationStack.Count == 0
        ? "Desktop"
        : string.Join(" / ", ScanNavigationStack.Select(f => f.FileName));

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
    public IRelayCommand<DesktopFileViewModel?> EnterFolderCommand { get; }
    public IRelayCommand NavigateUpCommand { get; }
    public IRelayCommand NavigateHomeCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> RemoveScannedFileCommand { get; }
    public IAsyncRelayCommand<BoxSummaryViewModel?> ConfigureBoxSettingsCommand { get; }
    public IAsyncRelayCommand ToggleDesktopCleanupCommand { get; }
    public IAsyncRelayCommand CreateShortcutsCommand { get; }

    public DashboardPageViewModel()
    {
        NewBoxCommand = new AsyncRelayCommand(CreateNewBoxAsync);
        EditBoxCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedBox != null);
        DeleteBoxCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedBox != null);
        OpenBoxCommand = new AsyncRelayCommand<BoxSummaryViewModel?>(OpenBoxAsync);
        ScanDesktopCommand = new AsyncRelayCommand(ScanDesktopAsync);
        RemoveScannedFileCommand = new RelayCommand<DesktopFileViewModel?>(RemoveScannedFile);
        ConfigureBoxSettingsCommand = new AsyncRelayCommand<BoxSummaryViewModel?>(ConfigureBoxSettingsAsync);
        ToggleDesktopCleanupCommand = new AsyncRelayCommand(ToggleDesktopCleanupAsync);
        EnterFolderCommand = new RelayCommand<DesktopFileViewModel?>(EnterFolder);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => ScanNavigationStack.Count > 0);
        NavigateHomeCommand = new RelayCommand(NavigateHome, () => ScanNavigationStack.Count > 0);
        CreateShortcutsCommand = new AsyncRelayCommand(CreateShortcutsAsync, () => CurrentScannedItems.Count > 0);

        ScannedFiles.CollectionChanged += OnScannedFilesCollectionChanged;

        _ = InitializeAsync();
        AppServices.BoxUpdated += OnBoxUpdated;
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
        CreateShortcutsCommand.NotifyCanExecuteChanged();
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

        var latest = await AppServices.BoxService.GetBoxAsync(target.Id).ConfigureAwait(false);
        if (latest == null)
        {
            latest = target.ToModel();
        }

        await AppServices.BoxWindowManager.ShowAsync(latest).ConfigureAwait(false);
    }

    private async Task EditSelectedAsync()
    {
        if (SelectedBox == null)
        {
            return;
        }

        var model = SelectedBox.ToModel();
        var updated = await AppServices.BoxService.AddOrUpdateAsync(model);
        await AppServices.BoxWindowManager.UpdateAsync(updated);
        SelectedBox.UpdateFromModel(updated);
    }

    private async void OnBoxUpdated(object? sender, DesktopBox box)
    {
        var target = Boxes.FirstOrDefault(b => b.Id == box.Id);
        if (target is null)
        {
            return;
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            target.UpdateFromModel(box);
        });
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
                    file.ParentId = shortcut.ParentId;
                    file.ItemType = shortcut.ItemType;
                }

                ScannedFiles.Add(new DesktopFileViewModel(file));
            }

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
                    IsArchived = true,
                    ParentId = shortcut.ParentId,
                    ItemType = shortcut.ItemType
                };
                ScannedFiles.Add(new DesktopFileViewModel(archived));
            }

            UpdateCurrentScannedItems(null);
            HasScannedFiles = ScannedFiles.Count > 0;
        });
    }

    private void RemoveScannedFile(DesktopFileViewModel? file)
    {
        if (file is null)
        {
            return;
        }

        if (!ScannedFiles.Contains(file))
        {
            return;
        }

        ScannedFiles.Remove(file);
        if (ScanNavigationStack.Contains(file))
        {
            ScanNavigationStack.Remove(file);
        }

        var currentParent = ScanNavigationStack.LastOrDefault();
        UpdateCurrentScannedItems(currentParent?.Id);
    }

    private void EnterFolder(DesktopFileViewModel? folder)
    {
        if (folder is null || folder.ItemType != ScannedItemType.Folder)
        {
            return;
        }

        ScanNavigationStack.Add(folder);
        UpdateCurrentScannedItems(folder.Id);
    }

    private void NavigateUp()
    {
        if (ScanNavigationStack.Count == 0)
        {
            return;
        }

        ScanNavigationStack.RemoveAt(ScanNavigationStack.Count - 1);
        var parent = ScanNavigationStack.LastOrDefault();
        UpdateCurrentScannedItems(parent?.Id);
    }

    private void NavigateHome()
    {
        ScanNavigationStack.Clear();
        UpdateCurrentScannedItems(null);
    }

    private void UpdateCurrentScannedItems(Guid? parentId)
    {
        CurrentScannedItems.Clear();
        foreach (var item in ScannedFiles.Where(f => f.ParentId == parentId))
        {
            CurrentScannedItems.Add(item);
        }
        CreateShortcutsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentScanPath));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfigureBoxSettingsAsync(BoxSummaryViewModel? box)
    {
        box ??= SelectedBox;
        if (box is null)
        {
            return;
        }

        var scannedFiles = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var viewModel = new ShortcutSelectionViewModel(box.Id, box.Name, scannedFiles, box.ShortcutIds);
        var dialog = new ShortcutSelectionDialog(viewModel);

        dialog.SnapRequested += async (_, _) =>
        {
            await AppServices.BoxWindowManager.SnapToTaskbarAsync(box.Id).ConfigureAwait(false);
        };

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
        var files = CurrentScannedItems
            .Where(f => !f.IsFolder)
            .Select(f => new ScannedFile
            {
                Id = f.Id,
                FileName = f.FileName,
                FilePath = f.FilePath,
                ParentId = f.ParentId,
                ItemType = f.ItemType,
                ShortcutPath = f.ShortcutPath,
                IsArchived = f.ShortcutPath != null
            })
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        await AppServices.ScannedFileService.CreateShortcutsAsync(files);
        await ScanDesktopAsync();
    }
}

