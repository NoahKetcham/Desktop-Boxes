using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class DashboardPageView : UserControl
{
    private DashboardPageViewModel? _viewModel;

    public DashboardPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as DashboardPageViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is DashboardPageViewModel vm && e.PropertyName == nameof(DashboardPageViewModel.SelectedTab))
        {
            await ScrollToSelectedSection(vm.SelectedTab);
        }
    }

    private async Task ScrollToSelectedSection(DashboardTab tab)
    {
        var scrollViewer = ScrollViewer;
        if (scrollViewer is null)
        {
            return;
        }

        Control? target = tab switch
        {
            DashboardTab.BoxManager => BoxSection,
            DashboardTab.ScanDesktop => ScanResultsCard,
            _ => null
        };

        if (target is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            double targetOffset = scrollViewer.Offset.Y + target.Bounds.Top - scrollViewer.Viewport.Height * 0.1;
            targetOffset = Math.Max(0, targetOffset);

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                FillMode = FillMode.Forward
            };

            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(0),
                Setters = { new Setter(ScrollViewer.OffsetProperty, scrollViewer.Offset) }
            });
            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(1),
                Setters = { new Setter(ScrollViewer.OffsetProperty, scrollViewer.Offset.WithY(targetOffset)) }
            });

            await animation.RunAsync(scrollViewer);
        });
    }

    private void ScrollViewer_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.Delta.Y < 0 && _viewModel.SelectedTab != DashboardTab.ScanDesktop)
        {
            _viewModel.SelectedTab = DashboardTab.ScanDesktop;
            e.Handled = true;
        }
        else if (e.Delta.Y > 0 && _viewModel.SelectedTab != DashboardTab.BoxManager)
        {
            _viewModel.SelectedTab = DashboardTab.BoxManager;
            e.Handled = true;
        }
    }

    private async void CreateShortcutsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Choose destination for shortcuts"
        };

        if (this.VisualRoot is not Window window)
        {
            return;
        }

        var folder = await dialog.ShowAsync(window);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await _viewModel.CreateShortcutsAsync(folder);
    }
}

