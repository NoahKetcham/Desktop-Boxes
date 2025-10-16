using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Boxes.App.Models;
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
        foreach (var file in shortcuts)
        {
            Shortcuts.Add(new DesktopFileViewModel(file));
        }
        NavigationStack.Clear();
        UpdateCurrentItems(null);
        RaiseNavigationCommands();
    }

    public void UpdateCurrentItems(Guid? parentId)
    {
        CurrentItems.Clear();
        foreach (var item in Shortcuts.Where(s => s.ParentId == parentId))
        {
            CurrentItems.Add(item);
        }
        OnPropertyChanged(nameof(CurrentPath));
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

    private void RaiseNavigationCommands()
    {
        OnPropertyChanged(nameof(CurrentPath));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }
}

