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

public partial class NotepadPreviewWindow : Window
{
    private bool _headerDragging;
    private PixelPoint _dragStartWindow;
    private PixelPoint _dragStartScreenPosition;
    private PixelPoint _lastWindowPosition;
    private bool _isDraggingWindow;
    private int _snapCheckCounter;
    private Border? _contentArea;
    private Grid? _rootGrid;
    private Border? _headerBar;
    private RowDefinition? _contentRow;
    private bool _isContentExpanded = true;
    private double _savedContentHeight = 240;
    private double _savedWindowHeight = 240;
    private DispatcherTimer? _expandAnimationTimer;
    private Border? _snapIndicator;
    private (bool IsCollapsed, double ExpandedHeight)? _restoreState;

    public NotepadPreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        _contentArea = this.FindControl<Border>("ContentArea");
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _headerBar = this.FindControl<Border>("HeaderBar");
        _snapIndicator = this.FindControl<Border>("SnapIndicator");

        if (_rootGrid?.RowDefinitions.Count > 1)
        {
            _contentRow = _rootGrid.RowDefinitions[1];
            if (_contentRow.Height.IsStar)
            {
                _rootGrid.LayoutUpdated += (s, e) =>
                {
                    if (_contentArea != null && _savedContentHeight <= 0)
                    {
                        _contentArea.Measure(new Avalonia.Size(_contentArea.Bounds.Width, double.PositiveInfinity));
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

        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private NotepadWindowViewModel ViewModel => (NotepadWindowViewModel)DataContext!;

    private void OnOpened(object? sender, EventArgs e)
    {
        this.SetAlwaysBelowApps();
        ApplyTransparencyFromSettings();
        if (_restoreState is { } rs)
        {
            RestoreCollapseState(rs.IsCollapsed, rs.ExpandedHeight);
            _restoreState = null;
        }
    }

    internal void SetRestoreState(bool isCollapsed, double expandedHeight)
    {
        _restoreState = (isCollapsed, expandedHeight);
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings e)
    {
        ApplyAllSettings(e);
    }

    private void ApplyTransparencyFromSettings()
    {
        _ = AppServices.SettingsService.GetAsync().ContinueWith(t =>
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
            if (!Color.TryParse(backgroundColorHex, out var parsedColor))
            {
                parsedColor = Color.Parse("#1C2235");
            }
            var bgColor = parsedColor;

            var contentPadding = Math.Clamp(settings.BoxContentPadding, 0, 64);
            var verticalPadding = Math.Clamp(settings.BoxContentVerticalPadding, 0, 64);
            if (_contentArea != null)
            {
                var vertical = verticalPadding > 0 ? verticalPadding : 12;
                _contentArea.Padding = new Thickness(contentPadding, vertical, contentPadding, vertical);
            }

            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 20);
            if (_rootGrid != null)
            {
                _rootGrid.Background = new SolidColorBrush(bgColor, opacity);
                _rootGrid.ClipToBounds = true;
            }

            if (_contentArea != null)
            {
                _contentArea.CornerRadius = new CornerRadius(0, 0, cornerRadius, cornerRadius);
            }

            if (_headerBar != null)
            {
                _headerBar.IsVisible = settings.ShowBoxHeader;
                var headerHeight = Math.Clamp(settings.BoxHeaderHeight, 30, 60);
                _headerBar.MinHeight = headerHeight;
                _headerBar.Padding = new Thickness(10, (headerHeight - 26) / 2);
                _headerBar.CornerRadius = settings.ShowBoxHeader
                    ? new CornerRadius(cornerRadius, cornerRadius, 0, 0)
                    : new CornerRadius(0);

                if (_contentArea != null && !settings.ShowBoxHeader)
                {
                    _contentArea.CornerRadius = new CornerRadius(cornerRadius);
                }

                var r = bgColor.R / 255.0;
                var g = bgColor.G / 255.0;
                var b = bgColor.B / 255.0;
                var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

                Color headerColor;
                if (luminance < 0.5)
                {
                    headerColor = Color.FromRgb(
                        (byte)Math.Min(255, bgColor.R + (255 - bgColor.R) * 0.1),
                        (byte)Math.Min(255, bgColor.G + (255 - bgColor.G) * 0.1),
                        (byte)Math.Min(255, bgColor.B + (255 - bgColor.B) * 0.1)
                    );
                }
                else
                {
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

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var pointerRelative = e.GetPosition(this);
        var screenX = Position.X + (int)Math.Round(pointerRelative.X);
        var screenY = Position.Y + (int)Math.Round(pointerRelative.Y);

        _headerDragging = true;
        _dragStartWindow = Position;
        _lastWindowPosition = Position;
        _dragStartScreenPosition = new PixelPoint(screenX, screenY);
        _snapCheckCounter = 0;
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void Header_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_headerDragging)
            return;

        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);
        var deltaX = currentScreenPos.X - _dragStartScreenPosition.X;
        var deltaY = currentScreenPos.Y - _dragStartScreenPosition.Y;
        var dragDistance = Math.Abs(deltaX) + Math.Abs(deltaY);

        if (dragDistance >= 5 && !_isDraggingWindow)
        {
            _isDraggingWindow = true;
        }

        if (_isDraggingWindow)
        {
            if (_snapCheckCounter++ % 3 == 0)
            {
                var (snapTargetY, _) = AppServices.BoxWindowManager.GetSnapTargetY(this, currentScreenY);
                if (_snapIndicator != null)
                {
                    _snapIndicator.IsVisible = snapTargetY.HasValue;
                }
            }

            var newX = _dragStartWindow.X + deltaX;
            var newY = _dragStartWindow.Y + deltaY;
            var newPos = new PixelPoint(newX, newY);
            _lastWindowPosition = newPos;
            Position = newPos;
            e.Handled = true;
        }
    }

    private void Header_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_headerDragging)
            return;

        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = _lastWindowPosition.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = _lastWindowPosition.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);
        var dragDistance = Math.Abs(currentScreenPos.X - _dragStartScreenPosition.X) + Math.Abs(currentScreenPos.Y - _dragStartScreenPosition.Y);

        if (!_isDraggingWindow && dragDistance < 5)
        {
            ToggleContentExpansion();
        }
        else if (_isDraggingWindow)
        {
            if (_snapIndicator != null)
            {
                _snapIndicator.IsVisible = false;
            }

            var (snapTargetY, _) = AppServices.BoxWindowManager.GetSnapTargetY(this, Position.Y);
            if (snapTargetY.HasValue)
            {
                Position = new PixelPoint(Position.X, snapTargetY.Value);
            }
        }

        _headerDragging = false;
        _isDraggingWindow = false;
        _snapCheckCounter = 0;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ToggleContentExpansion()
    {
        if (_contentRow == null || _rootGrid == null)
            return;

        _expandAnimationTimer?.Stop();
        _expandAnimationTimer = null;

        var targetExpanded = !_isContentExpanded;
        AnimateContentExpansion(targetExpanded);
    }

    private void AnimateContentExpansion(bool expand)
    {
        if (_contentRow == null || _rootGrid == null)
            return;

        _expandAnimationTimer?.Stop();

        var headerHeight = _headerBar?.Bounds.Height ?? 40;
        if (headerHeight <= 0)
            headerHeight = 40;

        double startHeight;
        var startWindowHeight = Height;

        if (_contentRow.Height.IsStar)
        {
            if (_contentArea != null)
            {
                _contentArea.Measure(new Avalonia.Size(_contentArea.Bounds.Width, double.PositiveInfinity));
                startHeight = Math.Max(_contentArea.DesiredSize.Height, _contentArea.Bounds.Height);
            }
            else
            {
                startHeight = Height - headerHeight;
            }
            _contentRow.Height = new GridLength(startHeight);
        }
        else
        {
            startHeight = _contentRow.Height.Value;
        }

        if (!expand && startHeight > 0)
        {
            _savedContentHeight = startHeight;
            _savedWindowHeight = Height;
        }

        var endHeight = expand ? (_savedContentHeight > 0 ? _savedContentHeight : startHeight) : 0.0;
        var endWindowHeight = expand ? (_savedWindowHeight > 0 ? _savedWindowHeight : headerHeight + endHeight) : headerHeight;

        if (expand && _savedContentHeight <= 0 && _contentArea != null)
        {
            _contentArea.Measure(new Avalonia.Size(_contentArea.Bounds.Width, double.PositiveInfinity));
            var measuredHeight = _contentArea.DesiredSize.Height;
            _savedContentHeight = measuredHeight > 0 ? measuredHeight : Math.Max(startHeight, 200);
            endHeight = _savedContentHeight;
            if (_savedWindowHeight <= headerHeight)
            {
                endWindowHeight = headerHeight + endHeight;
            }
        }

        if (expand && _savedWindowHeight <= headerHeight)
        {
            endWindowHeight = headerHeight + endHeight;
        }

        var duration = TimeSpan.FromSeconds(1.0);
        var sw = System.Diagnostics.Stopwatch.StartNew();

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
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };

        EventHandler? tick = null;
        tick = (_, _) =>
        {
            var progress = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);
            var eased = 1 - Math.Pow(1 - progress, 3);

            var currentHeight = startHeight + (endHeight - startHeight) * eased;
            var clampedHeight = Math.Max(0, currentHeight);
            _contentRow.Height = new GridLength(clampedHeight);

            var currentWindowHeight = startWindowHeight + (endWindowHeight - startWindowHeight) * eased;
            Height = Math.Max(headerHeight, currentWindowHeight);

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
                    _contentArea.Height = double.NaN;
                }
            }

