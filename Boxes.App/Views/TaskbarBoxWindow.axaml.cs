using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
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
        
        // Apply desktop icon metrics
        ApplyDesktopIconMetrics();
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
        var windowWidthPx = (int)Math.Round(Bounds.Width * RenderScaling);
        var clampedX = Math.Clamp(newX, working.X, working.Right - windowWidthPx);

        // Check for snap targets
        var snapTargetX = GetSnapTargetX(clampedX);
        if (snapTargetX.HasValue)
        {
            clampedX = snapTargetX.Value;
            // Ensure snap doesn't go outside working area
            clampedX = Math.Clamp(clampedX, working.X, working.Right - windowWidthPx);
        }

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

    private int? GetSnapTargetX(int proposedX)
    {
        const int snapThreshold = 20; // pixels
        const int snapGap = 15; // pixels

        // Convert window width from logical to physical pixels
        var windowWidthPx = (int)Math.Round(Bounds.Width * RenderScaling);
        var currentLeft = proposedX;
        var currentRight = proposedX + windowWidthPx;

        var otherWindows = AppServices.BoxWindowManager.GetOtherTaskbarWindows(this);
        
        int? bestSnapTarget = null;
        int minDistance = int.MaxValue;

        foreach (var otherWindow in otherWindows)
        {
            if (!otherWindow.IsVisible)
                continue;

            var otherLeft = otherWindow.Position.X;
            var otherWidthPx = (int)Math.Round(otherWindow.Bounds.Width * otherWindow.RenderScaling);
            var otherRight = otherLeft + otherWidthPx;

            // Check if current window's left edge is near other window's right edge
            // Snap current window to be 15px to the right of the other window
            var distanceToRight = Math.Abs(currentLeft - otherRight);
            if (distanceToRight <= snapThreshold && distanceToRight < minDistance)
            {
                minDistance = distanceToRight;
                bestSnapTarget = otherRight + snapGap;
            }

            // Check if current window's right edge is near other window's left edge
            // Snap current window so its right edge is 15px to the left of the other window
            var distanceToLeft = Math.Abs(currentRight - otherLeft);
            if (distanceToLeft <= snapThreshold && distanceToLeft < minDistance)
            {
                minDistance = distanceToLeft;
                bestSnapTarget = otherLeft - windowWidthPx - snapGap;
            }
        }

        return bestSnapTarget;
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

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Stop event propagation so button clicks don't trigger window dragging
        e.Handled = true;
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
    
    private void ApplyDesktopIconMetrics()
    {
        var shortcutsControl = this.FindControl<ItemsControl>("ShortcutsItemsControl");
        if (shortcutsControl == null)
            return;

        var metrics = DesktopIconMetricsService.GetDesktopIconMetrics();
        
        // Apply metrics immediately and also on Loaded event to ensure it takes effect
        void ApplyMetrics()
        {
            // Find the WrapPanel in the visual tree
            var wrapPanel = shortcutsControl.GetVisualDescendants()
                .OfType<WrapPanel>()
                .FirstOrDefault();
            
            if (wrapPanel != null)
            {
                wrapPanel.ItemWidth = metrics.HorizontalSpacing;
                wrapPanel.ItemHeight = metrics.VerticalSpacing;
            }
        }
        
        // Try immediately if control is already loaded
        if (shortcutsControl.IsLoaded)
        {
            ApplyMetrics();
        }
        
        // Also subscribe to Loaded event for when control loads later
        shortcutsControl.Loaded += (s, e) =>
        {
            ApplyMetrics();
        };
        
        // Subscribe to LayoutUpdated as a fallback to catch any timing issues
        shortcutsControl.LayoutUpdated += (s, e) =>
        {
            var wrapPanel = shortcutsControl.GetVisualDescendants()
                .OfType<WrapPanel>()
                .FirstOrDefault();
            
            if (wrapPanel != null && (wrapPanel.ItemWidth != metrics.HorizontalSpacing || wrapPanel.ItemHeight != metrics.VerticalSpacing))
            {
                wrapPanel.ItemWidth = metrics.HorizontalSpacing;
                wrapPanel.ItemHeight = metrics.VerticalSpacing;
            }
        };
        
        // Store metrics in resources for potential future use
        if (Resources == null)
        {
            Resources = new ResourceDictionary();
        }
        
        Resources["IconSize"] = metrics.IconSize;
        Resources["HorizontalSpacing"] = metrics.HorizontalSpacing;
        Resources["VerticalSpacing"] = metrics.VerticalSpacing;
    }
}


