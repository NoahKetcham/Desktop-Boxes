using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private async void CreateShortcutsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.CreateShortcutsAsync();
    }
}

