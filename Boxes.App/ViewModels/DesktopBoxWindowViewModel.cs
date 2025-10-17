using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
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

    public DesktopBoxWindowViewModel(DesktopBox model)
    {
        Model = model;
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
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
        foreach (var file in shortcuts
                     .OrderBy(s => s.ItemType != ScannedItemType.Folder)
                     .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase))
        {
            Shortcuts.Add(new DesktopFileViewModel(file));
        }
        NavigationStack.Clear();
        UpdateCurrentItems(null);
        RaiseNavigationCommands();
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
        UpdateCurrentItems(folder.Id);
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
        UpdateCurrentItems(parent?.Id);
        RaiseNavigationCommands();
    }

    public void NavigateHome()
    {
        NavigationStack.Clear();
        UpdateCurrentItems(null);
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
        UpdateCurrentItems(null);
        RaiseNavigationCommands();
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

}

