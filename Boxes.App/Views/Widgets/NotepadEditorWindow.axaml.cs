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

public partial class NotepadEditorWindow : Window
{
    private bool _headerDragging;
    private PixelPoint _dragStartWindow;
    private PixelPoint _dragStartScreenPosition;

    public NotepadEditorWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private NotepadWindowViewModel ViewModel => (NotepadWindowViewModel)DataContext!;

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
            var contentArea = this.FindControl<Border>("ContentArea");
            if (contentArea != null)
            {
                var vertical = verticalPadding > 0 ? verticalPadding : 12;
                contentArea.Padding = new Thickness(contentPadding, vertical, contentPadding, vertical);
            }

            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 20);
            var rootGrid = this.FindControl<Grid>("RootGrid");
            if (rootGrid != null)
            {
                rootGrid.Background = new SolidColorBrush(bgColor, opacity);
                rootGrid.ClipToBounds = true;
            }

            var headerBar = this.FindControl<Border>("HeaderBar");
            if (headerBar != null)
            {
                headerBar.IsVisible = settings.ShowBoxHeader;
                var headerHeight = Math.Clamp(settings.BoxHeaderHeight, 30, 60);
                headerBar.MinHeight = headerHeight;
                headerBar.Padding = new Thickness(10, (headerHeight - 26) / 2);
                headerBar.CornerRadius = settings.ShowBoxHeader
                    ? new CornerRadius(cornerRadius, cornerRadius, 0, 0)
                    : new CornerRadius(0);

                if (contentArea != null && !settings.ShowBoxHeader)
                {
                    contentArea.CornerRadius = new CornerRadius(cornerRadius);
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
                headerBar.Background = new SolidColorBrush(headerColor, opacity);
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
        _dragStartScreenPosition = new PixelPoint(screenX, screenY);
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void Header_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_headerDragging)
            return;

        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = Position.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = Position.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);

        var deltaX = currentScreenPos.X - _dragStartScreenPosition.X;
        var deltaY = currentScreenPos.Y - _dragStartScreenPosition.Y;

        var newX = _dragStartWindow.X + deltaX;
        var newY = _dragStartWindow.Y + deltaY;
        Position = new PixelPoint(newX, newY);
        e.Handled = true;
    }

    private void Header_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_headerDragging)
        {
            _headerDragging = false;
            e.Pointer.Capture(null);
        }
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
