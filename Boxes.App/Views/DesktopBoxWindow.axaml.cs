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

