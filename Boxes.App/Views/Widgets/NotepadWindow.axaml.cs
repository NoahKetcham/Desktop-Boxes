using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Boxes.App.ViewModels.Widgets;

namespace Boxes.App.Views.Widgets;

public partial class NotepadWindow : Window
{
    private bool _headerDragging;
    private PixelPoint _dragStartWindow;
    private PixelPoint _dragStartScreenPosition;

    public NotepadWindow()
    {
        InitializeComponent();
    }

    private NotepadWindowViewModel ViewModel => (NotepadWindowViewModel)DataContext!;

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

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
        {
            return;
        }

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
}
