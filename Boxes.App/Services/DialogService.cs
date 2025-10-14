using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.Views.Dialogs;

namespace Boxes.App.Services;

public static class DialogService
{
    private static Window? _mainWindow;

    public static void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public static Task<DesktopBox?> ShowNewBoxDialogAsync()
    {
        EnsureInitialized();
        return DispatchAsync(async () =>
        {
            var dialog = new NewBoxWindow();
            return await dialog.ShowDialog<DesktopBox?>(_mainWindow!);
        });
    }

    public static Task<bool> ShowDeleteConfirmationAsync(string boxName)
    {
        EnsureInitialized();
        return DispatchAsync(async () =>
        {
            var dialog = new ConfirmDeleteWindow(boxName);
            var result = await dialog.ShowDialog<bool?>(_mainWindow!);
            return result == true;
        });
    }

    private static void EnsureInitialized()
    {
        if (_mainWindow == null)
        {
            throw new InvalidOperationException("DialogService has not been initialized with a main window.");
        }
    }

    private static Task<T> DispatchAsync<T>(Func<Task<T>> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return func();
        }

        var tcs = new TaskCompletionSource<T>();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var result = await func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}

