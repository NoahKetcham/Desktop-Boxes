using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Boxes.App.ViewModels;
using Boxes.App.Views;

namespace Boxes.App.Services;

public static class AppServices
{
    private static bool _initialized;
    private static readonly object SyncRoot = new();

    public static BoxService BoxService { get; private set; } = null!;
    public static SettingsService SettingsService { get; private set; } = null!;
    public static BoxWindowManager BoxWindowManager { get; } = new();
    public static ScannedFileService ScannedFileService { get; private set; } = null!;
    public static DesktopCleanupService DesktopCleanupService { get; private set; } = null!;
    public static Window? MainWindowOwner { get; set; }

    private static MainWindowViewModel? _mainWindowViewModel;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var rootDirectory = Path.Combine(appData, "Boxes");
            Directory.CreateDirectory(rootDirectory);

            SettingsService = new SettingsService(rootDirectory);
            SettingsService.InitializeAsync().GetAwaiter().GetResult();

            BoxService = new BoxService(rootDirectory);
            BoxService.InitializeAsync().GetAwaiter().GetResult();

            ScannedFileService = new ScannedFileService(rootDirectory);
            DesktopCleanupService = new DesktopCleanupService(rootDirectory);

            _initialized = true;
        }
    }

    public static Window CreateMainWindow()
    {
        var window = new MainWindow
        {
            DataContext = GetOrCreateMainWindowViewModel()
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(MainWindowOwner, window))
            {
                MainWindowOwner = null;
            }
        };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = window;
        }

        MainWindowOwner = window;
        DialogService.Initialize(window);

        return window;
    }

    public static async Task OpenSettingsWindowAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = MainWindowOwner ?? CreateMainWindow();

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            if (window.DataContext is MainWindowViewModel vm)
            {
                vm.NavigateToSettings();
            }

            if (!window.IsVisible)
            {
                window.Show();
            }

            window.Activate();
        });
    }

    private static MainWindowViewModel GetOrCreateMainWindowViewModel()
    {
        if (MainWindowOwner?.DataContext is MainWindowViewModel vmFromWindow)
        {
            _mainWindowViewModel = vmFromWindow;
        }

        _mainWindowViewModel ??= new MainWindowViewModel();
        return _mainWindowViewModel;
    }
}

