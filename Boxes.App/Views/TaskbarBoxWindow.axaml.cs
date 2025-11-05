using System;
using System.Collections.Generic;
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
using Boxes.App.Extensions;

namespace Boxes.App.Views;

public partial class TaskbarBoxWindow : Window
{
    private bool _dragging;
    private PixelPoint _startWindow;
    private PixelPoint _startScreenPosition;
    private PixelPoint _lastWindowPosition;
    private Border? _contentArea;
    private Grid? _rootGrid;
    private Border? _headerBar;
    private DragLayoutContext? _dragLayoutContext;
    private const double LayoutGap = 15d;
    private readonly TimeSpan _dragReflowInterval = TimeSpan.FromMilliseconds(12);
    private DateTime _lastReflow = DateTime.MinValue;
    private const double HysteresisPx = 5.0;
    private int? _stableInsertionIndex;

    public TaskbarBoxWindow()
    {
        InitializeComponent();
        Opened += (_, __) => this.SetAlwaysBelowApps();
        Activated += (_, __) => { if (!AppServices.BoxWindowManager.IsBurstActive) this.SetAlwaysBelowApps(); };
        PointerEntered += (_, __) => AppServices.BoxWindowManager.NotifyHover();
        AddHandler(InputElement.PointerMovedEvent, OnPointerHoverActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(InputElement.PointerEnteredEvent, OnPointerHoverActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
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
        var backgroundColorHex = settings.BoxBackgroundColor ?? "#1C2235";
        Dispatcher.UIThread.Post(() =>
        {
            Color bgColor;
            if (Color.TryParse(backgroundColorHex, out var parsedColor))
            {
                bgColor = parsedColor;
            }
            else
            {
                bgColor = Color.Parse("#1C2235");
            }

            if (_rootGrid != null)
            {
                _rootGrid.Background = new SolidColorBrush(bgColor, opacity);
            }

            if (_headerBar != null)
            {
                // Calculate relative luminance to determine if background is dark or light
                // Relative luminance formula: L = 0.2126*R + 0.7152*G + 0.0722*B
                var r = bgColor.R / 255.0;
                var g = bgColor.G / 255.0;
                var b = bgColor.B / 255.0;
                var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                
                Color headerColor;
                if (luminance < 0.5)
                {
                    // Dark background: lighten the header bar (~10% lighter)
                    headerColor = Color.FromRgb(
                        (byte)Math.Min(255, bgColor.R + (255 - bgColor.R) * 0.1),
                        (byte)Math.Min(255, bgColor.G + (255 - bgColor.G) * 0.1),
                        (byte)Math.Min(255, bgColor.B + (255 - bgColor.B) * 0.1)
                    );
                }
                else
                {
                    // Light background: darken the header bar (~20% darker)
                    headerColor = Color.FromRgb(
                        (byte)(bgColor.R * 0.8),
                        (byte)(bgColor.G * 0.8),
                        (byte)(bgColor.B * 0.8)
                    );
                }
                _headerBar.Background = new SolidColorBrush(headerColor, opacity);
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

        // Convert window-relative pointer position to screen coordinates
        var pointerRelative = e.GetPosition(this);
        var screenX = Position.X + (int)Math.Round(pointerRelative.X);
        var screenY = Position.Y + (int)Math.Round(pointerRelative.Y);

        _dragging = false; // become true only after threshold movement
        _startWindow = Position;
        _lastWindowPosition = Position;
        _startScreenPosition = new PixelPoint(screenX, screenY);
        CaptureDragLayoutContext();
        _stableInsertionIndex = _dragLayoutContext?.OriginalIndex;
        _lastReflow = DateTime.MinValue;
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

        // Compute current pointer position in screen coordinates using last known window position
        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);

        // Horizontal delta from initial press in screen coordinates
        var deltaX = currentScreenPos.X - _startScreenPosition.X;

        if (!_dragging)
        {
            if (Math.Abs(deltaX) < 6)
            {
                return; // still within click threshold
            }
            _dragging = true; // crossed threshold
        }

        var proposedX = _startWindow.X + deltaX;
        var monitor = GetMonitorBoundsForWindow(this);
        var windowWidthPx = (int)Math.Round(Bounds.Width * RenderScaling);
        var clampedX = Math.Clamp(proposedX, monitor.X, monitor.Right - windowWidthPx);

        // Anchor to taskbar vertically
        int y;
        if (Boxes.App.Extensions.TaskbarMetrics.TryGetPrimaryTaskbarTop(out var taskbarTop, out _))
        {
            var heightPx = (int)Math.Round(Bounds.Height * RenderScaling);
            y = taskbarTop - heightPx;
        }
        else
        {
            var heightPx = (int)Math.Round(Bounds.Height * RenderScaling);
            y = monitor.Bottom - heightPx;
        }

        var newPos = new PixelPoint(clampedX, y);
        _lastWindowPosition = newPos;
        Position = newPos;

        var now = DateTime.UtcNow;
        if (now - _lastReflow >= _dragReflowInterval)
        {
            LiveReflowDuringDrag(clampedX);
            _lastReflow = now;
        }

        e.Handled = true;
    }

    

    private void Header_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            // Treat as click only if very small movement since press
            var currentPointerRelative = e.GetPosition(this);
            var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
            var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
            var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);
            var delta = Math.Abs(currentScreenPos.X - _startScreenPosition.X);
            if (e.InitialPressMouseButton == MouseButton.Left && delta < 6)
                ViewModel.ToggleExpanded();
            return;
        }
        HandleAutoShiftOnDrop();

        _dragging = false;
        _stableInsertionIndex = null;
        
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
    
    private void OnPointerHoverActivity(object? sender, PointerEventArgs e)
    {
        if (AppServices.BoxWindowManager.IsBurstActive)
        {
            AppServices.BoxWindowManager.NotifyHover();
        }
    }

    private void OnPointerWheelActivity(object? sender, PointerWheelEventArgs e)
    {
        if (AppServices.BoxWindowManager.IsBurstActive)
        {
            AppServices.BoxWindowManager.NotifyHover();
        }
    }
    
    private void HandleAutoShiftOnDrop()
    {
        if (_dragLayoutContext is not { } context || ViewModel?.Model is null)
        {
            _dragLayoutContext = null;
            return;
        }

        var allWindows = AppServices.BoxWindowManager.GetAllTaskbarWindows()
            .Where(w => w.IsVisible)
            .ToList();

        if (allWindows.Count <= 1)
        {
            _dragLayoutContext = null;
            return;
        }

        var monitorBounds = GetMonitorBoundsForWindow(this);
        var draggedBox = CreateLayout(this);
        draggedBox.X = Math.Clamp(Position.X, monitorBounds.X, monitorBounds.Right - draggedBox.Width);

        var others = new List<BoxLayout>(allWindows.Count - 1);
        foreach (var window in allWindows)
        {
            if (ReferenceEquals(window, this))
            {
                continue;
            }

            others.Add(CreateLayout(window));
        }

        if (others.Count == 0)
        {
            _dragLayoutContext = null;
            return;
        }

        others.Sort(static (a, b) => a.Left.CompareTo(b.Left));

        var insertionIndex = ComputeInsertionIndex(others, draggedBox);
        insertionIndex = Math.Clamp(insertionIndex, 0, others.Count);

        var ordered = new List<BoxLayout>(others.Count + 1);
        ordered.AddRange(others);
        ordered.Insert(insertionIndex, draggedBox);

        var originalIndex = Math.Clamp(context.OriginalIndex, 0, ordered.Count - 1);
        var direction = Math.Sign(insertionIndex - originalIndex);

        var leftNeighbor = insertionIndex > 0 ? ordered[insertionIndex - 1] : null;
        var rightNeighbor = insertionIndex + 1 < ordered.Count ? ordered[insertionIndex + 1] : null;

        AlignDraggedWithinNeighbors(draggedBox, leftNeighbor, rightNeighbor, monitorBounds, direction);

        if (direction > 0)
        {
            EnsureRightSegment(ordered, insertionIndex, monitorBounds);
        }
        else if (direction < 0)
        {
            EnsureLeftSegment(ordered, insertionIndex, monitorBounds);
        }
        else
        {
            EnsureLeftSegment(ordered, insertionIndex, monitorBounds);
            EnsureRightSegment(ordered, insertionIndex, monitorBounds);
        }

        ordered.Sort(static (a, b) => a.Left.CompareTo(b.Left));
        if (!IsWithinBounds(ordered, monitorBounds))
        {
            PackWithinBounds(ordered, draggedBox, monitorBounds);
        }

        ApplyLayout(ordered);

        _dragLayoutContext = null;
    }

    private void LiveReflowDuringDrag(int proposedX)
    {
        // Gather all visible taskbar windows
        var allWindows = AppServices.BoxWindowManager.GetAllTaskbarWindows()
            .Where(w => w.IsVisible)
            .ToList();

        if (allWindows.Count <= 1 || ViewModel?.Model is null)
        {
            return;
        }

        var monitorBounds = GetMonitorBoundsForWindow(this);

        // Build dragged layout and clamp within monitor
        var dragged = CreateLayout(this);
        dragged.X = Math.Clamp(proposedX, monitorBounds.X, monitorBounds.Right - dragged.Width);

        // Build other layouts
        var others = new List<BoxLayout>(allWindows.Count - 1);
        foreach (var window in allWindows)
        {
            if (ReferenceEquals(window, this))
            {
                continue;
            }
            others.Add(CreateLayout(window));
        }

        if (others.Count == 0)
        {
            return;
        }

        others.Sort(static (a, b) => a.Left.CompareTo(b.Left));

        // Compute candidate insertion index by centers
        var candidate = ComputeInsertionIndex(others, dragged);

        // Hysteresis: only change stable index when crossing boundary ±HysteresisPx
        var prevStable = _stableInsertionIndex ?? candidate;
        var nextStable = prevStable;

        if (candidate > prevStable)
        {
            var boundaryCenter = prevStable < others.Count ? others[prevStable].Center : double.PositiveInfinity;
            if (dragged.Center >= boundaryCenter + HysteresisPx)
            {
                nextStable = candidate;
            }
        }
        else if (candidate < prevStable)
        {
            var boundaryCenter = prevStable - 1 >= 0 ? others[prevStable - 1].Center : double.NegativeInfinity;
            if (dragged.Center <= boundaryCenter - HysteresisPx)
            {
                nextStable = candidate;
            }
        }

        var direction = Math.Sign(nextStable - prevStable);
        _stableInsertionIndex = Math.Clamp(nextStable, 0, others.Count);

        // Compose ordered list with dragged inserted
        var ordered = new List<BoxLayout>(others.Count + 1);
        ordered.AddRange(others);
        ordered.Insert(_stableInsertionIndex.Value, dragged);

        // Neighbor-aware alignment for dragged
        var leftNeighbor = _stableInsertionIndex > 0 ? ordered[_stableInsertionIndex.Value - 1] : null;
        var rightNeighbor = _stableInsertionIndex + 1 < ordered.Count ? ordered[_stableInsertionIndex.Value + 1] : null;
        AlignDraggedWithinNeighbors(dragged, leftNeighbor, rightNeighbor, monitorBounds, direction);

        // Enforce spacing constraints outward
        if (direction > 0)
        {
            EnsureRightSegment(ordered, _stableInsertionIndex.Value, monitorBounds);
        }
        else if (direction < 0)
        {
            EnsureLeftSegment(ordered, _stableInsertionIndex.Value, monitorBounds);
        }
        else
        {
            EnsureLeftSegment(ordered, _stableInsertionIndex.Value, monitorBounds);
            EnsureRightSegment(ordered, _stableInsertionIndex.Value, monitorBounds);
        }

        // Keep everything within bounds if needed
        ordered.Sort(static (a, b) => a.Left.CompareTo(b.Left));
        if (!IsWithinBounds(ordered, monitorBounds))
        {
            PackWithinBounds(ordered, dragged, monitorBounds);
        }

        // Apply reflow
        ApplyLayout(ordered);
    }

    private void CaptureDragLayoutContext()
    {
        if (ViewModel?.Model is null)
        {
            _dragLayoutContext = null;
            return;
        }

        var allWindows = AppServices.BoxWindowManager.GetAllTaskbarWindows()
            .Where(w => w.IsVisible)
            .ToList();

        if (allWindows.Count <= 1)
        {
            _dragLayoutContext = null;
            return;
        }

        var boxes = new List<BoxLayout>(allWindows.Count);
        foreach (var window in allWindows)
        {
            boxes.Add(CreateLayout(window));
        }

        boxes.Sort(static (a, b) => a.Left.CompareTo(b.Left));

        var originalIndex = boxes.FindIndex(b => b.Id == ViewModel.Model.Id);
        if (originalIndex < 0)
        {
            _dragLayoutContext = null;
            return;
        }

        _dragLayoutContext = new DragLayoutContext(originalIndex);
    }

    private static PixelRect GetMonitorBoundsForWindow(TaskbarBoxWindow window)
    {
        var screens = window.Screens;
        if (screens?.All is { } allScreens)
        {
            var position = window.Position;
            foreach (var screen in allScreens)
            {
                if (screen.Bounds.Contains(position))
                {
                    return screen.WorkingArea;
                }
            }
        }

        return screens?.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
    }

    private static int ComputeInsertionIndex(IReadOnlyList<BoxLayout> boxes, BoxLayout dragged)
    {
        if (boxes.Count == 0)
        {
            return 0;
        }

        const double epsilon = 0.1;
        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            if (dragged.Center < box.Center - epsilon)
            {
                return i;
            }
        }

        return boxes.Count;
    }

    private static void AlignDraggedWithinNeighbors(BoxLayout dragged, BoxLayout? leftNeighbor, BoxLayout? rightNeighbor, PixelRect monitorBounds, int direction)
    {
        double minLeft = monitorBounds.X;
        if (leftNeighbor != null)
        {
            minLeft = Math.Max(minLeft, leftNeighbor.Right + LayoutGap);
        }

        double maxLeft = monitorBounds.Right - dragged.Width;
        if (rightNeighbor != null)
        {
            maxLeft = Math.Min(maxLeft, rightNeighbor.Left - LayoutGap - dragged.Width);
        }

        if (minLeft <= maxLeft)
        {
            dragged.X = Math.Clamp(dragged.X, minLeft, maxLeft);
            return;
        }

        if (direction > 0)
        {
            dragged.X = minLeft;
        }
        else if (direction < 0)
        {
            dragged.X = maxLeft;
        }
        else
        {
            dragged.X = Math.Clamp(dragged.X, monitorBounds.X, monitorBounds.Right - dragged.Width);
        }
    }

    private static void EnsureRightSegment(IList<BoxLayout> boxes, int pivotIndex, PixelRect monitorBounds)
    {
        for (var i = pivotIndex + 1; i < boxes.Count; i++)
        {
            var prev = boxes[i - 1];
            var requiredLeft = prev.Right + LayoutGap;
            if (boxes[i].Left < requiredLeft)
            {
                var delta = requiredLeft - boxes[i].Left;
                ShiftRange(boxes, i, boxes.Count - 1, delta);
            }
        }

        if (boxes.Count == 0)
        {
            return;
        }

        var overflow = boxes[^1].Right - monitorBounds.Right;
        if (overflow > 0)
        {
            ShiftRange(boxes, pivotIndex + 1, boxes.Count - 1, -overflow);

            for (var i = pivotIndex + 1; i < boxes.Count; i++)
            {
                var prev = boxes[i - 1];
                var requiredLeft = prev.Right + LayoutGap;
                if (boxes[i].Left < requiredLeft)
                {
                    boxes[i].X = requiredLeft;
                }
            }
        }
    }

    private static void EnsureLeftSegment(IList<BoxLayout> boxes, int pivotIndex, PixelRect monitorBounds)
    {
        for (var i = pivotIndex - 1; i >= 0; i--)
        {
            var next = boxes[i + 1];
            var requiredRight = next.Left - LayoutGap;
            if (boxes[i].Right > requiredRight)
            {
                var delta = boxes[i].Right - requiredRight;
                ShiftRange(boxes, 0, i, -delta);
            }
        }

        if (boxes.Count == 0)
        {
            return;
        }

        var overflow = monitorBounds.X - boxes[0].Left;
        if (overflow > 0)
        {
            ShiftRange(boxes, 0, pivotIndex - 1, overflow);

            for (var i = pivotIndex - 1; i >= 0; i--)
            {
                if (i < 0)
                {
                    break;
                }

                var next = boxes[i + 1];
                var requiredRight = next.Left - LayoutGap;
                if (boxes[i].Right > requiredRight)
                {
                    boxes[i].X = requiredRight - boxes[i].Width;
                }
            }
        }
    }

    private static void ShiftRange(IList<BoxLayout> boxes, int startIndex, int endIndex, double delta)
    {
        if (delta == 0 || startIndex > endIndex)
        {
            return;
        }

        startIndex = Math.Max(startIndex, 0);
        endIndex = Math.Min(endIndex, boxes.Count - 1);

        for (var i = startIndex; i <= endIndex; i++)
        {
            boxes[i].X += delta;
        }
    }

    private static bool IsWithinBounds(IReadOnlyList<BoxLayout> boxes, PixelRect monitorBounds)
    {
        if (boxes.Count == 0)
        {
            return true;
        }

        var first = boxes[0];
        var last = boxes[^1];
        return first.Left >= monitorBounds.X - 0.5 && last.Right <= monitorBounds.Right + 0.5;
    }

    private static void PackWithinBounds(List<BoxLayout> boxes, BoxLayout dragged, PixelRect monitorBounds)
    {
        boxes.Sort(static (a, b) => a.Left.CompareTo(b.Left));
        var draggedIndex = boxes.IndexOf(dragged);
        if (draggedIndex < 0)
        {
            draggedIndex = 0;
        }

        var minLeft = monitorBounds.X;
        var maxLeft = monitorBounds.Right - dragged.Width;
        if (maxLeft < minLeft)
        {
            maxLeft = minLeft;
        }

        dragged.X = Math.Clamp(dragged.X, minLeft, maxLeft);

        for (var i = draggedIndex - 1; i >= 0; i--)
        {
            var next = boxes[i + 1];
            var targetRight = next.Left - LayoutGap;
            boxes[i].X = targetRight - boxes[i].Width;
        }

        for (var i = draggedIndex + 1; i < boxes.Count; i++)
        {
            var prev = boxes[i - 1];
            var targetLeft = prev.Right + LayoutGap;
            boxes[i].X = targetLeft;
        }

        if (boxes.Count == 0)
        {
            return;
        }

        var overflowLeft = monitorBounds.X - boxes[0].Left;
        if (overflowLeft > 0)
        {
            foreach (var box in boxes)
            {
                box.X += overflowLeft;
            }
        }

        var overflowRight = boxes[^1].Right - monitorBounds.Right;
        if (overflowRight > 0)
        {
            foreach (var box in boxes)
            {
                box.X -= overflowRight;
            }
        }
    }

    private void ApplyLayout(List<BoxLayout> boxes)
    {
        boxes.Sort(static (a, b) => a.Left.CompareTo(b.Left));

        foreach (var box in boxes)
        {
            var window = box.Window;
            var targetX = (int)Math.Round(box.X);
            var current = window.Position;
            if (current.X == targetX)
            {
                continue;
            }

            var newPos = new PixelPoint(targetX, current.Y);
            window.Position = newPos;

            if (ReferenceEquals(window, this))
            {
                _lastWindowPosition = newPos;
            }

            if (window.DataContext is TaskbarBoxWindowViewModel vm)
            {
                _ = AppServices.BoxWindowManager.SaveTaskbarWindowXAsync(vm.Model.Id, targetX);
            }
        }
    }

    private static BoxLayout CreateLayout(TaskbarBoxWindow window)
    {
        if (window.DataContext is not TaskbarBoxWindowViewModel vm)
        {
            throw new InvalidOperationException("Taskbar window is missing its view model.");
        }

        var scale = window.RenderScaling;
        var widthPx = Math.Round(window.Bounds.Width * scale);

        if (widthPx <= 0)
        {
            widthPx = Math.Round(window.Width * scale);
        }

        if (widthPx <= 0)
        {
            widthPx = 1;
        }

        return new BoxLayout(vm.Model.Id, window, window.Position.X, widthPx);
    }

    private sealed class BoxLayout
    {
        public BoxLayout(Guid id, TaskbarBoxWindow window, double x, double width)
        {
            Id = id;
            Window = window;
            X = x;
            Width = width;
        }

        public Guid Id { get; }

        public TaskbarBoxWindow Window { get; }

        public double X { get; set; }

        public double Width { get; }

        public double Left => X;

        public double Right => X + Width;

        public double Center => X + Width / 2d;
    }

    private readonly record struct DragLayoutContext(int OriginalIndex);

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


