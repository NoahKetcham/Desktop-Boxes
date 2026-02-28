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
using Boxes.App.Extensions;
using Boxes.App.Models;
using Avalonia.Threading;

namespace Boxes.App.Views;

public partial class DesktopBoxWindow : Window
{
        private PixelPoint _lastKnownPosition;
    private readonly ItemsControl? _shortcutsItemsControl;
        private bool _headerDragging;
        private PixelPoint _dragStartWindow;
        private PixelPoint _dragStartScreenPosition;
        private PixelPoint _lastWindowPosition;
        private bool _suppressPositionSync;
        private bool _isDraggingWindow;
        private int _snapCheckCounter;
        private readonly Border? _contentArea;
        private readonly Grid? _rootGrid;
        private readonly Border? _headerBar;
        private readonly RowDefinition? _contentRow;
        private bool _isContentExpanded = true;
        private double _savedContentHeight = 240;
        private double _savedWindowHeight = 240;
        private DispatcherTimer? _expandAnimationTimer;
        private bool _isAnimating;
        private readonly Border? _snapIndicator;
        private readonly StackPanel? _titlePanel;
        private const string DragDataFormat = "application/x-boxes-shortcut-id";
        private Point _dragStartPoint;
        private DesktopFileViewModel? _draggedItem;
        private bool _isDraggingItem;
        private Control? _currentDropTarget;

