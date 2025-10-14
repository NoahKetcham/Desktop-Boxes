using System.Threading.Tasks;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    public string Title => "Global Settings";
    public string Description => "Control appearance, behavior, and integrations";

    [ObservableProperty]
    private string themePreference = "System";

    [ObservableProperty]
    private bool autoSnapEnabled = true;

    [ObservableProperty]
    private bool showBoxOutlines = true;

    [ObservableProperty]
    private bool oneDriveLinked;

    [ObservableProperty]
    private bool googleDriveLinked;

    public IAsyncRelayCommand SaveCommand { get; }

    public SettingsPageViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var settings = await AppServices.SettingsService.GetAsync();
        Apply(settings);
    }

    private async Task SaveAsync()
    {
        var model = new ApplicationSettings
        {
            ThemePreference = ThemePreference,
            AutoSnapEnabled = AutoSnapEnabled,
            ShowBoxOutlines = ShowBoxOutlines,
            OneDriveLinked = OneDriveLinked,
            GoogleDriveLinked = GoogleDriveLinked
        };

        await AppServices.SettingsService.SaveAsync(model);
    }

    private void Apply(ApplicationSettings settings)
    {
        ThemePreference = settings.ThemePreference;
        AutoSnapEnabled = settings.AutoSnapEnabled;
        ShowBoxOutlines = settings.ShowBoxOutlines;
        OneDriveLinked = settings.OneDriveLinked;
        GoogleDriveLinked = settings.GoogleDriveLinked;
    }
}

