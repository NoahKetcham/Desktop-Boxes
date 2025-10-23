using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public class TaskbarBoxWindowViewModel : ViewModelBase
{
    public DesktopBox Model { get; }
    public ObservableCollection<DesktopFileViewModel> Shortcuts { get; } = new();
    public ObservableCollection<DesktopFileViewModel> CurrentItems { get; } = new();
    public ObservableCollection<DesktopFileViewModel> NavigationStack { get; } = new();

    public IRelayCommand<DesktopFileViewModel?> LaunchShortcutCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> EnterFolderCommand { get; }
    public IRelayCommand NavigateUpCommand { get; }
    public IRelayCommand NavigateHomeCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }
    public IRelayCommand CloseCommand { get; }

    public string Name => Model.Name;
    public string CurrentPath => NavigationStack.Count == 0 ? "Desktop" : string.Join(" / ", NavigationStack.Select(f => f.FileName));
    public bool IsExpanded { get; private set; }
    public event EventHandler<bool>? ToggleExpandRequested;

    public TaskbarBoxWindowViewModel(DesktopBox model, BoxWindowState state, System.Collections.Generic.IEnumerable<ScannedFile> shortcuts)
    {
        Model = model;
        IsExpanded = !state.IsCollapsed;
        foreach (var s in shortcuts.OrderBy(s => s.ItemType != ScannedItemType.Folder).ThenBy(s => s.FileName, System.StringComparer.OrdinalIgnoreCase))
        {
            Shortcuts.Add(new DesktopFileViewModel(s));
        }
        UpdateCurrentItems(null);

        LaunchShortcutCommand = new RelayCommand<DesktopFileViewModel?>(LaunchShortcut);
        EnterFolderCommand = new RelayCommand<DesktopFileViewModel?>(EnterFolder);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => NavigationStack.Count > 0);
        NavigateHomeCommand = new RelayCommand(NavigateHome, () => NavigationStack.Count > 0);
        ToggleExpandCommand = new RelayCommand(ToggleExpanded);
        CloseCommand = new RelayCommand(() => { });
    }

    public void ToggleExpanded()
    {
        var next = !IsExpanded;
        SetExpanded(next);
        ToggleExpandRequested?.Invoke(this, next);
    }

    public void SetExpanded(bool value)
    {
        if (IsExpanded == value)
        {
            return;
        }
        IsExpanded = value;
        OnPropertyChanged(nameof(IsExpanded));
    }

    private void EnterFolder(DesktopFileViewModel? vm)
    {
        if (vm is null || !vm.IsFolder)
        {
            LaunchShortcut(vm);
            return;
        }
        NavigationStack.Add(vm);
        UpdateCurrentItems(vm.Id);
        RaiseNavigation();
    }

    private void LaunchShortcut(DesktopFileViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }
        // Reuse DesktopBoxWindowViewModel's launch behavior
        var psi = new ProcessStartInfo
        {
            FileName = vm.ShortcutPath ?? vm.ArchivedContentPath ?? vm.FilePath,
            UseShellExecute = true
        };
        try { Process.Start(psi); } catch { }
    }

    private void UpdateCurrentItems(Guid? parentId)
    {
        CurrentItems.Clear();
        foreach (var item in Shortcuts.Where(s => s.ParentId == parentId))
        {
            CurrentItems.Add(item);
        }
    }

    // --- Drag & Drop parity with DesktopBoxWindow ---
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
            if (uri is null) continue;

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
        // refresh shortcuts content
        var shortcutsData = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var filtered = shortcutsData.Where(s => boxShortcuts.Contains(s.Id))
            .OrderBy(s => s.ItemType != ScannedItemType.Folder)
            .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Shortcuts.Clear();
        foreach (var s in filtered)
        {
            Shortcuts.Add(new DesktopFileViewModel(s));
        }
        var activeParent = NavigationStack.LastOrDefault();
        UpdateCurrentItems(activeParent?.Id);
        RaiseNavigation();
    }

    private bool CanAcceptDrag(DragEventArgs e)
    {
        return e.Data.Contains(DataFormats.Files);
    }

    private void RaiseNavigation()
    {
        OnPropertyChanged(nameof(CurrentPath));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }

    private void NavigateUp()
    {
        if (NavigationStack.Count == 0) return;
        NavigationStack.RemoveAt(NavigationStack.Count - 1);
        var parent = NavigationStack.LastOrDefault();
        UpdateCurrentItems(parent?.Id);
        RaiseNavigation();
    }

    private void NavigateHome()
    {
        NavigationStack.Clear();
        UpdateCurrentItems(null);
        RaiseNavigation();
    }
}


