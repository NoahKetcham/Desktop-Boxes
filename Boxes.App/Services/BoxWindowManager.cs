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
    private readonly Dictionary<Guid, (double Width, double Height, double X, double Y)> _windowStates = new();
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

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (visible)
            {
                foreach (var window in _windows.Values)
                {
                    if (_windowStates.TryGetValue(window.ViewModel.Model.Id, out var state))
                    {
                        window.Width = state.Width;
                        window.Height = state.Height;
                        window.Position = new PixelPoint((int)state.X, (int)state.Y);
                    }

                    window.Show();
                    window.Activate();
                }
            }
            else
            {
                foreach (var window in _windows.Values)
                {
                    _windowStates[window.ViewModel.Model.Id] = (window.Width, window.Height, window.Position.X, window.Position.Y);
                    window.Hide();
                }
            }

            _areWindowsVisible = visible;
        });
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
                    vmExisting.SetShortcuts(filteredShortcuts);
                }
                if (!existing.IsVisible)
                {
                    existing.Show();
                }
                existing.Activate();
                return;
            }

            var vm = new DesktopBoxWindowViewModel(box);
            vm.SetShortcuts(filteredShortcuts);
            var window = new DesktopBoxWindow
            {
                DataContext = vm
            };

            EventHandler? handler = null;
            handler = (_, _) => window.Close();
            vm.RequestClose += handler;

            window.Closed += (_, _) =>
            {
                _windows.Remove(box.Id);
                vm.RequestClose -= handler;
            };
            _windows[box.Id] = window;

            window.Show();
            window.Activate();
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
                vm.SetShortcuts(filteredShortcuts);
            }
        });
    }

    public async Task CloseAsync(Guid id)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windows.TryGetValue(id, out var window))
            {
                window.Close();
                _windows.Remove(id);
            }
        });
    }

    public async Task CloseAllAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var window in _windows.Values)
            {
                window.Close();
            }

            _windows.Clear();
        });
    }
    private static List<ScannedFile> BuildShortcutList(DesktopBox box, List<ScannedFile> allFiles, IReadOnlyList<ScannedFileService.StoredShortcut> storedShortcuts)
    {
        if (box.ShortcutIds.Count == 0)
        {
            return new List<ScannedFile>();
        }

        var archiveDirectory = AppServices.ScannedFileService.ShortcutArchiveDirectory;
        var storedLookup = storedShortcuts.ToDictionary(s => s.Id, s => s);
        var allLookup = allFiles.ToDictionary(f => f.Id, f => f);

        foreach (var file in allFiles)
        {
            if (storedLookup.TryGetValue(file.Id, out var stored))
            {
                file.ShortcutPath = Path.Combine(archiveDirectory, stored.Id.ToString("N") + ".lnk");
                file.ItemType = stored.ItemType;
                if (stored.ParentId.HasValue)
                {
                    file.ParentId = stored.ParentId;
                }
            }
        }

        var selected = new HashSet<Guid>(box.ShortcutIds);

        bool IsDescendantSelected(ScannedFile file)
        {
            var current = file.ParentId;
            while (current.HasValue)
            {
                if (selected.Contains(current.Value))
                {
                    return true;
                }

                if (!allLookup.TryGetValue(current.Value, out var parent))
                {
                    break;
                }

                current = parent.ParentId;
            }

            return false;
        }

        return allFiles.Where(file => selected.Contains(file.Id) || IsDescendantSelected(file)).ToList();
    }
}

