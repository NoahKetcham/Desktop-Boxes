using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Boxes.App.ViewModels;
using Avalonia.Interactivity;
using Boxes.App.Services;
using Boxes.App.Models;
using Avalonia.Threading;
using Avalonia.Media;

namespace Boxes.App.Views;

public partial class TaskbarBoxWindow : Window
{
    private bool _dragging;
    private PixelPoint _startWindow;
    private Point _startPointer;
    private Border? _contentArea;
    private Grid? _rootGrid;
    private Border? _headerBar;

    public TaskbarBoxWindow()
    {
        InitializeComponent();
        HookDataContext();
        _contentArea = this.FindControl<Border>("ContentArea");
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _headerBar = this.FindControl<Border>("HeaderBar");
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
        ApplyTransparencyFromSettings();
    }

    internal TaskbarBoxWindowViewModel ViewModel => (TaskbarBoxWindowViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        HookDataContext();
    }

    private void HookDataContext()
    {
        if (DataContext is TaskbarBoxWindowViewModel vm)
        {
            vm.ToggleExpandRequested -= OnToggleExpandRequested;
            vm.ToggleExpandRequested += OnToggleExpandRequested;
        }
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings e)
    {
        ApplyTransparency(e);
    }

    private void ApplyTransparencyFromSettings()
    {
        var _ = AppServices.SettingsService.GetAsync().ContinueWith(t =>
        {
            if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result is { } s)
            {
                ApplyTransparency(s);
            }
        });
    }

    private void ApplyTransparency(ApplicationSettings settings)
    {
        var opacity = Math.Clamp(settings.BoxesTransparencyPercent, 0, 100) / 100.0;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rootGrid != null)
            {
                if (_rootGrid.Background is ISolidColorBrush gridBg)
                    _rootGrid.Background = new SolidColorBrush(gridBg.Color, opacity);
                else
                    _rootGrid.Background = new SolidColorBrush(Color.Parse("#1C2235"), opacity);
            }

            if (_headerBar != null)
            {
                if (_headerBar.Background is ISolidColorBrush headerBg)
                    _headerBar.Background = new SolidColorBrush(headerBg.Color, opacity);
                else
                    _headerBar.Background = new SolidColorBrush(Color.Parse("#232B46"), opacity);
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }

    private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = false; // become true only after threshold movement
        _startWindow = Position;
        _startPointer = e.GetPosition(this);
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void Header_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // If not currently dragging, ignore hover moves unless the left button is pressed
        if (!_dragging && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _startPointer.X;
        if (!_dragging)
        {
            if (Math.Abs(deltaX) < 6)
            {
                return; // still within click threshold
            }
            _dragging = true; // crossed threshold: start dragging
        }

        var newX = _startWindow.X + (int)deltaX;
        var working = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var clampedX = Math.Clamp(newX, working.X, working.Right - (int)Bounds.Width);

        int y;
        if (Boxes.App.Extensions.TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out _))
        {
            var heightPx = (int)Math.Round(Bounds.Height * RenderScaling);
            y = taskbarTop - heightPx;
        }
        else
        {
            var heightPx = (int)Math.Round(Bounds.Height * RenderScaling);
            y = working.Bottom - heightPx;
        }
        Position = new PixelPoint(clampedX, y);
        e.Handled = true;
    }

    private void Header_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            // Treat as click only if very small movement since press
            var delta = Math.Abs(e.GetPosition(this).X - _startPointer.X);
            if (e.InitialPressMouseButton == MouseButton.Left && delta < 6)
                ViewModel.ToggleExpanded();
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        // Persist X position after drag ends
        _ = Boxes.App.Services.AppServices.BoxWindowManager.SaveTaskbarWindowXAsync(ViewModel.Model.Id, Position.X);
    }

    private void Header_OnTapped(object? sender, TappedEventArgs e)
    {
        // Ensure tap also toggles when no drag is in progress
        if (!_dragging)
        {
            ViewModel.ToggleExpanded();
            e.Handled = true;
        }
    }

    private async void OnToggleExpandRequested(object? sender, bool expanded)
    {
        await Boxes.App.Services.AppServices.BoxWindowManager.SetSnappedExpandedAsync(ViewModel.Model.Id, expanded);
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ViewModel.IsExpanded)
        {
            return;
        }

        if (sender is Border border && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            WindowEdge? edge = border.Tag switch
            {
                "Left" => WindowEdge.West,
                "Right" => WindowEdge.East,
                "Top" => WindowEdge.North,
                "TopLeft" => WindowEdge.NorthWest,
                "TopRight" => WindowEdge.NorthEast,
                _ => null
            };

            if (edge.HasValue)
            {
                BeginResizeDrag(edge.Value, e);
                e.Handled = true;
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Keep bottom edge anchored to taskbar while resizing
        var working = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        int y;
        var heightPx = (int)Math.Round(Bounds.Height * RenderScaling);
        if (Boxes.App.Extensions.TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out _))
        {
            y = taskbarTop - heightPx;
        }
        else
        {
            y = working.Bottom - heightPx;
        }
        Position = new PixelPoint(Position.X, y);

        // Persist size when in expanded state
        if (ViewModel.IsExpanded)
        {
            _ = Boxes.App.Services.AppServices.BoxWindowManager.SaveTaskbarExpandedHeightAsync(ViewModel.Model.Id, Height);
            _ = Boxes.App.Services.AppServices.BoxWindowManager.SaveTaskbarWidthAsync(ViewModel.Model.Id, Width);
            _ = Boxes.App.Services.AppServices.BoxWindowManager.SaveTaskbarWindowXAsync(ViewModel.Model.Id, Position.X);
        }
    }

    // Drag & drop support (reuse DesktopBoxWindow handlers)
    private void Content_OnDragEnter(object? sender, DragEventArgs e)
    {
        ViewModel.HandleDragEvent(e);
        if (!e.Handled)
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private void Content_OnDragOver(object? sender, DragEventArgs e)
    {
        ViewModel.HandleDragEvent(e);
        if (!e.Handled)
        {
            e.DragEffects = DragDropEffects.Copy;
        }
    }

    private async void Content_OnDrop(object? sender, DragEventArgs e)
    {
        await ViewModel.HandleDropAsync(e);
    }

    private void Shortcut_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DesktopFileViewModel vm)
        {
            if (vm.IsFolder)
            {
                ViewModel.EnterFolderCommand.Execute(vm);
            }
            else
            {
                ViewModel.LaunchShortcutCommand.Execute(vm);
            }
        }
    }
}


