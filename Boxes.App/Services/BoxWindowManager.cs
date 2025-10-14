using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.ViewModels;
using Boxes.App.Views;

namespace Boxes.App.Services;

public class BoxWindowManager
{
    private readonly Dictionary<Guid, DesktopBoxWindow> _windows = new();

    public bool HasOpenWindows => _windows.Count > 0;

    public async Task ShowAsync(DesktopBox box)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windows.TryGetValue(box.Id, out var existing))
            {
                if (existing.DataContext is DesktopBoxWindowViewModel vmExisting)
                {
                    vmExisting.Update(box);
                }
                if (!existing.IsVisible)
                {
                    existing.Show();
                }
                existing.Activate();
                return;
            }

            var vm = new DesktopBoxWindowViewModel(box);
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
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windows.TryGetValue(box.Id, out var window) && window.DataContext is DesktopBoxWindowViewModel vm)
            {
                vm.Update(box);
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
}

