using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Boxes.App.Services;
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // No-op; reserved for future use
    }

    private async void OnBoxNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is BoxSummaryViewModel box)
        {
            var model = box.ToModel();
            var updated = await AppServices.BoxService.AddOrUpdateAsync(model);
            await AppServices.BoxWindowManager.UpdateAsync(updated);
            box.UpdateFromModel(updated);
        }
    }
}

