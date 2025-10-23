using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class TaskbarBoxWindow : Window
{
    private bool _dragging;
    private PixelPoint _startWindow;
    private Point _startPointer;

    public TaskbarBoxWindow()
    {
        InitializeComponent();
        HookDataContext();
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

    private void Header_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = false; // become true only after threshold movement
        _startWindow = Position;
        _startPointer = e.GetPosition(this);
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void Header_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = e.GetPosition(this);
        var deltaX = current.X - _startPointer.X;
        if (!_dragging && Math.Abs(deltaX) < 3)
        {
            return; // ignore tiny movement to allow click
        }

        _dragging = true;
        var newX = _startWindow.X + (int)deltaX;
        var working = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var clampedX = Math.Clamp(newX, working.X, working.Right - (int)Bounds.Width);
        var y = working.Bottom - (int)Bounds.Height;
        Position = new PixelPoint(clampedX, y);
        e.Handled = true;
    }

    private void Header_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                ViewModel.ToggleExpanded();
            }
            return;
        }

        _dragging = false;
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

    private async void OnToggleExpandRequested(object? sender, bool expanded)
    {
        await Boxes.App.Services.AppServices.BoxWindowManager.SetSnappedExpandedAsync(ViewModel.Model.Id, expanded);
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
}


