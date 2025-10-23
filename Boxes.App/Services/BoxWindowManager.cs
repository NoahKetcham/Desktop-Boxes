using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.ViewModels;
using Boxes.App.Views;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Boxes.App.Services;

public class BoxWindowManager
{
    private readonly Dictionary<Guid, DesktopBoxWindow> _windows = new();
    private readonly Dictionary<Guid, TaskbarBoxWindow> _taskbarWindows = new();
    private readonly Dictionary<Guid, WindowStateData> _windowStates = new();
    private bool _areWindowsVisible = true;

    public bool AreWindowsVisible => _areWindowsVisible;

    public bool HasOpenWindows => _windows.Count > 0;

    public Task ToggleAllWindowsVisibility()
    {
        var targetState = !_areWindowsVisible;
        return SetWindowsVisibility(targetState);
    }

    public async Task SetWindowsVisibility(bool visible)
    {
        if (_areWindowsVisible == visible)
        {
            return;
        }

        var saveTasks = new List<Task>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // snapshot to avoid collection modification issues
            var snapshot = _windows.Values.ToList();

            if (visible)
            {
                foreach (var window in snapshot)
                {
                    if (_windowStates.TryGetValue(window.ViewModel.Model.Id, out var state))
                    {
                        window.Width = state.Width;
                        window.Height = state.Height;
                        if (!double.IsNaN(state.X) && !double.IsNaN(state.Y))
                        {
                            window.Position = new PixelPoint((int)state.X, (int)state.Y);
                        }
                    }

                    window.Show();
                    window.Activate();
                }
            }
            else
            {
                foreach (var window in snapshot)
                {
                    // capture state before hiding
                    saveTasks.Add(TrySaveWindowStateAsync(window.ViewModel.Model, window, window.ViewModel.CurrentPath));
                    window.Hide();
                }
            }

            _areWindowsVisible = visible;
        });

        if (saveTasks.Count > 0)
        {
            await Task.WhenAll(saveTasks).ConfigureAwait(false);
        }
    }

    public async Task ShowAsync(DesktopBox box)
    {
        var shortcutsData = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var archivedShortcuts = await AppServices.ScannedFileService.GetStoredShortcutsAsync();
        var filteredShortcuts = BuildShortcutList(box, shortcutsData, archivedShortcuts);

        var state = await AppServices.WindowStateService.GetAsync(box.Id);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (state.Mode == WindowMode.Taskbar)
            {
                CreateOrShowTaskbarWindow(box, filteredShortcuts, state);
                return;
            }

            if (_windows.TryGetValue(box.Id, out var existing))
            {
                if (existing.DataContext is DesktopBoxWindowViewModel vmExisting)
                {
                    vmExisting.Update(box);
                    vmExisting.PrepareForStagedLoad();
                    vmExisting.StageLoadShortcuts(filteredShortcuts, TimeSpan.FromMilliseconds(150));
                }
                if (!existing.IsVisible && _areWindowsVisible)
                {
                    existing.Show();
                }
                existing.Activate();
                return;
            }

            var vm = new DesktopBoxWindowViewModel(box);
            vm.PrepareForStagedLoad();
            var window = new DesktopBoxWindow
            {
                DataContext = vm
            };

            if (_windowStates.TryGetValue(box.Id, out var existingState))
            {
                window.Width = existingState.Width;
                window.Height = existingState.Height;
                if (!double.IsNaN(existingState.X) && !double.IsNaN(existingState.Y))
                {
                    window.Position = new PixelPoint((int)existingState.X, (int)existingState.Y);
                }
            }
            else
            {
                window.Width = box.Width > 0 ? box.Width : window.Width;
                window.Height = box.Height > 0 ? box.Height : window.Height;
                if (box.PositionX.HasValue && box.PositionY.HasValue)
                {
                    window.Position = new PixelPoint((int)box.PositionX.Value, (int)box.PositionY.Value);
                }
            }

            EventHandler? handler = null;
            handler = (_, _) => window.Close();
            vm.RequestClose += handler;

            vm.RequestStateSave += async (_, _) =>
            {
                await SaveWindowStateAsync(vm.Model, window, vm.CurrentPath).ConfigureAwait(false);
            };

            vm.RequestSnapToTaskbar += async (_, _) => await SnapToTaskbarAsync(vm.Model.Id).ConfigureAwait(false);
            vm.RequestUnsnapFromTaskbar += async (_, _) => await UnsnapFromTaskbarAsync(vm.Model.Id).ConfigureAwait(false);

            window.Closed += async (_, _) =>
            {
                await SaveWindowStateAsync(vm.Model, window, vm.CurrentPath).ConfigureAwait(false);
                _windows.Remove(box.Id);
                vm.RequestClose -= handler;
            };
            _windows[box.Id] = window;

            if (_windowStates.TryGetValue(box.Id, out var stateFromCache))
            {
                RestorePath(vm, stateFromCache.Path);
            }
            else if (!string.IsNullOrEmpty(box.CurrentPath))
            {
                RestorePath(vm, box.CurrentPath);
            }

            if (_areWindowsVisible)
            {
                window.Show();
                window.Activate();
            }
            else
            {
                window.Hide();
            }

            vm.StageLoadShortcuts(filteredShortcuts, TimeSpan.FromMilliseconds(150));
        });
    }

    private void CreateOrShowTaskbarWindow(DesktopBox box, List<ScannedFile> filteredShortcuts, BoxWindowState state)
    {
        if (_taskbarWindows.TryGetValue(box.Id, out var existing))
        {
            try
            {
                existing.DataContext = new TaskbarBoxWindowViewModel(box, state, filteredShortcuts);
                AnchorTaskbarWindow(existing, state);
                if (_areWindowsVisible)
                {
                    if (!existing.IsVisible)
                    {
                        existing.Show();
                    }
                    existing.Activate();
                }
                else
                {
                    existing.Hide();
                }
                return;
            }
            catch (InvalidOperationException)
            {
                // Window instance was closed; recreate a new one
                _taskbarWindows.Remove(box.Id);
            }
        }

        var taskbarVm = new TaskbarBoxWindowViewModel(box, state, filteredShortcuts);
        var taskbar = new TaskbarBoxWindow { DataContext = taskbarVm };
        _taskbarWindows[box.Id] = taskbar;
        taskbar.Closed += (_, _) => _taskbarWindows.Remove(box.Id);
        AnchorTaskbarWindow(taskbar, state);
        if (_areWindowsVisible)
        {
            taskbar.Show();
            taskbar.Activate();
        }
        else
        {
            taskbar.Hide();
        }
    }

    public async Task SnapToTaskbarAsync(Guid boxId)
    {
        // capture UI-related values on UI thread
        DesktopBox? model = null;
        int expandedHeight = 0;
        int expandedX = 0;
        int expandedY = 0;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!_windows.TryGetValue(boxId, out var window))
            {
                return;
            }

            model = window.ViewModel.Model;
            expandedHeight = (int)window.Height;
            expandedX = window.Position.X;
            expandedY = window.Position.Y;

            _windows.Remove(boxId);
            window.Close();
        });

        if (model is null)
        {
            model = await AppServices.BoxService.GetBoxAsync(boxId).ConfigureAwait(false);
            if (model is null)
            {
                return;
            }
        }

        var state = await AppServices.WindowStateService.GetAsync(boxId).ConfigureAwait(false);
        state.Mode = WindowMode.Taskbar;
        state.IsCollapsed = true;
        state.ExpandedHeight = expandedHeight;
        state.ExpandedPosX = expandedX;
        state.ExpandedPosY = expandedY;
        state.NormalWidth = model.Width;
        state.NormalHeight = model.Height;
        state.NormalX = model.PositionX ?? expandedX;
        state.NormalY = model.PositionY ?? expandedY;
        await AppServices.WindowStateService.SaveAsync(state).ConfigureAwait(false);

        var shortcutsData = await AppServices.ScannedFileService.GetScannedFilesAsync().ConfigureAwait(false);
        var archived = await AppServices.ScannedFileService.GetStoredShortcutsAsync().ConfigureAwait(false);
        var filtered = BuildShortcutList(model, shortcutsData, archived);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreateOrShowTaskbarWindow(model, filtered, state);
        });
    }

    public async Task UnsnapFromTaskbarAsync(Guid boxId)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_taskbarWindows.Remove(boxId, out var taskbar))
            {
                taskbar.Close();
            }
        });

        var state = await AppServices.WindowStateService.GetAsync(boxId).ConfigureAwait(false);
        state.Mode = WindowMode.Normal;
        state.IsCollapsed = false;
        await AppServices.WindowStateService.SaveAsync(state).ConfigureAwait(false);

        var box = await AppServices.BoxService.GetBoxAsync(boxId).ConfigureAwait(false);
        if (box is null)
        {
            return;
        }
        await ShowAsync(box).ConfigureAwait(false);
    }

    public async Task SetSnappedExpandedAsync(Guid boxId, bool expanded)
    {
        int startPx = 0;
        int endPx = 0;
        int newX = 0;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!_taskbarWindows.TryGetValue(boxId, out var window))
            {
                return;
            }

            newX = window.Position.X;
            startPx = (int)Math.Round(window.Bounds.Height * window.RenderScaling);
            var working = GetPrimaryWorkingArea(window);
            if (expanded)
            {
                var half = Math.Max(120, working.Height / 2);
                endPx = (int)Math.Round(half * window.RenderScaling);
            }
            else
            {
                endPx = (int)Math.Round(DesktopBoxWindowViewModel.CollapsedWindowHeight * window.RenderScaling);
            }
        });

        if (!_taskbarWindows.TryGetValue(boxId, out var w))
        {
            return;
        }
        await AnimateTaskbarHeightAsync(w, startPx, endPx, TimeSpan.FromSeconds(1));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_taskbarWindows.TryGetValue(boxId, out var window) && window.DataContext is TaskbarBoxWindowViewModel tvm)
            {
                tvm.SetExpanded(expanded);
            }
        });

        var state = await AppServices.WindowStateService.GetAsync(boxId).ConfigureAwait(false);
        state.IsCollapsed = !expanded;
        state.X = newX;
        if (expanded && state.ExpandedHeight <= 0)
        {
            var workingHeight = await Dispatcher.UIThread.InvokeAsync(() => GetPrimaryWorkingArea(_taskbarWindows[boxId]).Height);
            state.ExpandedHeight = Math.Max(120, workingHeight / 2);
        }
        await AppServices.WindowStateService.SaveAsync(state).ConfigureAwait(false);
    }

    private static PixelRect GetPrimaryWorkingArea(Window window)
    {
        var screens = window.Screens;
        var primary = screens?.Primary ?? screens?.All?.FirstOrDefault();
        return primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
    }

    private static void AnchorTaskbarWindow(TaskbarBoxWindow window, BoxWindowState state)
    {
        var working = GetPrimaryWorkingArea(window);
        var height = state.IsCollapsed ? DesktopBoxWindowViewModel.CollapsedWindowHeight : (state.ExpandedHeight > 0 ? state.ExpandedHeight : window.Height);
        var heightPx = (int)Math.Round(height * window.RenderScaling);
        int y;
        if (TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out var monitorRect))
        {
            y = taskbarTop - heightPx;
        }
        else
        {
            y = working.Bottom - heightPx;
        }
        window.Height = height;
        var x = (int)(state.X == 0 ? window.Position.X : state.X);
        window.Position = new PixelPoint(x, y);
    }

    public Task SaveTaskbarWindowXAsync(Guid boxId, int x)
    {
        return AppServices.WindowStateService.GetAsync(boxId)
            .ContinueWith(async t =>
            {
                var s = t.Result;
                s.X = x;
                await AppServices.WindowStateService.SaveAsync(s).ConfigureAwait(false);
            }).Unwrap();
    }

    private async Task AnimateTaskbarHeightAsync(TaskbarBoxWindow window, int startHeightPx, int endHeightPx, TimeSpan duration)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var step = TimeSpan.FromMilliseconds(16);
        var lastApplied = -1;
        while (sw.Elapsed < duration)
        {
            var t = sw.Elapsed.TotalMilliseconds / duration.TotalMilliseconds;
            var eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic
            var hPx = (int)Math.Round(startHeightPx + (endHeightPx - startHeightPx) * eased);
            if (hPx == lastApplied)
            {
                await Task.Delay(step).ConfigureAwait(false);
                continue;
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var hLogical = hPx / window.RenderScaling;
                window.Height = hLogical;
                int y;
                if (TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out _))
                {
                    y = taskbarTop - hPx;
                }
                else
                {
                    var working = GetPrimaryWorkingArea(window);
                    y = working.Bottom - hPx;
                }
                window.Position = new PixelPoint(window.Position.X, y);
            });
            lastApplied = hPx;
            await Task.Delay(step).ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var hLogical = endHeightPx / window.RenderScaling;
            window.Height = hLogical;
            int y;
            if (TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out _))
            {
                y = taskbarTop - endHeightPx;
            }
            else
            {
                var working = GetPrimaryWorkingArea(window);
                y = working.Bottom - endHeightPx;
            }
            window.Position = new PixelPoint(window.Position.X, y);
        });
    }

    public async Task UpdateAsync(DesktopBox box)
    {
        var shortcutsData = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var archivedShortcuts = await AppServices.ScannedFileService.GetStoredShortcutsAsync();
        var filteredShortcuts = BuildShortcutList(box, shortcutsData, archivedShortcuts);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windows.TryGetValue(box.Id, out var window) && window.DataContext is DesktopBoxWindowViewModel vm)
            {
                vm.Update(box);
                vm.RefreshShortcuts(filteredShortcuts);
            }
        });
    }

    public async Task CloseAsync(Guid id)
    {
        DesktopBoxWindow? target = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _windows.TryGetValue(id, out target);
        });

        if (target is null)
        {
            return;
        }

        // save state before closing
        await TrySaveWindowStateAsync(target.ViewModel.Model, target, target.ViewModel.CurrentPath).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windows.Remove(id, out var w))
            {
                w.Close();
            }
        });
    }

    public async Task CloseAllAsync()
    {
        List<DesktopBoxWindow> snapshot = new();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            snapshot = _windows.Values.ToList();
        });

        // Save all states first
        var saveTasks = snapshot
            .Select(w => TrySaveWindowStateAsync(w.ViewModel.Model, w, w.ViewModel.CurrentPath))
            .ToList();

        if (saveTasks.Count > 0)
        {
            await Task.WhenAll(saveTasks).ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var window in snapshot)
            {
                window.Close();
            }

            _windows.Clear();
        });
    }

    public async Task CloseAllWithoutSaveAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var window in _windows.Values.ToList())
            {
                window.Close();
            }

            _windows.Clear();
        });
    }

    private async Task TrySaveWindowStateAsync(DesktopBox model, DesktopBoxWindow window, string currentPath)
    {
        try
        {
            await SaveWindowStateAsync(model, window, currentPath).ConfigureAwait(false);
        }
        catch
        {
            // swallow to prevent state-save failures from crashing close/hide flows.
            // consider plugging in a logging service if available
        }
    }

    private async Task SaveWindowStateAsync(DesktopBox model, DesktopBoxWindow window, string currentPath)
    {
        var state = await Dispatcher.UIThread.InvokeAsync(() => WindowStateData.FromWindow(model.Id, window, currentPath));
        _windowStates[model.Id] = state;
        model.Width = state.Width;
        model.Height = state.Height;
        model.PositionX = double.IsNaN(state.X) ? null : state.X;
        model.PositionY = double.IsNaN(state.Y) ? null : state.Y;
        model.CurrentPath = currentPath;
        var updated = await AppServices.BoxService.AddOrUpdateAsync(model).ConfigureAwait(false);
        AppServices.NotifyBoxUpdated(updated);
    }

    private static void RestorePath(DesktopBoxWindowViewModel vm, string? path)
    {
        if (string.IsNullOrEmpty(path) || path == "Desktop")
        {
            vm.NavigateHome();
            return;
        }

        var segments = path.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            vm.NavigateHome();
            return;
        }

        vm.NavigateHome();
        foreach (var segment in segments)
        {
            var folder = vm.CurrentItems.FirstOrDefault(item => string.Equals(item.FileName, segment, StringComparison.OrdinalIgnoreCase));
            if (folder?.IsFolder == true)
            {
                vm.EnterFolder(folder);
            }
            else
            {
                break;
            }
        }
    }

    private record struct WindowStateData(Guid BoxId, double Width, double Height, double X, double Y, string? Path)
    {
        public static WindowStateData FromWindow(Guid id, DesktopBoxWindow window, string currentPath)
        {
            var position = window.LastKnownPosition;
            return new WindowStateData(id, window.Width, window.Height,
                position.X,
                position.Y,
                currentPath);
        }
    }

    private static List<ScannedFile> BuildShortcutList(DesktopBox box, List<ScannedFile> allFiles, IReadOnlyList<ScannedFileService.StoredShortcut> storedShortcuts)
    {
        return ShortcutCatalog.GetBoxShortcuts(box, allFiles, storedShortcuts).ToList();
    }
}