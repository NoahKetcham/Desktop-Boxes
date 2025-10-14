using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
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
        if (ScrollViewer is null)
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
            double targetOffset = ScrollViewer.Offset.Y + target.Bounds.Top - ScrollViewer.Viewport.Height * 0.1;
            targetOffset = Math.Max(0, targetOffset);

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(300),
                FillMode = FillMode.Forward
            };

            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(0),
                Setters = { new Setter(ScrollViewer.OffsetProperty, new Vector(ScrollViewer.Offset.X, ScrollViewer.Offset.Y)) }
            });
            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(1),
                Setters = { new Setter(ScrollViewer.OffsetProperty, new Vector(ScrollViewer.Offset.X, targetOffset)) }
            });

            await animation.RunAsync(ScrollViewer);
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
}

