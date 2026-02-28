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

        // Show dashboard sidebar when on Dashboard, advertising on Overview, hide on Settings
        var isSettings = value?.Content is SettingsPageViewModel;
        IsRightSidebarVisible = !isSettings;
        SidebarContent = value?.Content switch
        {
            DashboardPageViewModel => _dashboardSidebar,
            SettingsPageViewModel => null,
            _ => Advertising
        };
    }
}
