using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace Boxes.App.ViewModels;

public partial class ShortcutSelectionViewModel : ViewModelBase
{
    public ObservableCollection<ShortcutSelectionItemViewModel> Shortcuts { get; }
    public ObservableCollection<ShortcutSelectionItemViewModel> CurrentItems { get; } = new();
    public ObservableCollection<ShortcutSelectionItemViewModel> NavigationStack { get; } = new();
    public string BoxName { get; }

    public event EventHandler<bool>? CloseRequested;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand<ShortcutSelectionItemViewModel?> EnterFolderCommand { get; }
    public IRelayCommand NavigateUpCommand { get; }
    public IRelayCommand NavigateHomeCommand { get; }

    public string CurrentPath => NavigationStack.Count == 0
        ? "Desktop"
        : string.Join(" / ", NavigationStack.Select(s => s.File.FileName));

    public ShortcutSelectionViewModel(string boxName, IEnumerable<ScannedFile> files, IEnumerable<Guid> selected)
    {
        BoxName = boxName;
        var selectedSet = new HashSet<Guid>(selected);
        Shortcuts = new ObservableCollection<ShortcutSelectionItemViewModel>(
            files.Select(f => new ShortcutSelectionItemViewModel(f, selectedSet.Contains(f.Id))));

        UpdateCurrentItems(null);

        SaveCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        EnterFolderCommand = new RelayCommand<ShortcutSelectionItemViewModel?>(EnterFolder);
        NavigateUpCommand = new RelayCommand(NavigateUp, () => NavigationStack.Count > 0);
        NavigateHomeCommand = new RelayCommand(NavigateHome, () => NavigationStack.Count > 0);
    }

    public IEnumerable<ScannedFile> GetSelectedFiles()
    {
        return Shortcuts.Where(s => s.IsSelected).Select(s => s.File);
    }

    public void EnterFolder(ShortcutSelectionItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        if (folder.File.ItemType != ScannedItemType.Folder)
        {
            if (folder.File.ItemType == ScannedItemType.File || folder.File.ItemType == ScannedItemType.Shortcut)
            {
                folder.IsSelected = !folder.IsSelected;
            }
            return;
        }

        NavigationStack.Add(folder);
        UpdateCurrentItems(folder.File.Id);
        RaiseNavigationState();
    }

    public void NavigateUp()
    {
        if (NavigationStack.Count == 0)
        {
            return;
        }

        NavigationStack.RemoveAt(NavigationStack.Count - 1);
        var parent = NavigationStack.LastOrDefault();
        UpdateCurrentItems(parent?.File.Id);
        RaiseNavigationState();
    }

    public void NavigateHome()
    {
        NavigationStack.Clear();
        UpdateCurrentItems(null);
        RaiseNavigationState();
    }

    private void UpdateCurrentItems(Guid? parentId)
    {
        CurrentItems.Clear();
        foreach (var item in Shortcuts.Where(s => s.File.ParentId == parentId))
        {
            CurrentItems.Add(item);
        }
    }

    private void RaiseNavigationState()
    {
        OnPropertyChanged(nameof(CurrentPath));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }
}
