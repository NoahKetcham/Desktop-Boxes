using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Boxes.App.Services;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _debouncedSaveTimer;

    public MainWindow()
    {
        InitializeComponent();
        LoadSavedBounds();
        PositionChanged += MainWindow_PositionChanged;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void LoadSavedBounds()
    {
        try
        {
            var settings = AppServices.SettingsService.GetAsync().GetAwaiter().GetResult();
            if (settings.MainWindowPositionX.HasValue && settings.MainWindowPositionY.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint((int)settings.MainWindowPositionX.Value, (int)settings.MainWindowPositionY.Value);
            }
            if (settings.MainWindowWidth.HasValue && settings.MainWindowWidth.Value >= MinWidth)
            {
                Width = settings.MainWindowWidth.Value;
            }
            if (settings.MainWindowHeight.HasValue && settings.MainWindowHeight.Value >= MinHeight)
            {
                Height = settings.MainWindowHeight.Value;
            }
        }
        catch
        {
            // Use default position/size on first run or if loading fails
        }
    }

    private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        ScheduleDebouncedSave();
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ScheduleDebouncedSave();
    }

    private void ScheduleDebouncedSave()
    {
        _debouncedSaveTimer?.Stop();
        _debouncedSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _debouncedSaveTimer.Tick += async (_, _) =>
        {
            _debouncedSaveTimer?.Stop();
            _debouncedSaveTimer = null;
            try
            {
                var settings = await AppServices.SettingsService.GetAsync();
                settings.MainWindowPositionX = Position.X;
                settings.MainWindowPositionY = Position.Y;
                settings.MainWindowWidth = Width;
                settings.MainWindowHeight = Height;
                await AppServices.SettingsService.SaveAsync(settings);
            }
            catch
            {
                // Ignore save failures
            }
        };
        _debouncedSaveTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _debouncedSaveTimer?.Stop();
        _debouncedSaveTimer = null;
        base.OnClosed(e);
    }

    private void OnDeselectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.SidebarContent is DashboardBoxSettingsViewModel dashboardSidebar)
        {
            dashboardSidebar.DismissTemplateInfoOrClear();
        }
    }
}