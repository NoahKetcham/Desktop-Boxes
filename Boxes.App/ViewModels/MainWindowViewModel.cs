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

    public AdvertisingViewModel Advertising { get; } = new();

    public MainWindowViewModel()
    {
        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("Overview", "High-level status and quick stats", new OverviewPageViewModel()),
            new("Dashboard", "Create and manage your boxes", new DashboardPageViewModel()),
            new("Settings", "Configure appearance and behavior", new SettingsPageViewModel())
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
    }
}
