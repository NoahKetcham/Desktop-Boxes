using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
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
        private bool runAtStartup;

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
        public IAsyncRelayCommand ResetAccentCommand { get; }
    public IAsyncRelayCommand CreateShowBoxesShortcutCommand { get; }

    [ObservableProperty]
    private Color selectedAccentColor = Color.Parse("#3A8DFF");

    [ObservableProperty]
    private bool isAccentColorPickerPopupOpen;

    private Color? _originalAccentColor;

    [ObservableProperty]
    private string? boxBackgroundColorHex = "#1C2235";

    [ObservableProperty]
    private Color selectedBoxBackgroundColor = Color.Parse("#1C2235");

    [ObservableProperty]
    private bool isColorPickerPopupOpen;

    [ObservableProperty]
    private ObservableCollection<Color> recentAccentColors = new();

    [ObservableProperty]
    private ObservableCollection<Color> recentBoxBackgroundColors = new();

    public IRelayCommand ApplyBoxBackgroundColorCommand { get; }
    public IRelayCommand ResetBoxBackgroundColorCommand { get; }
    public IRelayCommand OpenColorPickerPopupCommand { get; }
    public IRelayCommand CloseColorPickerPopupCommand { get; }
    public IRelayCommand ApplyAccentColorCommand { get; }
    public IRelayCommand OpenAccentColorPickerPopupCommand { get; }
    public IRelayCommand CloseAccentColorPickerPopupCommand { get; }
    public IRelayCommand<Color> SelectRecentAccentColorCommand { get; }
    public IRelayCommand<Color> SelectRecentBoxBackgroundColorCommand { get; }

    public SettingsPageViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetDataCommand = new AsyncRelayCommand(ResetDataAsync);
        OpenAllWindowsCommand = new AsyncRelayCommand(OpenAllWindowsAsync);
        CloseAllWindowsCommand = new AsyncRelayCommand(CloseAllWindowsAsync);
        ResetAccentCommand = new AsyncRelayCommand(ResetAccentAsync);
        CreateShowBoxesShortcutCommand = new AsyncRelayCommand(CreateShowBoxesShortcutAsync);
        ApplyBoxBackgroundColorCommand = new RelayCommand(ApplyBoxBackgroundColor);
        ResetBoxBackgroundColorCommand = new RelayCommand(ResetBoxBackgroundColor);
        OpenColorPickerPopupCommand = new RelayCommand(() => IsColorPickerPopupOpen = true);
        CloseColorPickerPopupCommand = new RelayCommand(() => IsColorPickerPopupOpen = false);
        ApplyAccentColorCommand = new RelayCommand(ApplyAccentColor);
        OpenAccentColorPickerPopupCommand = new RelayCommand(OpenAccentColorPickerPopup);
        CloseAccentColorPickerPopupCommand = new RelayCommand(CloseAccentColorPickerPopup);
        SelectRecentAccentColorCommand = new RelayCommand<Color>(SelectRecentAccentColor);
        SelectRecentBoxBackgroundColorCommand = new RelayCommand<Color>(SelectRecentBoxBackgroundColor);
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
            RunAtStartup = RunAtStartup,
            OneDriveLinked = OneDriveLinked,
            GoogleDriveLinked = GoogleDriveLinked,
                BoxesTransparencyPercent = BoxesTransparencyPercent,
                AccentHex = $"#{SelectedAccentColor.R:X2}{SelectedAccentColor.G:X2}{SelectedAccentColor.B:X2}",
            BoxBackgroundColor = BoxBackgroundColorHex ?? "#1C2235",
            RecentAccentColors = RecentAccentColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList(),
            RecentBoxBackgroundColors = RecentBoxBackgroundColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList()
        };

        await AppServices.SettingsService.SaveAsync(model);

        if (RunAtStartup)
        {
            _ = DesktopIntegrationService.EnableRunAtStartup();
        }
        else
        {
            DesktopIntegrationService.DisableRunAtStartup();
        }
    }

    private void Apply(ApplicationSettings settings)
    {
        ThemePreference = settings.ThemePreference;
        AutoSnapEnabled = settings.AutoSnapEnabled;
        ShowBoxOutlines = settings.ShowBoxOutlines;
        RunAtStartup = settings.RunAtStartup;
        OneDriveLinked = settings.OneDriveLinked;
        GoogleDriveLinked = settings.GoogleDriveLinked;
        BoxesTransparencyPercent = settings.BoxesTransparencyPercent;
        if (!string.IsNullOrWhiteSpace(settings.AccentHex) && Color.TryParse(settings.AccentHex, out var accentColor))
        {
            SelectedAccentColor = accentColor;
            AccentService.ApplyAccent(accentColor);
        }
        BoxBackgroundColorHex = settings.BoxBackgroundColor ?? "#1C2235";
        if (Color.TryParse(BoxBackgroundColorHex, out var color))
        {
            SelectedBoxBackgroundColor = color;
        }

        // Load recently used colors
        RecentAccentColors.Clear();
        if (settings.RecentAccentColors != null)
        {
            foreach (var hex in settings.RecentAccentColors.Take(5))
            {
                if (Color.TryParse(hex, out var recentColor))
                {
                    RecentAccentColors.Add(recentColor);
                }
            }
        }

        RecentBoxBackgroundColors.Clear();
        if (settings.RecentBoxBackgroundColors != null)
        {
            foreach (var hex in settings.RecentBoxBackgroundColors.Take(5))
            {
                if (Color.TryParse(hex, out var recentColor))
                {
                    RecentBoxBackgroundColors.Add(recentColor);
                }
            }
        }
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

    private void OpenAccentColorPickerPopup()
    {
        _originalAccentColor = SelectedAccentColor;
        IsAccentColorPickerPopupOpen = true;
    }

    private void CloseAccentColorPickerPopup()
    {
        if (_originalAccentColor.HasValue)
        {
            SelectedAccentColor = _originalAccentColor.Value;
            AccentService.ApplyAccent(_originalAccentColor.Value);
        }
        IsAccentColorPickerPopupOpen = false;
    }

    private async void ApplyAccentColor()
    {
        AccentService.ApplyAccent(SelectedAccentColor);
        AddToRecentAccentColors(SelectedAccentColor);
        var current = await AppServices.SettingsService.GetAsync();
        current.AccentHex = $"#{SelectedAccentColor.R:X2}{SelectedAccentColor.G:X2}{SelectedAccentColor.B:X2}";
        current.RecentAccentColors = RecentAccentColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        await AppServices.SettingsService.SaveAsync(current);
        _originalAccentColor = null;
        IsAccentColorPickerPopupOpen = false;
    }

    private void SelectRecentAccentColor(Color color)
    {
        SelectedAccentColor = color;
        AccentService.ApplyAccent(color);
        AddToRecentAccentColors(color);
        _ = SaveRecentAccentColorsAsync();
    }

    private void AddToRecentAccentColors(Color color)
    {
        // Remove if already exists
        var existing = RecentAccentColors.FirstOrDefault(c => c.R == color.R && c.G == color.G && c.B == color.B);
        if (existing != default)
        {
            RecentAccentColors.Remove(existing);
        }
        
        // Add to beginning
        RecentAccentColors.Insert(0, color);
        
        // Keep only 5 most recent
        while (RecentAccentColors.Count > 5)
        {
            RecentAccentColors.RemoveAt(RecentAccentColors.Count - 1);
        }
    }

    private async Task SaveRecentAccentColorsAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.AccentHex = $"#{SelectedAccentColor.R:X2}{SelectedAccentColor.G:X2}{SelectedAccentColor.B:X2}";
        current.RecentAccentColors = RecentAccentColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async Task ResetAccentAsync()
    {
        AccentService.ResetToDefault();
        var current = await AppServices.SettingsService.GetAsync();
        current.AccentHex = null;
        await AppServices.SettingsService.SaveAsync(current);
        SelectedAccentColor = Color.Parse("#3A8DFF");
    }

    private async Task CreateShowBoxesShortcutAsync()
    {
        var ok = DesktopIntegrationService.CreateShowBurstStartMenuShortcut();
        var message = ok
            ? "Shortcut created in Start Menu → Programs. You can pin it to the taskbar."
            : "Failed to create shortcut. Try running the app with sufficient permissions.";
        await DialogService.ShowConfirmationAsync(message);
    }

    partial void OnSelectedBoxBackgroundColorChanged(Color value)
    {
        BoxBackgroundColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    partial void OnSelectedAccentColorChanged(Color value)
    {
        // Preview accent color changes in real-time
        AccentService.ApplyAccent(value);
    }

    private async void ApplyBoxBackgroundColor()
    {
        AddToRecentBoxBackgroundColors(SelectedBoxBackgroundColor);
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxBackgroundColor = BoxBackgroundColorHex ?? "#1C2235";
        current.RecentBoxBackgroundColors = RecentBoxBackgroundColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        await AppServices.SettingsService.SaveAsync(current);
        IsColorPickerPopupOpen = false;
    }

    private void SelectRecentBoxBackgroundColor(Color color)
    {
        SelectedBoxBackgroundColor = color;
        BoxBackgroundColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        AddToRecentBoxBackgroundColors(color);
        _ = SaveRecentBoxBackgroundColorsAsync();
    }

    private void AddToRecentBoxBackgroundColors(Color color)
    {
        // Remove if already exists
        var existing = RecentBoxBackgroundColors.FirstOrDefault(c => c.R == color.R && c.G == color.G && c.B == color.B);
        if (existing != default)
        {
            RecentBoxBackgroundColors.Remove(existing);
        }
        
        // Add to beginning
        RecentBoxBackgroundColors.Insert(0, color);
        
        // Keep only 5 most recent
        while (RecentBoxBackgroundColors.Count > 5)
        {
            RecentBoxBackgroundColors.RemoveAt(RecentBoxBackgroundColors.Count - 1);
        }
    }

    private async Task SaveRecentBoxBackgroundColorsAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxBackgroundColor = BoxBackgroundColorHex ?? "#1C2235";
        current.RecentBoxBackgroundColors = RecentBoxBackgroundColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async void ResetBoxBackgroundColor()
    {
        BoxBackgroundColorHex = "#1C2235";
        SelectedBoxBackgroundColor = Color.Parse("#1C2235");
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxBackgroundColor = "#1C2235";
        await AppServices.SettingsService.SaveAsync(current);
    }
}

