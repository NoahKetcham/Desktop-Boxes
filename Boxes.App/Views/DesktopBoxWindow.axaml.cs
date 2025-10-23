using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class DesktopBoxWindow : Window
{
    private PixelPoint _lastKnownPosition;
    private ItemsControl? _shortcutsItemsControl;
        private bool _headerDragging;
        private PixelPoint _dragStartWindow;
        private Point _dragStartPointer;
        private bool _suppressPositionSync;

    public DesktopBoxWindow()
    {
        InitializeComponent();
        Opened += DesktopBoxWindow_Opened;
        PositionChanged += DesktopBoxWindow_PositionChanged;
        _shortcutsItemsControl = this.FindControl<ItemsControl>("ShortcutsItemsControl");
        if (DataContext is DesktopBoxWindowViewModel vm)
        {
            vm.RegisterView(this);
        }
    }

    internal DesktopBoxWindowViewModel ViewModel => (DesktopBoxWindowViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DesktopBoxWindowViewModel vm)
        {
            vm.RegisterView(this);
        }
    }

    public void InvalidateShortcutsLayout()
    {
        _shortcutsItemsControl?.InvalidateMeasure();
        _shortcutsItemsControl?.InvalidateArrange();
        _shortcutsItemsControl?.InvalidateVisual();
    }

    public PixelPoint LastKnownPosition => _lastKnownPosition;

    private void DesktopBoxWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (ViewModel.IsSnappedToTaskbar && !_suppressPositionSync)
        {
            AnchorToTaskbar(e.Point.X);
            return;
        }

        _lastKnownPosition = e.Point;
    }

    private async void DesktopBoxWindow_Opened(object? sender, EventArgs e)
    {
        _lastKnownPosition = Position;
        if (DataContext is DesktopBoxWindowViewModel vm)
        {
            await vm.RefreshIconsAsync().ConfigureAwait(false);
        }
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (ViewModel.IsSnappedToTaskbar)
            {
                _headerDragging = true;
                _dragStartWindow = Position;
                _dragStartPointer = e.GetPosition(this);
                e.Pointer.Capture((IInputElement)sender!);
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
        }
    }

    private void Header_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_headerDragging)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _dragStartPointer.X;

        var newX = _dragStartWindow.X + (int)deltaX;

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

    private void Header_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_headerDragging)
        {
            // Toggle expand/collapse when snapped and not dragged
            if (ViewModel.IsSnappedToTaskbar && e.InitialPressMouseButton == MouseButton.Left)
            {
                var expanded = ViewModel.IsCollapsed;
                _ = Boxes.App.Services.AppServices.BoxWindowManager.SetSnappedExpandedAsync(ViewModel.Model.Id, expanded);
                e.Handled = true;
            }
            return;
        }

        _headerDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void AnchorToTaskbar(int? desiredX = null)
    {
        try
        {
            _suppressPositionSync = true;
            var working = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
            var x = desiredX ?? Position.X;
            x = Math.Clamp(x, working.X, working.Right - (int)Bounds.Width);
            var y = working.Bottom - (int)Bounds.Height;
            Position = new PixelPoint(x, y);
            _lastKnownPosition = Position;
        }
        finally
        {
            _suppressPositionSync = false;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (ViewModel.IsSnappedToTaskbar)
        {
            AnchorToTaskbar(Position.X);
        }
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
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
    }

    private void ShortcutTile_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DesktopFileViewModel file)
        {
            if (file.IsFolder)
            {
                ViewModel.EnterFolderCommand.Execute(file);
            }
            else
            {
                ViewModel.LaunchShortcutCommand.Execute(file);
            }
        }
    }

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
}