    public DesktopBoxWindow()
    {
        InitializeComponent();
        Opened += DesktopBoxWindow_Opened;
        PositionChanged += DesktopBoxWindow_PositionChanged;
        Activated += (_, __) => { if (!AppServices.BoxWindowManager.IsBurstActive) this.SetAlwaysBelowApps(); };
        PointerEntered += (_, __) => AppServices.BoxWindowManager.NotifyHover();
        AddHandler(InputElement.PointerMovedEvent, OnPointerHoverActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(InputElement.PointerEnteredEvent, OnPointerHoverActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelActivity, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        AddHandler(InputElement.PointerMovedEvent, (_, __) => AppServices.BoxWindowManager.NotifyHover(), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        _shortcutsItemsControl = this.FindControl<ItemsControl>("ShortcutsItemsControl");
        _contentArea = this.FindControl<Border>("ContentArea");
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _headerBar = this.FindControl<Border>("HeaderBar");
        _snapIndicator = this.FindControl<Border>("SnapIndicator");
        _titlePanel = this.FindControl<StackPanel>("TitlePanel");
        
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
        this.SetAlwaysBelowApps();
        ApplyTransparencyFromSettings();
        if (DataContext is DesktopBoxWindowViewModel vm)
        {
            await vm.RefreshIconsAsync().ConfigureAwait(false);
        }
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings e)
    {
        ApplyAllSettings(e);
    }

    private void ApplyTransparencyFromSettings()
    {
        var _ = AppServices.SettingsService.GetAsync().ContinueWith(t =>
        {
            if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result is { } s)
            {
                ApplyAllSettings(s);
            }
        });
    }

    private void ApplyAllSettings(ApplicationSettings settings)
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

            // Apply content padding (horizontal and vertical independently)
            var contentPadding = Math.Clamp(settings.BoxContentPadding, 0, 64);
            var verticalPadding = Math.Clamp(settings.BoxContentVerticalPadding, 0, 64);
            if (_contentArea != null)
            {
                var currentPadding = _contentArea.Padding;
                var vertical = verticalPadding > 0 ? verticalPadding : (currentPadding.Top > 0 ? currentPadding.Top : 12);
                _contentArea.Padding = new Thickness(contentPadding, vertical, contentPadding, vertical);
            }

            // Apply background and transparency
            if (_rootGrid != null)
            {
                _rootGrid.Background = new SolidColorBrush(bgColor, opacity);
                _rootGrid.ClipToBounds = true;
            }

            // Apply corner radius
            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 20);
            if (_contentArea != null)
            {
                _contentArea.CornerRadius = new CornerRadius(0, 0, cornerRadius, cornerRadius);
            }

            // Apply header settings
            if (_headerBar != null)
            {
                // Header bar is always visible and interactive for collapse animation, right-click menu, etc.
                // ShowBoxHeader controls whether the header background is visible
                // ShowBoxTitle controls whether the title text is visible (independent of header)
                _headerBar.IsHitTestVisible = true;
                
                // Apply header height
                var headerHeight = Math.Clamp(settings.BoxHeaderHeight, 30, 60);
                _headerBar.MinHeight = headerHeight;
                _headerBar.Padding = new Thickness(10, (headerHeight - 26) / 2);
                
                // Apply header corner radius (only top corners when header is visible)
                _headerBar.CornerRadius = settings.ShowBoxHeader 
                    ? new CornerRadius(cornerRadius, cornerRadius, 0, 0)
                    : new CornerRadius(0);
                
                // If header is hidden, content area gets full corner radius
                if (_contentArea != null && !settings.ShowBoxHeader)
                {
                    _contentArea.CornerRadius = new CornerRadius(cornerRadius);
                }

                // Calculate relative luminance to determine if background is dark or light
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
                
                // When ShowBoxHeader is off, make background transparent but keep header functional
                _headerBar.Background = settings.ShowBoxHeader 
                    ? new SolidColorBrush(headerColor, opacity)
                    : Brushes.Transparent;
            }

            // Apply title visibility (independent of header bar visibility)
            if (_titlePanel != null)
            {
                _titlePanel.Opacity = settings.ShowBoxTitle ? 1.0 : 0.0;
            }

            // Apply icon size and label visibility to shortcut items
            ApplyIconSettings(settings);
        });
    }

    private void ApplyIconSettings(ApplicationSettings settings)
    {
        if (_shortcutsItemsControl == null) return;

        UpdateIconResources(settings.BoxIconSize, settings.ShowShortcutLabels);
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        
        if (point.Properties.IsLeftButtonPressed)
        {
            // Capture pointer and store initial positions
            // Convert window-relative pointer position to screen coordinates
            var pointerRelative = e.GetPosition(this);
            var screenX = Position.X + (int)Math.Round(pointerRelative.X);
            var screenY = Position.Y + (int)Math.Round(pointerRelative.Y);
            
            if (ViewModel.IsSnappedToTaskbar)
            {
                _headerDragging = true;
                _dragStartWindow = Position;
                _lastWindowPosition = Position;
                _dragStartScreenPosition = new PixelPoint(screenX, screenY);
                _snapCheckCounter = 0;
                e.Pointer.Capture((IInputElement)sender!);
                e.Handled = true;
                return;
            }

            // Handle left-click for expand/collapse animation (but allow dragging)
            // We'll check in PointerReleased if it was a click vs drag
            _headerDragging = true; // Mark as potential drag
            _dragStartWindow = Position;
            _lastWindowPosition = Position;
            _dragStartScreenPosition = new PixelPoint(screenX, screenY);
            _snapCheckCounter = 0;
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

        // Get current pointer position in screen coordinates
        // Use _lastWindowPosition instead of Position to avoid stale values
        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);
        
        // Calculate delta from initial screen position - this prevents drift
        var deltaX = currentScreenPos.X - _dragStartScreenPosition.X;
        var deltaY = currentScreenPos.Y - _dragStartScreenPosition.Y;

        // For taskbar windows, handle horizontal dragging only
        if (ViewModel.IsSnappedToTaskbar)
        {
            var newX = _dragStartWindow.X + deltaX;

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
            var newPos = new PixelPoint(clampedX, y);
            _lastWindowPosition = newPos;
            Position = newPos;
            e.Handled = true;
            return;
        }

        // For non-taskbar windows, check if movement exceeds threshold
        var dragDistance = Math.Abs(deltaX) + Math.Abs(deltaY);
        
        if (dragDistance >= 5 && !_isDraggingWindow)
        {
            // Significant movement detected - start manual window dragging
            _isDraggingWindow = true;
        }
        
        if (_isDraggingWindow)
        {
            // Calculate new position based on initial window position + screen-space delta
            var newX = _dragStartWindow.X + deltaX;
            var newY = _dragStartWindow.Y + deltaY;
            
            // Throttle snap checking - use a counter to check every few moves instead of modulo
            // This is more reliable than modulo which might skip checks
            if (_snapCheckCounter++ % 3 == 0)
            {
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
            }
            
            // Update position immediately - snap will happen on release if within range
            var newPos = new PixelPoint(newX, newY);
            _lastWindowPosition = newPos;
            Position = newPos;
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
            var currentPointerRelative = e.GetPosition(this);
            var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
            var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
            var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);
            var dragDistance = Math.Abs(currentScreenPos.X - _dragStartScreenPosition.X) + Math.Abs(currentScreenPos.Y - _dragStartScreenPosition.Y);
            
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
            _snapCheckCounter = 0;
            
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

    private void ShortcutTile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && sender is Control control && control.DataContext is DesktopFileViewModel vm)
        {
            _dragStartPoint = e.GetPosition(this);
            _draggedItem = vm;
            _isDraggingItem = false;
        }
    }

    private async void ShortcutTile_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedItem is null || _isDraggingItem == true)
        {
            return;
        }

        var point = e.GetPosition(this);
        var delta = point - _dragStartPoint;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && (Math.Abs(delta.X) + Math.Abs(delta.Y)) > 6)
        {
            _isDraggingItem = true;
            var data = new DataObject();
            data.Set(DragDataFormat, _draggedItem.Id.ToString());
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            ClearDragVisuals();
            _draggedItem = null;
            _isDraggingItem = false;
        }
    }

    private void ShortcutTile_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggedItem = null;
        _isDraggingItem = false;
    }

    private void Shortcuts_OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DragDataFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        var targetControl = e.Source as Control;
        SetDropTargetHighlight(targetControl);
    }

    private async void Shortcuts_OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DragDataFormat))
        {
            return;
        }

        var draggedText = e.Data.Get(DragDataFormat) as string;
        if (!Guid.TryParse(draggedText, out var draggedId))
        {
            ClearDragVisuals();
            return;
        }

        var targetVm = (e.Source as Control)?.DataContext as DesktopFileViewModel;
        var dropBeforeId = targetVm?.Id == draggedId ? (Guid?)null : targetVm?.Id;

        await ViewModel.ReorderCurrentItemsAsync(draggedId, dropBeforeId);

        ClearDragVisuals();
        e.Handled = true;
    }

    private void Shortcuts_OnDragLeave(object? sender, DragEventArgs e)
    {
        ClearDragVisuals();
    }

    private void SetDropTargetHighlight(Control? control)
    {
        if (_currentDropTarget is Control previous)
        {
            previous.Classes.Set("drop-target", false);
        }

        var border = control?.FindAncestorOfType<Border>() ?? control as Border;
        if (border != null && border.Classes.Contains("shortcut-item"))
        {
            border.Classes.Set("drop-target", true);
            _currentDropTarget = border;
            return;
        }

        _currentDropTarget = null;
    }

    private void ClearDragVisuals()
    {
        if (_currentDropTarget is Control border)
        {
            border.Classes.Set("drop-target", false);
        }
        _currentDropTarget = null;
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

        // Debounce: ignore clicks while animation is in progress
        if (_isAnimating)
        {
            return;
        }

        var targetExpanded = !_isContentExpanded;
        AnimateContentExpansion(targetExpanded);
    }

    private void AnimateContentExpansion(bool expand)
    {
        if (_contentRow == null || _rootGrid == null)
        {
            return;
        }

        // Mark animation as in progress for debouncing
        _isAnimating = true;

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

        void Tick(object? s, EventArgs e)
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
                _expandAnimationTimer!.Tick -= Tick;
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
                _isAnimating = false;
                
                sw.Stop();
            }
        }

        _expandAnimationTimer.Tick += Tick;
        _isContentExpanded = !expand; // Set immediately to prevent double-toggles
        _expandAnimationTimer.Start();
    }

    private void ApplyDesktopIconMetrics()
    {
        if (_shortcutsItemsControl == null)
            return;

        var metrics = DesktopIconMetricsService.GetDesktopIconMetrics();
        UpdateIconResources(metrics.IconSize, showLabels: true, itemWidthOverride: metrics.HorizontalSpacing, itemHeightOverride: metrics.VerticalSpacing);
        
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
    }

    private void UpdateIconResources(int iconSize, bool showLabels, int? itemWidthOverride = null, int? itemHeightOverride = null)
    {
        Resources ??= new ResourceDictionary();

        var clampedSize = Math.Clamp(iconSize, 32, 64);
        var itemWidth = itemWidthOverride ?? clampedSize + 30;
        var itemHeight = itemHeightOverride ?? (showLabels ? clampedSize + 49 : clampedSize + 16);
        var labelWidth = clampedSize + 20;

        Resources["IconSize"] = (double)clampedSize;
        Resources["IconItemWidth"] = (double)itemWidth;
        Resources["IconItemHeight"] = (double)itemHeight;
        Resources["IconLabelMaxWidth"] = (double)labelWidth;
        Resources["ShowShortcutLabels"] = showLabels;

        if (_shortcutsItemsControl?.GetVisualDescendants().OfType<WrapPanel>().FirstOrDefault() is { } wrapPanel)
        {
            wrapPanel.ItemWidth = itemWidth;
            wrapPanel.ItemHeight = itemHeight;
        }

        _shortcutsItemsControl?.InvalidateMeasure();
        _shortcutsItemsControl?.InvalidateArrange();
        _shortcutsItemsControl?.InvalidateVisual();
    }

    protected override void OnClosed(EventArgs e)
    {
        _expandAnimationTimer?.Stop();
        _expandAnimationTimer = null;
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
