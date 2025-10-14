using Avalonia.Controls;
using Avalonia.Input;

namespace Boxes.App.Views;

public partial class DesktopBoxWindow : Window
{
    public DesktopBoxWindow()
    {
        InitializeComponent();
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
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
}

