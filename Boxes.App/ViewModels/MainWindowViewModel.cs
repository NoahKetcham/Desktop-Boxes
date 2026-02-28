using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    [ObservableProperty]
    private ViewModelBase? currentPage;

    [ObservableProperty]
    private ViewModelBase? sidebarContent;

    [ObservableProperty]
    private bool isRightSidebarVisible = true;

    public AdvertisingViewModel Advertising { get; } = new();

    private readonly DashboardBoxSettingsViewModel _dashboardSidebar;
    private readonly DashboardPageViewModel _dashboard;

    public MainWindowViewModel()
    {
        var overview = new OverviewPageViewModel();
        _dashboard = new DashboardPageViewModel();
        var settings = new SettingsPageViewModel();

        _dashboardSidebar = new DashboardBoxSettingsViewModel(_dashboard);
        _dashboard.BoxSettingsHost = _dashboardSidebar;

        sidebarContent = Advertising;

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("Overview", "High-level status and quick stats", overview, "📊"),
            new("Dashboard", "Create and manage your boxes", _dashboard, "📦"),
            new("Settings", "Configure appearance and behavior", settings, "⚙️")
        };

        SelectedNavigationItem = NavigationItems.FirstOrDefault();
    }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        CurrentPage = value?.Content;

        // Hide sidebar on Dashboard and Settings, show advertising on Overview
        var hideRightSidebar = value?.Content is SettingsPageViewModel || value?.Content is DashboardPageViewModel;
        IsRightSidebarVisible = !hideRightSidebar;
        SidebarContent = value?.Content switch
        {
            DashboardPageViewModel => null,
            SettingsPageViewModel => null,
            _ => Advertising
        };
    }
}
