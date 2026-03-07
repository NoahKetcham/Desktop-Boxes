using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Boxes.App.Services;
using Boxes.App.ViewModels;
using Boxes.App.Views;

namespace Boxes.App;

public partial class App : Application
{
    private CancellationTokenSource? _desktopIntegrationCts;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // #region agent log
        Program.DebugLog("App.axaml.cs:OnFrameworkInitializationCompleted:entry", "OnFrameworkInitializationCompleted entered", "H1");
        // #endregion

        // Run initialization off the UI thread to avoid deadlock when blocking on async service init
        // (GetAwaiter().GetResult() on UI thread can deadlock with async code under debugger/STA)
        Task.Run(() => AppServices.Initialize()).GetAwaiter().GetResult();

        // #region agent log
        Program.DebugLog("App.axaml.cs:OnFrameworkInitializationCompleted:afterInit", "AppServices.Initialize() completed", "H1");
        // #endregion

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Check if there are any boxes to determine startup behavior
            var boxes = Task.Run(() => AppServices.BoxService.GetBoxesAsync()).GetAwaiter().GetResult();
            var hasBoxes = boxes.Count > 0;

            // Prevent app from closing when all windows are closed (boxes run without main window)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // #region agent log
            Program.DebugLog("App.axaml.cs:OnFrameworkInitializationCompleted:creatingWindow", "About to create MainWindow", "H3");
            // #endregion

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            // #region agent log
            Program.DebugLog("App.axaml.cs:OnFrameworkInitializationCompleted:windowCreated", "MainWindow created successfully", "H3");
            // #endregion

            AppServices.MainWindowOwner = mainWindow;
            DialogService.Initialize(mainWindow);

            if (!hasBoxes)
            {
                // No boxes exist - show the main window so user can create some
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            }
            // If boxes exist, main window stays hidden - user can access via right-click menu on boxes
            // We don't set desktop.MainWindow to avoid auto-show behavior

            DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            DesktopIntegrationService.SetupJumpListTasks();
            _desktopIntegrationCts = new CancellationTokenSource();
            DesktopIntegrationService.StartCommandListener(HandleDesktopCommandAsync, _desktopIntegrationCts.Token);
            desktop.Exit += OnDesktopExit;

            var pendingCommand = Program.PendingCommand;
            Program.PendingCommand = null;
            if (string.Equals(pendingCommand, "opennotepad", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    var notepads = await AppServices.NotepadCatalogService.GetNotepadsAsync();
                    if (notepads.Count > 0)
                    {
                        await AppServices.WidgetWindowManager.ShowNotepadAsync(notepads[0]);
                    }
                });
            }
            else if (string.Equals(pendingCommand, "opencommandcenter", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await AppServices.WidgetWindowManager.ShowCommandCenterAsync();
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Task HandleDesktopCommandAsync(string command)
    {
        if (string.Equals(command, "hide", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppServices.BoxWindowManager.SetWindowsVisibility(false);
                DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            });
        }
        else if (string.Equals(command, "show", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppServices.BoxWindowManager.SetWindowsVisibility(true);
                DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            });
        }
        else if (string.Equals(command, "toggle", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppServices.BoxWindowManager.ToggleAllWindowsVisibility();
                DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            });
        }
        else if (string.Equals(command, "showburst", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppServices.BoxWindowManager.ToggleBurstAsync(TimeSpan.FromSeconds(5));
            });
        }
        else if (string.Equals(command, "opennotepad", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var notepads = await AppServices.NotepadCatalogService.GetNotepadsAsync();
                if (notepads.Count > 0)
                {
                    await AppServices.WidgetWindowManager.ShowNotepadAsync(notepads[0]);
                }
            });
        }
        else if (string.Equals(command, "opencommandcenter", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await AppServices.WidgetWindowManager.ShowCommandCenterAsync();
            });
        }
        else if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            });
        }

        return Task.CompletedTask;
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _desktopIntegrationCts?.Cancel();
        _desktopIntegrationCts?.Dispose();
        _desktopIntegrationCts = null;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}