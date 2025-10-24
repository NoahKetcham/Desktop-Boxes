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
        AppServices.Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            AppServices.MainWindowOwner = desktop.MainWindow;
            DialogService.Initialize(desktop.MainWindow);

            DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            _desktopIntegrationCts = new CancellationTokenSource();
            DesktopIntegrationService.StartCommandListener(HandleDesktopCommandAsync, _desktopIntegrationCts.Token);
            desktop.Exit += OnDesktopExit;
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