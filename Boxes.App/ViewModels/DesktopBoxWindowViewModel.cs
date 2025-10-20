using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public class DesktopBoxWindowViewModel : ViewModelBase
{
    public DesktopBox Model { get; private set; }

    public ObservableCollection<DesktopFileViewModel> Shortcuts { get; } = new();
    public ObservableCollection<DesktopFileViewModel> CurrentItems { get; } = new();
    public ObservableCollection<DesktopFileViewModel> NavigationStack { get; } = new();

    public IRelayCommand<DesktopFileViewModel?> LaunchShortcutCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> EnterFolderCommand { get; }
    public IRelayCommand NavigateUpCommand { get; }
    public IRelayCommand NavigateHomeCommand { get; }

    public string CurrentPath => NavigationStack.Count == 0
        ? "Desktop"
        : string.Join(" / ", NavigationStack.Select(f => f.FileName));

    private string SerializedPath => NavigationStack.Count == 0
        ? "Desktop"
        : string.Join(" > ", NavigationStack.Select(f => f.FileName));

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name != value)
            {
                Model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => Model.Description;
        set
        {
            if (Model.Description != value)
            {
                Model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public RelayCommand CloseCommand { get; }
    public event EventHandler? RequestClose;

    public event EventHandler? RequestStateSave;

    public DesktopBoxWindowViewModel(DesktopBox model)
    {
        Model = model;
        CloseCommand = new RelayCommand(() =>
        {
            RequestStateSave?.Invoke(this, EventArgs.Empty);
            RequestClose?.Invoke(this, EventArgs.Empty);
        });
        LaunchShortcutCommand = new RelayCommand<DesktopFileViewModel?>(LaunchShortcut);
        EnterFolderCommand = new RelayCommand<DesktopFileViewModel?>(EnterFolder);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => NavigationStack.Count > 0);
        NavigateHomeCommand = new RelayCommand(NavigateHome, () => NavigationStack.Count > 0);
    }

    public void Update(DesktopBox model)
    {
        Model = model;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }

    public void SetShortcuts(IEnumerable<ScannedFile> shortcuts)
    {
        Shortcuts.Clear();
        foreach (var file in DeduplicateShortcuts(shortcuts)
                     .OrderBy(s => s.ItemType != ScannedItemType.Folder)
                     .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase))
        {
            Shortcuts.Add(new DesktopFileViewModel(file));
        }
        NavigationStack.Clear();
        UpdateCurrentItems(null);
        RaiseNavigationCommands();
        Model.CurrentPath = SerializedPath;
    }

    public void RefreshShortcuts(IEnumerable<ScannedFile> shortcuts)
    {
        var ordered = DeduplicateShortcuts(shortcuts)
            .OrderBy(s => s.ItemType != ScannedItemType.Folder)
            .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeParent = NavigationStack.LastOrDefault();
        var parentId = activeParent?.Id;

        var itemsToRemove = Shortcuts.Where(s => ordered.All(f => f.Id != s.Id)).ToList();
        foreach (var remove in itemsToRemove)
        {
            Shortcuts.Remove(remove);
        }

        foreach (var file in ordered)
        {
            var existing = Shortcuts.FirstOrDefault(s => s.Id == file.Id);
            if (existing == null)
            {
                var vm = new DesktopFileViewModel(file);
                InsertShortcutInOrder(vm);
            }
            else
            {
                existing.UpdateFromModel(file);
            }
        }

        NavigateAndSyncCurrentItems(parentId);
        RaiseNavigationCommands();
    }

    private static IEnumerable<ScannedFile> DeduplicateShortcuts(IEnumerable<ScannedFile> shortcuts)
    {
        var seenIds = new HashSet<Guid>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var shortcut in shortcuts)
        {
            if (!seenIds.Add(shortcut.Id))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(shortcut.FilePath) && !seenPaths.Add(shortcut.FilePath))
            {
                continue;
            }

            yield return shortcut;
        }
    }

    public void EnterFolder(DesktopFileViewModel? folder)
    {
        if (folder is null || folder.ItemType != ScannedItemType.Folder)
        {
            if (folder is not null)
            {
                LaunchShortcut(folder);
            }
            return;
        }

        NavigationStack.Add(folder);
        NavigateAndSyncCurrentItems(folder.Id);
        RaiseNavigationCommands();
    }

    public void NavigateUp()
    {
        if (NavigationStack.Count == 0)
        {
            return;
        }

        NavigationStack.RemoveAt(NavigationStack.Count - 1);
        var parent = NavigationStack.LastOrDefault();
        NavigateAndSyncCurrentItems(parent?.Id);
        RaiseNavigationCommands();
    }

    public async Task RefreshIconsAsync()
    {
        var refreshTasks = Shortcuts.Select(item => item.RefreshIconAsync()).ToList();
        await Task.WhenAll(refreshTasks).ConfigureAwait(false);

        var activeParent = NavigationStack.LastOrDefault();
        NavigateAndSyncCurrentItems(activeParent?.Id);
    }

    private void InsertShortcutInOrder(DesktopFileViewModel vm)
    {
        var index = 0;
        while (index < Shortcuts.Count)
        {
            var current = Shortcuts[index];
            var currentIsFolder = current.ItemType == ScannedItemType.Folder;
            var vmIsFolder = vm.ItemType == ScannedItemType.Folder;

            if (vmIsFolder && !currentIsFolder)
            {
                break;
            }

            if (currentIsFolder == vmIsFolder)
            {
                if (string.Compare(vm.FileName, current.FileName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    break;
                }
            }

            index++;
        }

        Shortcuts.Insert(index, vm);
        SyncWindowState();
    }

    public void NavigateHome()
    {
        NavigationStack.Clear();
        NavigateAndSyncCurrentItems(null);
        RaiseNavigationCommands();
    }

    private void LaunchShortcut(DesktopFileViewModel? file)
    {
        if (file is null)
        {
            return;
        }

        var pathToLaunch = !string.IsNullOrWhiteSpace(file.ShortcutPath) && File.Exists(file.ShortcutPath)
            ? file.ShortcutPath
            : file.IsArchived && !string.IsNullOrWhiteSpace(file.ArchivedContentPath)
                ? file.ArchivedContentPath
                : file.FilePath;

        try
        {
            if (!File.Exists(pathToLaunch) && !Directory.Exists(pathToLaunch))
            {
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = pathToLaunch,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Swallow failures for now; consider logging in future.
        }
    }

    public void HandleDragEvent(DragEventArgs e)
    {
        if (!CanAcceptDrag(e))
        {
            e.Handled = true;
            e.DragEffects = DragDropEffects.None;
        }
    }

    public async Task HandleDropAsync(DragEventArgs e)
    {
        if (!CanAcceptDrag(e))
        {
            e.Handled = true;
            return;
        }

        if (!e.Data.Contains(DataFormats.Files))
        {
            e.Handled = true;
            return;
        }

        var storageItems = e.Data.GetFiles()?.ToList();
        if (storageItems is null || storageItems.Count == 0)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        var paths = new List<string>();
        foreach (var item in storageItems)
        {
            var uri = item.Path;
            if (uri is null)
            {
                continue;
            }

            string? path = null;
            if (uri.IsAbsoluteUri)
            {
                path = Uri.UnescapeDataString(uri.LocalPath);
            }
            else
            {
                var text = uri.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    path = Uri.UnescapeDataString(text);
                }
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        var imported = await AppServices.ScannedFileService.ImportPathsAsync(paths);
        if (imported.Count == 0)
        {
            return;
        }

        var boxShortcuts = new HashSet<Guid>(Model.ShortcutIds);
        var added = false;
        foreach (var file in imported)
        {
            if (boxShortcuts.Add(file.Id))
            {
                added = true;
            }
        }

        if (!added)
        {
            return;
        }

        Model.ShortcutIds = boxShortcuts.ToList();
        Model.ItemCount = boxShortcuts.Count;

        var updated = await AppServices.BoxService.AddOrUpdateAsync(Model);
        Update(updated);

        AppServices.NotifyBoxUpdated(updated);

        var shortcutsData = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var filtered = shortcutsData.Where(s => boxShortcuts.Contains(s.Id))
            .OrderByDescending(s => s.ItemType == ScannedItemType.Folder)
            .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase);
        Shortcuts.Clear();
        foreach (var file in filtered)
        {
            Shortcuts.Add(new DesktopFileViewModel(file));
        }
        NavigationStack.Clear();
        NavigateAndSyncCurrentItems(null);
        RaiseNavigationCommands();
        Model.CurrentPath = SerializedPath;
    }

    private bool CanAcceptDrag(DragEventArgs e)
    {
        return e.Data.Contains(DataFormats.Files);
    }

    private void UpdateCurrentItems(Guid? parentId)
    {
        CurrentItems.Clear();
        foreach (var item in Shortcuts.Where(s => s.ParentId == parentId))
        {
            CurrentItems.Add(item);
        }
        OnPropertyChanged(nameof(CurrentPath));
    }

    private void RaiseNavigationCommands()
    {
        OnPropertyChanged(nameof(CurrentPath));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }

    private void NavigateAndSyncCurrentItems(Guid? parentId)
    {
        UpdateCurrentItems(parentId);
        Model.CurrentPath = SerializedPath;
        SyncWindowState();
    }

    private void SyncWindowState()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RequestStateSave?.Invoke(this, EventArgs.Empty));
        }
        else
        {
            RequestStateSave?.Invoke(this, EventArgs.Empty);
        }
    }
}

