using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.Services;
using Boxes.App.Views;
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
    public IRelayCommand ToggleSnapCommand { get; }
    public IRelayCommand OpenSettingsCommand { get; }

    public string CurrentPath => NavigationStack.Count == 0
        ? "Desktop"
        : string.Join(" / ", NavigationStack.Select(f => f.FileName));
    
    public bool CanNavigateUp => NavigationStack.Count > 0;

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
    public event EventHandler? RequestSnapToTaskbar;
    public event EventHandler? RequestUnsnapFromTaskbar;

    private DesktopBoxWindow? _view;
    private CancellationTokenSource? _pendingLoadCts;
    private bool _suspendStateSync;
    private static readonly Guid RootOrderKey = Guid.Empty;

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
        ToggleSnapCommand = new RelayCommand(ToggleSnap);
        OpenSettingsCommand = new RelayCommand(OpenSettings);

        ApplySnapState(model.IsSnappedToTaskbar, model.IsCollapsed, model.ExpandedHeight, model.ExpandedPositionX, model.ExpandedPositionY, suppressSync: true);
        
        // Subscribe to navigation stack changes to update CanNavigateUp property
        NavigationStack.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanNavigateUp));
        };
    }

    public void SnapToTaskbar() => RequestSnapToTaskbar?.Invoke(this, EventArgs.Empty);

    private void ToggleSnap()
    {
        if (!Model.IsSnappedToTaskbar)
        {
            RequestSnapToTaskbar?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RequestUnsnapFromTaskbar?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OpenSettings()
    {
        // Try to get existing main window from AppServices
        var mainWindow = AppServices.MainWindowOwner;
        
        // Only create a new window if one doesn't exist at all
        if (mainWindow == null)
        {
            mainWindow = new Views.MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            AppServices.MainWindowOwner = mainWindow;
        }

        // Show and activate the main window
        mainWindow.Show();
        mainWindow.Activate();

        // Navigate to Settings page
        if (mainWindow.DataContext is MainWindowViewModel mainVm)
        {
            var settingsNav = mainVm.NavigationItems.FirstOrDefault(n => n.Content is SettingsPageViewModel);
            if (settingsNav != null)
            {
                mainVm.SelectedNavigationItem = settingsNav;
            }
        }
    }

    internal void RegisterView(DesktopBoxWindow view)
    {
        _view = view;
    }

    public void Update(DesktopBox model)
    {
        Model = model;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        ApplySnapState(model.IsSnappedToTaskbar, model.IsCollapsed, model.ExpandedHeight, model.ExpandedPositionX, model.ExpandedPositionY, suppressSync: true);
    }

    public const double CollapsedWindowHeight = 40;

    public bool IsSnappedToTaskbar => Model.IsSnappedToTaskbar;
    public bool IsCollapsed => Model.IsCollapsed;
    public bool IsExpanded => !Model.IsCollapsed;
    public double ExpandedHeight => Model.ExpandedHeight ?? Model.Height;
    public double? ExpandedPositionX => Model.ExpandedPositionX;
    public double? ExpandedPositionY => Model.ExpandedPositionY;
    public bool WasSnapExpanded => Model.WasSnapExpanded;

    public void ApplySnapState(bool isSnapped, bool isCollapsed, double? expandedHeight, double? expandedPosX, double? expandedPosY, bool suppressSync)
    {
        var previous = _suspendStateSync;
        if (suppressSync)
        {
            _suspendStateSync = true;
        }

        var effectiveExpandedHeight = expandedHeight.HasValue && expandedHeight.Value > 0
            ? expandedHeight.Value
            : Model.ExpandedHeight ?? Model.Height;

        if (effectiveExpandedHeight <= 0)
        {
            effectiveExpandedHeight = Model.Height > 0 ? Model.Height : 240;
        }

        if (Model.IsSnappedToTaskbar != isSnapped)
        {
            Model.IsSnappedToTaskbar = isSnapped;
            OnPropertyChanged(nameof(IsSnappedToTaskbar));
        }

        if (Model.IsCollapsed != isCollapsed)
        {
            Model.IsCollapsed = isCollapsed;
            OnPropertyChanged(nameof(IsCollapsed));
            OnPropertyChanged(nameof(IsExpanded));
        }

        if (!Nullable.Equals(Model.ExpandedHeight, effectiveExpandedHeight))
        {
            Model.ExpandedHeight = effectiveExpandedHeight;
            OnPropertyChanged(nameof(ExpandedHeight));
        }

        if (!Nullable.Equals(Model.ExpandedPositionX, expandedPosX))
        {
            Model.ExpandedPositionX = expandedPosX;
            OnPropertyChanged(nameof(ExpandedPositionX));
        }

        if (!Nullable.Equals(Model.ExpandedPositionY, expandedPosY))
        {
            Model.ExpandedPositionY = expandedPosY;
            OnPropertyChanged(nameof(ExpandedPositionY));
        }

        if (Model.WasSnapExpanded != !isCollapsed)
        {
            Model.WasSnapExpanded = !isCollapsed;
            OnPropertyChanged(nameof(WasSnapExpanded));
        }

        if (suppressSync)
        {
            _suspendStateSync = previous;
        }
        else
        {
            SyncWindowState();
        }
    }

    public void PrepareForStagedLoad()
    {
        CancelPendingLoad();
        Shortcuts.Clear();
        CurrentItems.Clear();
        NavigationStack.Clear();
        OnPropertyChanged(nameof(CurrentPath));
        RaiseNavigationCommands();
    }

    public void StageLoadShortcuts(IEnumerable<ScannedFile> shortcuts, TimeSpan delay)
    {
        CancelPendingLoad();
        var snapshot = shortcuts.ToList();
        var cts = new CancellationTokenSource();
        _pendingLoadCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        ApplyShortcuts(snapshot, resetNavigation: true);
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // ignore, superseded by a newer load
            }
            finally
            {
                if (_pendingLoadCts == cts)
                {
                    CancelPendingLoad();
                }
            }
        });
    }

    public void SetShortcuts(IEnumerable<ScannedFile> shortcuts)
    {
        CancelPendingLoad();
        ApplyShortcuts(shortcuts, resetNavigation: true);
    }

    public void RefreshShortcuts(IEnumerable<ScannedFile> shortcuts)
    {
        CancelPendingLoad();
        ApplyShortcuts(shortcuts, resetNavigation: false);
    }

    /// <summary>
    /// Reorder items within the current folder, persist order map, and refresh UI.
    /// </summary>
    public async Task ReorderCurrentItemsAsync(Guid draggedId, Guid? dropBeforeId)
    {
        var parentId = NavigationStack.LastOrDefault()?.Id;
        var orderKey = GetOrderKey(parentId);
        var currentIds = CurrentItems.Select(i => i.Id);
        var order = GetOrCreateOrder(orderKey, currentIds);

        if (!order.Remove(draggedId))
        {
            return;
        }

        var targetIndex = dropBeforeId.HasValue ? order.IndexOf(dropBeforeId.Value) : order.Count;
        if (targetIndex < 0 || targetIndex > order.Count)
        {
            targetIndex = order.Count;
        }

        order.Insert(targetIndex, draggedId);
        Model.ItemOrder[orderKey] = order;

        ApplyOrderToCollections(parentId, order);
        Model.CurrentPath = SerializedPath;
        SyncWindowState();

        var updated = await AppServices.BoxService.AddOrUpdateAsync(Model).ConfigureAwait(false);
        Update(updated);
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
        var filtered = shortcutsData.Where(s => boxShortcuts.Contains(s.Id));
        ApplyShortcuts(filtered, resetNavigation: true);
    }

    private bool CanAcceptDrag(DragEventArgs e)
    {
        return e.Data.Contains(DataFormats.Files);
    }

    private void ApplyShortcuts(IEnumerable<ScannedFile> shortcuts, bool resetNavigation)
    {
        var ordered = OrderShortcutsWithStoredOrder(shortcuts);

        LogDuplicateWarnings(ordered);

        List<Guid> breadcrumb = resetNavigation
            ? new List<Guid>()
            : NavigationStack.Select(f => f.Id).ToList();

        Shortcuts.Clear();
        foreach (var file in ordered)
        {
            Shortcuts.Add(new DesktopFileViewModel(file));
        }

        RestoreNavigation(breadcrumb);
        RaiseNavigationCommands();
        Model.CurrentPath = SerializedPath;
        SyncWindowState();
        _view?.InvalidateShortcutsLayout();
    }

    private void CancelPendingLoad()
    {
        if (_pendingLoadCts is null)
        {
            return;
        }

        _pendingLoadCts.Cancel();
        _pendingLoadCts.Dispose();
        _pendingLoadCts = null;
    }

    private void RestoreNavigation(IReadOnlyList<Guid> breadcrumb)
    {
        NavigationStack.Clear();

        if (breadcrumb.Count == 0)
        {
            UpdateCurrentItems(null);
            return;
        }

        foreach (var id in breadcrumb)
        {
            var folderVm = Shortcuts.FirstOrDefault(s => s.Id == id && s.ItemType == ScannedItemType.Folder);
            if (folderVm is null)
            {
                break;
            }

            NavigationStack.Add(folderVm);
        }

        var activeParent = NavigationStack.LastOrDefault();
        UpdateCurrentItems(activeParent?.Id);
    }

    [Conditional("DEBUG")]
    private static void LogDuplicateWarnings(IEnumerable<ScannedFile> shortcuts)
    {
        var duplicatesById = shortcuts
            .GroupBy(s => s.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatesById.Count > 0)
        {
            Debug.WriteLine($"[Boxes] Duplicate shortcut IDs detected: {string.Join(", ", duplicatesById)}");
        }

        var duplicatesByPath = shortcuts
            .Select(s => ShortcutCatalog.GetAllShortcutsDeduped(new[] { s }).First())
            .GroupBy(s => s.FilePath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatesByPath.Count > 0)
        {
            Debug.WriteLine($"[Boxes] Duplicate shortcut paths detected: {string.Join(", ", duplicatesByPath)}");
        }
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
        OnPropertyChanged(nameof(CanNavigateUp));
        NavigateUpCommand.NotifyCanExecuteChanged();
        NavigateHomeCommand.NotifyCanExecuteChanged();
    }

    private void NavigateAndSyncCurrentItems(Guid? parentId)
    {
        UpdateCurrentItems(parentId);
        Model.CurrentPath = SerializedPath;
        SyncWindowState();
    }

    private List<ScannedFile> OrderShortcutsWithStoredOrder(IEnumerable<ScannedFile> shortcuts)
    {
        var deduped = ShortcutCatalog.GetAllShortcutsDeduped(shortcuts).ToList();
        var grouped = deduped.GroupBy(s => GetOrderKey(s.ParentId));
        var result = new List<ScannedFile>();

        foreach (var group in grouped)
        {
            var orderedGroup = OrderGroupWithFallback(group.Key, group.ToList());
            result.AddRange(orderedGroup);
        }

        return result;
    }

    private List<ScannedFile> OrderGroupWithFallback(Guid orderKey, List<ScannedFile> group)
    {
        var order = GetOrCreateOrder(orderKey, group.Select(g => g.Id));
        var lookup = group.ToDictionary(g => g.Id);
        var ordered = new List<ScannedFile>();

        foreach (var id in order)
        {
            if (lookup.TryGetValue(id, out var file))
            {
                ordered.Add(file);
            }
        }

        if (ordered.Count == group.Count)
        {
            return ordered;
        }

        var remaining = group
            .Where(g => !order.Contains(g.Id))
            .OrderBy(g => g.ItemType != ScannedItemType.Folder)
            .ThenBy(g => g.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in remaining)
        {
            order.Add(item.Id);
            ordered.Add(item);
        }

        Model.ItemOrder[orderKey] = order;
        return ordered;
    }

    private List<Guid> GetOrCreateOrder(Guid orderKey, IEnumerable<Guid> currentIds)
    {
        if (!Model.ItemOrder.TryGetValue(orderKey, out var order) || order is null)
        {
            order = new List<Guid>();
        }

        var currentSet = new HashSet<Guid>(currentIds);
        order = order.Where(currentSet.Contains).Distinct().ToList();

        foreach (var id in currentSet)
        {
            if (!order.Contains(id))
            {
                order.Add(id);
            }
        }

        Model.ItemOrder[orderKey] = order;
        return order;
    }

    private Guid GetOrderKey(Guid? parentId) => parentId ?? RootOrderKey;

    private void ApplyOrderToCollections(Guid? parentId, List<Guid> order)
    {
        var parentItems = Shortcuts.Where(s => s.ParentId == parentId).ToList();
        var orderedItems = parentItems
            .OrderBy(i => order.IndexOf(i.Id) is int idx && idx >= 0 ? idx : int.MaxValue)
            .ThenBy(i => i.ItemType != ScannedItemType.Folder)
            .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in parentItems)
        {
            Shortcuts.Remove(item);
        }

        foreach (var item in orderedItems)
        {
            Shortcuts.Add(item);
        }

        UpdateCurrentItems(parentId);
    }

    private void SyncWindowState()
    {
        if (_suspendStateSync)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RequestStateSave?.Invoke(this, EventArgs.Empty));
        }
        else
        {
            RequestStateSave?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetSnapState(bool isSnapped, bool isCollapsed, double? expandedHeight, bool suppressSync)
    {
        var effectiveExpanded = expandedHeight.HasValue && expandedHeight.Value > 0
            ? expandedHeight
            : (Model.ExpandedHeight is > 0 ? Model.ExpandedHeight : Model.Height);

        if (effectiveExpanded is null || effectiveExpanded <= 0)
        {
            effectiveExpanded = Model.Height > 0 ? Model.Height : 240;
        }

        var previousSuspend = _suspendStateSync;
        if (suppressSync)
        {
            _suspendStateSync = true;
        }

        var changed = false;

        if (Model.IsSnappedToTaskbar != isSnapped)
        {
            Model.IsSnappedToTaskbar = isSnapped;
            OnPropertyChanged(nameof(IsSnappedToTaskbar));
            changed = true;
        }

        if (Model.IsCollapsed != isCollapsed)
        {
            Model.IsCollapsed = isCollapsed;
            OnPropertyChanged(nameof(IsCollapsed));
            OnPropertyChanged(nameof(IsExpanded));
            changed = true;
        }

        if (!Nullable.Equals(Model.ExpandedHeight, effectiveExpanded))
        {
            Model.ExpandedHeight = effectiveExpanded;
            OnPropertyChanged(nameof(ExpandedHeight));
            changed = true;
        }

        if (suppressSync)
        {
            _suspendStateSync = previousSuspend;
        }

        if (changed && !suppressSync)
        {
            SyncWindowState();
        }
    }
}

