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

public partial class NoteHubPreviewWindow : Window
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
    private Border? _snapIndicator;

    public NoteHubPreviewWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        _contentArea = this.FindControl<Border>("ContentArea");
        _rootGrid = this.FindControl<Grid>("RootGrid");
        _headerBar = this.FindControl<Border>("HeaderBar");
        _snapIndicator = this.FindControl<Border>("SnapIndicator");
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private NoteHubWindowViewModel ViewModel => (NoteHubWindowViewModel)DataContext!;

    private void OnOpened(object? sender, EventArgs e)
    {
        this.SetAlwaysBelowApps();
        ApplyTransparencyFromSettings();
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

            if (_headerBar != null)
            {
                _headerBar.IsVisible = settings.ShowBoxHeader;
                var headerHeight = Math.Clamp(settings.BoxHeaderHeight, 30, 60);
                _headerBar.MinHeight = headerHeight;
                _headerBar.Padding = new Thickness(10, (headerHeight - 26) / 2);
                _headerBar.CornerRadius = new CornerRadius(cornerRadius, cornerRadius, 0, 0);
                _headerBar.Background = new SolidColorBrush(headerColor, opacity);
            }

            if (_contentArea != null)
            {
                _contentArea.CornerRadius = settings.ShowBoxHeader
                    ? new CornerRadius(0, 0, cornerRadius, cornerRadius)
                    : new CornerRadius(cornerRadius);
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

        if (_isDraggingWindow)
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
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
