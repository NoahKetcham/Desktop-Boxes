using Avalonia.Controls;
using Avalonia.Input;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnDeselectPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.SidebarContent is DashboardBoxSettingsViewModel dashboardSidebar)
        {
            dashboardSidebar.DismissTemplateInfoOrClear();
        }
    }
}