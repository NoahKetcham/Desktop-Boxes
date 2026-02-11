using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
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
    public ObservableCollection<DesktopFileViewModel> CurrentScannedItems { get; } = new();
    public ObservableCollection<DesktopFileViewModel> ScanNavigationStack { get; } = new();
    public ObservableCollection<DesktopBuildViewModel> DesktopBuilds { get; } = new();
    public ObservableCollection<BoxTemplateOptionViewModel> AvailableTemplates { get; } = new();

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

    /// <summary>
    /// The sidebar viewmodel that displays box settings in the right panel on the Dashboard page.
    /// Injected by MainWindowViewModel.
    /// </summary>
    public DashboardBoxSettingsViewModel? BoxSettingsHost { get; set; }

    public int ScannedFilesCount => ScannedFiles.Count;

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
    public IAsyncRelayCommand SaveDesktopBuildCommand { get; }
    public IAsyncRelayCommand<DesktopBuildViewModel?> RestoreDesktopBuildCommand { get; }
    public IAsyncRelayCommand<DesktopBuildViewModel?> DeleteDesktopBuildCommand { get; }
    public IRelayCommand<BoxSummaryViewModel?> ToggleTemplatesForBoxCommand { get; }
    public IAsyncRelayCommand<BoxTemplateOptionViewModel?> SelectTemplateForSelectedBoxCommand { get; }
    public IRelayCommand<BoxTemplateOptionViewModel?> ShowTemplateInfoCommand { get; }

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
        SaveDesktopBuildCommand = new AsyncRelayCommand(SaveDesktopBuildAsync);
        RestoreDesktopBuildCommand = new AsyncRelayCommand<DesktopBuildViewModel?>(RestoreDesktopBuildAsync);
        DeleteDesktopBuildCommand = new AsyncRelayCommand<DesktopBuildViewModel?>(DeleteDesktopBuildAsync);
        ToggleTemplatesForBoxCommand = new RelayCommand<BoxSummaryViewModel?>(ToggleTemplatesForBox);
        SelectTemplateForSelectedBoxCommand = new AsyncRelayCommand<BoxTemplateOptionViewModel?>(SelectTemplateForSelectedBoxAsync);
        ShowTemplateInfoCommand = new RelayCommand<BoxTemplateOptionViewModel?>(ShowTemplateInfo);

        ScannedFiles.CollectionChanged += OnScannedFilesCollectionChanged;

        SeedAvailableTemplates();
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
        OnPropertyChanged(nameof(ScannedFilesCount));
        CreateShortcutsCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeAsync()
    {
        await LoadAsync();
        await LoadDesktopBuildsAsync();
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
                await AppServices.DesktopCleanupService.CleanAsync();
                IsDesktopClean = true;
            }
            else
            {
                await AppServices.DesktopCleanupService.RestoreAsync();
                IsDesktopClean = false;
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
        await AppServices.WindowStateService.DeleteAsync(toRemove.Id);
        await AppServices.BoxWindowManager.CloseAsync(toRemove.Id, persistState: false);
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

    private Task ConfigureBoxSettingsAsync(BoxSummaryViewModel? box)
    {
        // NOTE: The old shortcut-selection dialog (ShortcutSelectionDialog/ShortcutSelectionViewModel)
        // is deprecated. Box settings are now displayed in the right-side panel sidebar.
        // The old dialog may be reintroduced later for advanced shortcut management.

        var target = box ?? SelectedBox;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        BoxSettingsHost?.LoadBox(target);
        return Task.CompletedTask;
    }

    private void ToggleTemplatesForBox(BoxSummaryViewModel? box)
    {
        if (box is null)
        {
            return;
        }

        var willExpand = !box.IsTemplatesExpanded;
        foreach (var b in Boxes)
        {
            b.IsTemplatesExpanded = false;
        }

        box.IsTemplatesExpanded = willExpand;
        SelectedBox = box;
    }

    private async Task SelectTemplateForSelectedBoxAsync(BoxTemplateOptionViewModel? template)
    {
        if (template is null || SelectedBox is null)
        {
            return;
        }

        // Fetch the latest persisted model so we don't accidentally overwrite other fields we don't surface in the summary VM.
        var latest = await AppServices.BoxService.GetBoxAsync(SelectedBox.Id).ConfigureAwait(false);
        if (latest is null)
        {
            latest = SelectedBox.ToModel();
        }

        latest.TemplateKey = template.Key;
        var updated = await AppServices.BoxService.AddOrUpdateAsync(latest).ConfigureAwait(false);
        await AppServices.BoxWindowManager.UpdateAsync(updated).ConfigureAwait(false);

        SelectedBox.TemplateKey = template.Key;
    }

    private void ShowTemplateInfo(BoxTemplateOptionViewModel? template)
    {
        if (template is null || SelectedBox is null)
        {
            return;
        }

        // Make sure we have a current box loaded, then swap the sidebar into template-info mode.
        BoxSettingsHost?.LoadBox(SelectedBox);
        BoxSettingsHost?.ShowTemplateInfo(template);
    }

    private void SeedAvailableTemplates()
    {
        AvailableTemplates.Clear();
        AvailableTemplates.Add(new BoxTemplateOptionViewModel(
            key: "minimal",
            name: "Minimal",
            shortDescription: "A clean, uncluttered look.",
            longDescription: "Minimal template focuses on content with reduced chrome. Placeholder details for Task 2."));

        AvailableTemplates.Add(new BoxTemplateOptionViewModel(
            key: "compactGrid",
            name: "Compact Grid",
            shortDescription: "Smaller spacing, denser layout.",
            longDescription: "Compact Grid packs more shortcuts into the same space. Placeholder details for Task 2."));

        AvailableTemplates.Add(new BoxTemplateOptionViewModel(
            key: "headerPlusBadges",
            name: "Header + Badges",
            shortDescription: "Emphasized header and metadata badges.",
            longDescription: "Header + Badges highlights section header and adds more at-a-glance info. Placeholder details for Task 2."));
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

    private async Task LoadDesktopBuildsAsync()
    {
        var builds = await AppServices.DesktopBuildService.GetAllAsync();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            DesktopBuilds.Clear();
            foreach (var build in builds)
            {
                DesktopBuilds.Add(DesktopBuildViewModel.FromModel(build));
            }
        });
    }

    private async Task SaveDesktopBuildAsync()
    {
        var name = await DialogService.ShowDesktopBuildNameDialogAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var boxes = await AppServices.BoxService.GetBoxesAsync();
        await AppServices.DesktopBuildService.SaveAsync(name, boxes);
        await LoadDesktopBuildsAsync();
    }

    private async Task RestoreDesktopBuildAsync(DesktopBuildViewModel? buildViewModel)
    {
        if (buildViewModel == null)
        {
            return;
        }

        var confirmed = await DialogService.ShowConfirmationAsync($"Restore desktop build '{buildViewModel.Name}'? This will replace your current desktop configuration.");
        if (!confirmed)
        {
            return;
        }

        var build = await AppServices.DesktopBuildService.GetAsync(buildViewModel.Id);
        if (build == null)
        {
            return;
        }

        // Close all existing windows
        await AppServices.BoxWindowManager.CloseAllWindowsAsync();

        // Clear existing boxes
        var existingBoxes = await AppServices.BoxService.GetBoxesAsync();
        foreach (var box in existingBoxes)
        {
            await AppServices.BoxService.DeleteAsync(box.Id);
        }

        // Restore boxes from build
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            Boxes.Clear();
        });

        foreach (var box in build.Boxes)
        {
            var restoredBox = await AppServices.BoxService.AddOrUpdateAsync(box);
            await AppServices.BoxWindowManager.ShowAsync(restoredBox);
            var viewModel = BoxSummaryViewModel.FromModel(restoredBox);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Boxes.Add(viewModel);
            });
        }

        SelectedBox = Boxes.FirstOrDefault();
    }

    private async Task DeleteDesktopBuildAsync(DesktopBuildViewModel? buildViewModel)
    {
        if (buildViewModel == null)
        {
            return;
        }

        var confirmed = await DialogService.ShowDeleteConfirmationAsync(buildViewModel.Name);
        if (!confirmed)
        {
            return;
        }

        await AppServices.DesktopBuildService.DeleteAsync(buildViewModel.Id);
        await LoadDesktopBuildsAsync();
    }
}

