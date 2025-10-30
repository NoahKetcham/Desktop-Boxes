using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

    [ObservableProperty]
    private int boxesTransparencyPercent = 100;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ResetDataCommand { get; }
    public IAsyncRelayCommand OpenAllWindowsCommand { get; }
    public IAsyncRelayCommand CloseAllWindowsCommand { get; }

    public SettingsPageViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetDataCommand = new AsyncRelayCommand(ResetDataAsync);
        OpenAllWindowsCommand = new AsyncRelayCommand(OpenAllWindowsAsync);
        CloseAllWindowsCommand = new AsyncRelayCommand(CloseAllWindowsAsync);
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
            GoogleDriveLinked = GoogleDriveLinked,
            BoxesTransparencyPercent = BoxesTransparencyPercent
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
        BoxesTransparencyPercent = settings.BoxesTransparencyPercent;
    }

    private async Task ResetDataAsync()
    {
        var confirmed = await DialogService.ShowConfirmationAsync("This will delete all Boxes saved data and restart the app. Continue?");
        if (!confirmed)
        {
            return;
        }

        await AppServices.DataMaintenanceService.ResetAllAsync();

        var app = Application.Current;
        if (app is { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime desktopLifetime })
        {
            desktopLifetime.Shutdown();
        }
    }

    private async Task OpenAllWindowsAsync()
    {
        await AppServices.BoxWindowManager.OpenAllWindowsAsync().ConfigureAwait(false);
    }

    private async Task CloseAllWindowsAsync()
    {
        await AppServices.BoxWindowManager.CloseAllWindowsAsync().ConfigureAwait(false);
    }
}

