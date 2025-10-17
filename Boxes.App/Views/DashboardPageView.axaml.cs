using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Boxes.App.Models;
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

    private void ScanTile_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (sender is Border border && border.DataContext is DesktopFileViewModel file)
        {
            if (file.ItemType == ScannedItemType.Folder)
            {
                _viewModel.EnterFolderCommand.Execute(file);
            }
            else
            {
                // For files we can toggle selection or future behavior
            }
        }
    }

}

