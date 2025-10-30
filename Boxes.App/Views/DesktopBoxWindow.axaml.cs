using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Boxes.App.ViewModels;
using Boxes.App.Services;
using Boxes.App.Models;
using Avalonia.Threading;

namespace Boxes.App.Views;

public partial class DesktopBoxWindow : Window
{
    private PixelPoint _lastKnownPosition;
    private ItemsControl? _shortcutsItemsControl;
        private bool _headerDragging;
        private PixelPoint _dragStartWindow;
        private Point _dragStartPointer;
        private bool _suppressPositionSync;
        private bool _isDraggingWindow;
        private Border? _contentArea;
        private Grid? _rootGrid;
        private Border? _headerBar;
        private RowDefinition? _contentRow;
        private bool _isContentExpanded = true;
        private double _savedContentHeight = 240;
        private double _savedWindowHeight = 240;
        private DispatcherTimer? _expandAnimationTimer;
        private Border? _snapIndicator;

    public DesktopBoxWindow()
    {
        InitializeComponent();
        Opened += DesktopBoxWindow_Opened;
        PositionChanged += DesktopBoxWindow_PositionChanged;
        _shortcutsItemsControl = this.FindControl<ItemsControl>("ShortcutsItemsControl");
        _contentArea = this.FindControl<Border>("ContentArea");
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _headerBar = this.FindControl<Border>("HeaderBar");
        _snapIndicator = this.FindControl<Border>("SnapIndicator");
        
        // Apply desktop icon metrics
        ApplyDesktopIconMetrics();
        
        if (_rootGrid?.RowDefinitions.Count > 1)
        {
            _contentRow = _rootGrid.RowDefinitions[1];
            // Initialize content row to use fixed height if it's currently "*"
            if (_contentRow.Height.IsStar)
            {
                // Measure content area after layout is complete
                _rootGrid.LayoutUpdated += (s, e) =>
                {
                    if (_contentArea != null && _savedContentHeight <= 0)
                    {
                        _contentArea.Measure(new Size(_contentArea.Bounds.Width, double.PositiveInfinity));
                        var desiredHeight = _contentArea.DesiredSize.Height;
                        if (desiredHeight > 0)
                        {
                            _savedContentHeight = desiredHeight;
                            _contentRow.Height = new GridLength(_savedContentHeight);
                        }
                    }
                };
            }
            else
            {
                _savedContentHeight = _contentRow.Height.Value;
            }
        }
        // Subscribe to settings changes to reflect transparency
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
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
        ApplyTransparencyFromSettings();
        if (DataContext is DesktopBoxWindowViewModel vm)
        {
            await vm.RefreshIconsAsync().ConfigureAwait(false);
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

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        
        if (point.Properties.IsLeftButtonPressed)
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

            // Handle left-click for expand/collapse animation (but allow dragging)
            // We'll check in PointerReleased if it was a click vs drag
            _headerDragging = true; // Mark as potential drag
            _dragStartPointer = e.GetPosition(this);
            _dragStartWindow = Position;
            e.Pointer.Capture((IInputElement)sender!);
            e.Handled = true;
        }
    }

