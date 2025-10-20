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

namespace Boxes.App.Services;

public class BoxWindowManager
{
    private readonly Dictionary<Guid, DesktopBoxWindow> _windows = new();
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

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
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

            window.Closed += async (_, _) =>
            {
                await SaveWindowStateAsync(vm.Model, window, vm.CurrentPath).ConfigureAwait(false);
                _windows.Remove(box.Id);
                vm.RequestClose -= handler;
            };
            _windows[box.Id] = window;

            if (_windowStates.TryGetValue(box.Id, out var state))
            {
                RestorePath(vm, state.Path);
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