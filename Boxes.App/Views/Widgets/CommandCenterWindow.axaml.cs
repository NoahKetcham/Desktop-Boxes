using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.Services;
using Boxes.App.ViewModels.Widgets;

namespace Boxes.App.Views.Widgets;

public partial class CommandCenterWindow : Window
{
    private Border? _outerBorder;
    private DispatcherTimer? _stateSaveTimer;

    public CommandCenterWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Activated += (_, _) =>
        {
            if (!AppServices.BoxWindowManager.IsBurstActive)
            {
                this.SetAlwaysBelowApps();
            }
        };
        PositionChanged += (_, _) => ScheduleStateSave();
        _outerBorder = this.FindControl<Border>("OuterBorder");
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        this.SetAlwaysBelowApps();
        this.HideDwmBorder();
        ApplySettingsFromService();
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings settings)
    {
        ApplyAllSettings(settings);
    }

    private void ApplySettingsFromService()
    {
        _ = AppServices.SettingsService.GetAsync().ContinueWith(task =>
        {
            if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result is { } settings)
            {
                ApplyAllSettings(settings);
            }
        });
    }

    private void ApplyAllSettings(ApplicationSettings settings)
    {
        var opacity = Math.Clamp(settings.BoxesTransparencyPercent, 0, 100) / 100.0;
        var backgroundColorHex = settings.BoxBackgroundColor ?? "#1C2235";

        Dispatcher.UIThread.Post(() =>
        {
            if (_outerBorder == null)
            {
                return;
            }

            if (!Color.TryParse(backgroundColorHex, out var bgColor))
            {
                bgColor = Color.Parse("#1C2235");
            }

            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 24);
            _outerBorder.CornerRadius = new CornerRadius(cornerRadius);
            _outerBorder.Background = new SolidColorBrush(bgColor, opacity);

            // Force a borderless shell so no outline is visible.
            _outerBorder.BorderBrush = Brushes.Transparent;
            _outerBorder.BorderThickness = new Thickness(0);
        });
    }

    private void ScheduleStateSave()
    {
        _stateSaveTimer?.Stop();
        _stateSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _stateSaveTimer.Tick += async (_, _) =>
        {
            _stateSaveTimer?.Stop();
            _stateSaveTimer = null;
            await AppServices.WidgetWindowManager.SaveCommandCenterStateAsync(this).ConfigureAwait(false);
        };
        _stateSaveTimer.Start();
    }

    private void Chrome_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        WindowEdge? edge = border.Tag switch
        {
            "Right" => WindowEdge.East,
            "Bottom" => WindowEdge.South,
            "BottomRight" => WindowEdge.SouthEast,
            _ => null
        };

        if (edge.HasValue)
        {
            BeginResizeDrag(edge.Value, e);
            e.Handled = true;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ScheduleStateSave();
    }

    protected override void OnClosed(EventArgs e)
    {
        _stateSaveTimer?.Stop();
        _stateSaveTimer = null;

        if (DataContext is CommandCenterWindowViewModel vm)
        {
            vm.Dispose();
        }

        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }
}
