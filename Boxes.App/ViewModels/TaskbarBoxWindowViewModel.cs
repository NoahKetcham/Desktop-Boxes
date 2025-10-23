using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public class TaskbarBoxWindowViewModel : ViewModelBase
{
    public DesktopBox Model { get; }
    public ObservableCollection<DesktopFileViewModel> Shortcuts { get; } = new();
    public ObservableCollection<DesktopFileViewModel> CurrentItems { get; } = new();

    public IRelayCommand<DesktopFileViewModel?> LaunchShortcutCommand { get; }
    public IRelayCommand<DesktopFileViewModel?> EnterFolderCommand { get; }
    public IRelayCommand ToggleExpandCommand { get; }
    public IRelayCommand CloseCommand { get; }

    public string Name => Model.Name;
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
        UpdateCurrentItems(vm.Id);
    }

    private void LaunchShortcut(DesktopFileViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }
        // Reuse DesktopBoxWindowViewModel's launch behavior later if needed
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = vm.ShortcutPath ?? vm.ArchivedContentPath ?? vm.FilePath,
            UseShellExecute = true
        });
    }

    private void UpdateCurrentItems(Guid? parentId)
    {
        CurrentItems.Clear();
        foreach (var item in Shortcuts.Where(s => s.ParentId == parentId))
        {
            CurrentItems.Add(item);
        }
    }
}


