using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }
    public IRelayCommand OpenSettingsCommand { get; }

    private readonly NavigationItemViewModel? _settingsNavigationItem;

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

        _settingsNavigationItem = NavigationItems.FirstOrDefault(item => item.Title == "Settings");
        OpenSettingsCommand = new RelayCommand(NavigateToSettings);
        SelectedNavigationItem = NavigationItems.FirstOrDefault();
    }

    public void NavigateToSettings()
    {
        if (_settingsNavigationItem is not null)
        {
            SelectedNavigationItem = _settingsNavigationItem;
        }
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
