using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.ViewModels;
using Boxes.App.Views.Dialogs;

namespace Boxes.App.Services;

public static class DialogService
{
    public static void Initialize(Window mainWindow)
    {
        // DialogService now uses AppServices.MainWindowOwner as the single source of truth
        // This ensures we always use the same window instance
        AppServices.MainWindowOwner = mainWindow;
    }

    /// <summary>
    /// Gets the appropriate parent window for dialogs.
    /// Ensures the window is visible before returning it, as ShowDialog requires a visible parent.
    /// </summary>
    private static Window? GetDialogParent()
    {
        var window = AppServices.MainWindowOwner;
        
        if (window == null)
        {
            return null;
        }

        // ShowDialog requires the parent window to be visible
        if (!window.IsVisible)
        {
            window.Show();
        }

        return window;
    }

    public static Task<CreateItemResult?> ShowNewBoxDialogAsync(CreateItemType defaultType = CreateItemType.Box)
    {
        return DispatchAsync(async () =>
        {
            var dialog = new NewBoxWindow(defaultType);
            var parent = GetDialogParent();
            if (parent != null)
            {
                return await dialog.ShowDialog<CreateItemResult?>(parent);
            }
            dialog.Show();
            return null;
        });
    }

    public static Task<bool> ShowDeleteConfirmationAsync(string boxName)
    {
        return DispatchAsync(async () =>
        {
            var dialog = new ConfirmDeleteWindow(boxName);
            var parent = GetDialogParent();
            if (parent != null)
            {
                var result = await dialog.ShowDialog<bool?>(parent);
                return result == true;
            }
            dialog.Show();
            return false;
        });
    }

    public static Task<bool> ShowConfirmationAsync(string message)
    {
        return DispatchAsync(async () =>
        {
            var dialog = new ConfirmationDialog();
            dialog.ViewModel.Message = message;
            var parent = GetDialogParent();
            if (parent != null)
            {
                var result = await dialog.ShowDialog<bool?>(parent);
                return result == true;
            }
            dialog.Show();
            return false;
        });
    }

    public static Task<string?> ShowDesktopBuildNameDialogAsync()
    {
        return DispatchAsync(async () =>
        {
            var dialog = new DesktopBuildNameDialog();
            var parent = GetDialogParent();
            if (parent != null)
            {
                return await dialog.ShowDialog<string?>(parent);
            }
            dialog.Show();
            return null;
        });
    }

    public static Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue = "", string watermark = "", string confirmButtonText = "OK")
    {
        return DispatchAsync(async () =>
        {
            var vm = new InputDialogViewModel
            {
                Title = title,
                Prompt = prompt,
                Value = defaultValue,
                Watermark = watermark,
                ConfirmButtonText = confirmButtonText
            };
            var dialog = new InputDialog { DataContext = vm };
            var parent = GetDialogParent();
            if (parent != null)
            {
                return await dialog.ShowDialog<string?>(parent);
            }
            dialog.Show();
            return null;
        });
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