            if (progress >= 1.0)
            {
                _expandAnimationTimer!.Tick -= tick!;
                _expandAnimationTimer.Stop();
                _expandAnimationTimer = null;

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
                        _contentArea.Height = double.NaN;
                    }
                }
                _isContentExpanded = expand;
                sw.Stop();
            }
        };

        _expandAnimationTimer.Tick += tick;
        _isContentExpanded = !expand;
        _expandAnimationTimer.Start();
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

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

    protected override void OnClosed(EventArgs e)
    {
        _expandAnimationTimer?.Stop();
        _expandAnimationTimer = null;
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }

    internal (bool IsCollapsed, double ExpandedHeight) GetCollapseState()
    {
        return (!_isContentExpanded, _savedContentHeight > 0 ? _savedContentHeight : 240);
    }

    internal void RestoreCollapseState(bool isCollapsed, double expandedHeight)
    {
        if (expandedHeight > 0)
        {
            _savedContentHeight = expandedHeight;
            _savedWindowHeight = expandedHeight + (_headerBar?.Bounds.Height ?? 40);
        }
        if (isCollapsed && _contentRow != null && _rootGrid != null)
        {
            _isContentExpanded = false;
            _contentRow.Height = new GridLength(0);
            if (_contentArea != null)
            {
                _contentArea.IsVisible = false;
                _contentArea.Height = 0;
            }
            Height = _headerBar?.Bounds.Height ?? 40;
        }
    }
}