    private void Header_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_headerDragging)
        {
            return;
        }

        // For taskbar windows, handle horizontal dragging
        if (ViewModel.IsSnappedToTaskbar)
        {
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
            return;
        }

        // For non-taskbar windows, check if movement exceeds threshold
        var currentPos = e.GetPosition(this);
        var dragDistance = Math.Abs(currentPos.X - _dragStartPointer.X) + Math.Abs(currentPos.Y - _dragStartPointer.Y);
        
        if (dragDistance >= 5 && !_isDraggingWindow)
        {
            // Significant movement detected - start manual window dragging
            _isDraggingWindow = true;
        }
        
        if (_isDraggingWindow)
        {
            // Manually handle window dragging - no visual snapping during drag
            var deltaX = currentPos.X - _dragStartPointer.X;
            var deltaY = currentPos.Y - _dragStartPointer.Y;
            var newX = _dragStartWindow.X + (int)deltaX;
            var newY = _dragStartWindow.Y + (int)deltaY;
            
            // Check for snap target in background to show indicator
            var (snapTargetY, _) = AppServices.BoxWindowManager.GetSnapTargetY(this, newY);
            if (snapTargetY.HasValue)
            {
                // Show snap indicator when within range
                if (_snapIndicator != null)
                {
                    _snapIndicator.IsVisible = true;
                }
            }
            else
            {
                // Hide snap indicator when not in range
                if (_snapIndicator != null)
                {
                    _snapIndicator.IsVisible = false;
                }
            }
            
            // Update position normally - snap will happen on release if within range
            Position = new PixelPoint(newX, newY);
            e.Handled = true;
        }
    }

    private void Header_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel.IsSnappedToTaskbar)
        {
            if (!_headerDragging)
            {
                // Toggle expand/collapse when snapped and not dragged
                if (e.InitialPressMouseButton == MouseButton.Left)
                {
                    var expanded = ViewModel.IsCollapsed;
                    _ = Boxes.App.Services.AppServices.BoxWindowManager.SetSnappedExpandedAsync(ViewModel.Model.Id, expanded);
                    e.Handled = true;
                }
            }
            else
            {
                _headerDragging = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
            return;
        }

        // For non-taskbar windows: if we didn't drag, trigger expand/collapse animation
        if (_headerDragging && e.InitialPressMouseButton == MouseButton.Left)
        {
            var currentPos = e.GetPosition(this);
            var dragDistance = Math.Abs(currentPos.X - _dragStartPointer.X) + Math.Abs(currentPos.Y - _dragStartPointer.Y);
            
            // Only trigger animation if it was a click (little to no movement)
            if (!_isDraggingWindow && dragDistance < 5)
            {
                ToggleContentExpansion();
                e.Handled = true;
                e.Pointer.Capture(null);
                _headerDragging = false;
                _isDraggingWindow = false;
                return;
            }
            
            // Reset dragging state and check for snap on release
            // Check if we actually dragged before resetting flags
            var actuallyDragged = _isDraggingWindow || dragDistance >= 5;
            
            // Hide snap indicator on release
            if (_snapIndicator != null)
            {
                _snapIndicator.IsVisible = false;
            }
            
            _headerDragging = false;
            _isDraggingWindow = false;
            
            // Check if we're within snap range on release - snap happens invisibly in background
            if (actuallyDragged)
            {
                var currentY = Position.Y;
                var (snapTargetY, _) = AppServices.BoxWindowManager.GetSnapTargetY(this, currentY);
                if (snapTargetY.HasValue)
                {
                    // Snap to target position on release
                    Position = new PixelPoint(Position.X, snapTargetY.Value);
                }
            }
        }

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Stop event propagation so button clicks don't trigger window dragging
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

    private void ToggleContentExpansion()
    {
        if (_contentRow == null || _rootGrid == null)
        {
            return;
        }

        // Cancel any ongoing animation
        _expandAnimationTimer?.Stop();
        _expandAnimationTimer = null;

        var targetExpanded = !_isContentExpanded;
        AnimateContentExpansion(targetExpanded);
    }

    private void AnimateContentExpansion(bool expand)
    {
        if (_contentRow == null || _rootGrid == null)
        {
            return;
        }

        // Stop any ongoing animation
        _expandAnimationTimer?.Stop();

        // Get header height
        var headerHeight = _headerBar?.Bounds.Height ?? 40;
        if (headerHeight <= 0)
        {
            headerHeight = 40; // Fallback to MinHeight
        }

        // Get current height (handle both pixel and star sizing)
        double startHeight;
        double startWindowHeight = Height;
        
        if (_contentRow.Height.IsStar)
        {
            // If using star sizing, measure actual content height
            if (_contentArea != null)
            {
                _contentArea.Measure(new Size(_contentArea.Bounds.Width, double.PositiveInfinity));
                startHeight = Math.Max(_contentArea.DesiredSize.Height, _contentArea.Bounds.Height);
            }
            else
            {
                startHeight = Height - headerHeight;
            }
            // Convert to fixed pixel height for animation
            _contentRow.Height = new GridLength(startHeight);
        }
        else
        {
            startHeight = _contentRow.Height.Value;
        }

        // If we're collapsing, save the current height for later expansion
        if (!expand && startHeight > 0)
        {
            _savedContentHeight = startHeight;
            _savedWindowHeight = Height;
        }

        var endHeight = expand ? (_savedContentHeight > 0 ? _savedContentHeight : startHeight) : 0.0;
        var endWindowHeight = expand ? (_savedWindowHeight > 0 ? _savedWindowHeight : headerHeight + endHeight) : headerHeight;

        // If expanding and we don't have a saved height, measure the content
        if (expand && _savedContentHeight <= 0 && _contentArea != null)
        {
            _contentArea.Measure(new Size(_contentArea.Bounds.Width, double.PositiveInfinity));
            var measuredHeight = _contentArea.DesiredSize.Height;
            if (measuredHeight > 0)
            {
                _savedContentHeight = measuredHeight;
            }
            else
            {
                _savedContentHeight = Math.Max(startHeight, 200); // Fallback
            }
            endHeight = _savedContentHeight;
            // Recalculate window height if we didn't have a saved one
            if (_savedWindowHeight <= headerHeight)
            {
                endWindowHeight = headerHeight + endHeight;
            }
        }
        
        // Ensure endWindowHeight is calculated correctly
        if (expand && _savedWindowHeight <= headerHeight)
        {
            endWindowHeight = headerHeight + endHeight;
        }

        var duration = TimeSpan.FromSeconds(1.0);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Set initial visibility state
        if (_contentArea != null)
        {
            _contentArea.IsVisible = expand || startHeight > 0.1;
            if (!expand && startHeight <= 0.1)
            {
                _contentArea.Height = 0;
            }
        }

        _expandAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) // 60 FPS
        };

        EventHandler? tick = null;
        tick = (_, __) =>
        {
            var progress = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            // Use ease-out cubic for smooth animation
            var eased = 1 - Math.Pow(1 - progress, 3);
            
            var currentHeight = startHeight + (endHeight - startHeight) * eased;
            var clampedHeight = Math.Max(0, currentHeight);
            _contentRow.Height = new GridLength(clampedHeight);
            
            // Animate window height along with content row height
            var currentWindowHeight = startWindowHeight + (endWindowHeight - startWindowHeight) * eased;
            Height = Math.Max(headerHeight, currentWindowHeight);
            
            // Hide content area when collapsed to ensure nothing shows (padding, borders, background, etc.)
            if (_contentArea != null)
            {
                if (clampedHeight <= 0.1)
                {
                    _contentArea.IsVisible = false;
                    _contentArea.Height = 0;
                }
                else
                {
                    _contentArea.IsVisible = true;
                    _contentArea.Height = double.NaN; // Reset to auto sizing
                }
            }

            if (progress >= 1.0)
            {
                _expandAnimationTimer.Tick -= tick!;
                _expandAnimationTimer.Stop();
                _expandAnimationTimer = null;
                
                // Ensure final state is exact
                _contentRow.Height = new GridLength(endHeight);
                Height = endWindowHeight;
                
                if (_contentArea != null)
                {
                    if (endHeight <= 0)
                    {
                        _contentArea.IsVisible = false;
                        _contentArea.Height = 0;
                    }
                    else
                    {
                        _contentArea.IsVisible = true;
                        _contentArea.Height = double.NaN; // Reset to auto sizing
                    }
                }
                _isContentExpanded = expand;
                
                sw.Stop();
            }
        };

        _expandAnimationTimer.Tick += tick;
        _isContentExpanded = !expand; // Set immediately to prevent double-toggles
        _expandAnimationTimer.Start();
    }

    private void ApplyDesktopIconMetrics()
    {
        if (_shortcutsItemsControl == null)
            return;

        var metrics = DesktopIconMetricsService.GetDesktopIconMetrics();
        
        // Apply metrics immediately and also on Loaded event to ensure it takes effect
        void ApplyMetrics()
        {
            // Find the WrapPanel in the visual tree
            var wrapPanel = _shortcutsItemsControl.GetVisualDescendants()
                .OfType<WrapPanel>()
                .FirstOrDefault();
            
            if (wrapPanel != null)
            {
                wrapPanel.ItemWidth = metrics.HorizontalSpacing;
                wrapPanel.ItemHeight = metrics.VerticalSpacing;
            }
        }
        
        // Try immediately if control is already loaded
        if (_shortcutsItemsControl.IsLoaded)
        {
            ApplyMetrics();
        }
        
        // Also subscribe to Loaded event for when control loads later
        _shortcutsItemsControl.Loaded += (s, e) =>
        {
            ApplyMetrics();
        };
        
        // Subscribe to LayoutUpdated as a fallback to catch any timing issues
        _shortcutsItemsControl.LayoutUpdated += (s, e) =>
        {
            var wrapPanel = _shortcutsItemsControl.GetVisualDescendants()
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

    protected override void OnClosed(EventArgs e)
    {
        _expandAnimationTimer?.Stop();
        _expandAnimationTimer = null;
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
