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
    public string Title => "Settings";
    public string Description => "Customize your Desktop Boxes experience";

    // Category Navigation
    [ObservableProperty]
    private string? selectedCategory = "appearance";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool isCategoryVisible = true;

    // Appearance
    [ObservableProperty]
    private string themePreference = "System";

    [ObservableProperty]
    private Color selectedAccentColor = Color.Parse("#3A8DFF");

    [ObservableProperty]
    private Color selectedBoxBackgroundColor = Color.Parse("#1C2235");

    [ObservableProperty]
    private string? boxBackgroundColorHex = "#1C2235";

    [ObservableProperty]
    private int boxesTransparencyPercent = 100;

    // Behavior
    [ObservableProperty]
    private bool autoSnapEnabled = true;

    [ObservableProperty]
    private bool showBoxOutlines = true;

    [ObservableProperty]
    private bool runAtStartup;

    // Integrations
    [ObservableProperty]
    private bool oneDriveLinked;

    [ObservableProperty]
    private bool googleDriveLinked;

    // Box Customization
    [ObservableProperty]
    private int boxHeaderHeight = 40;

    [ObservableProperty]
    private bool showBoxHeader = true;

    [ObservableProperty]
    private int boxCornerRadius = 8;

    [ObservableProperty]
    private int boxIconSize = 48;

    [ObservableProperty]
    private bool showShortcutLabels = true;

    [ObservableProperty]
    private int boxContentPadding = 12;

    [ObservableProperty]
    private int boxContentVerticalPadding = 12;

    // Popup State
    [ObservableProperty]
    private bool isAccentColorPickerPopupOpen;

    [ObservableProperty]
    private bool isColorPickerPopupOpen;

    [ObservableProperty]
    private Color? originalAccentColor;

    // Recent Colors
    [ObservableProperty]
    private ObservableCollection<Color> recentAccentColors = new();

    [ObservableProperty]
    private ObservableCollection<Color> recentBoxBackgroundColors = new();

    // Commands
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ResetDataCommand { get; }
    public IAsyncRelayCommand OpenAllWindowsCommand { get; }
    public IAsyncRelayCommand CloseAllWindowsCommand { get; }
    public IAsyncRelayCommand ResetAccentCommand { get; }
    public IAsyncRelayCommand CreateShowBoxesShortcutCommand { get; }
    public IAsyncRelayCommand<string> SelectCategoryCommand { get; }
    public IAsyncRelayCommand<string> ResetCategoryCommand { get; }

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
        SelectCategoryCommand = new AsyncRelayCommand<string>(SelectCategoryAsync);
        ResetCategoryCommand = new AsyncRelayCommand<string>(ResetCategoryAsync);

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

    partial void OnSelectedCategoryChanged(string? value)
    {
        // Update visibility based on category
        UpdateCategoryVisibility();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Filter settings based on search - could implement partial matching
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedCategory = "appearance";
        }
    }

    private void UpdateCategoryVisibility()
    {
        // This would be used with a converter in the XAML
        // For now, we rely on IsVisible binding with converter
    }

    private async Task SelectCategoryAsync(string? category)
    {
        SelectedCategory = category;
        await Task.CompletedTask;
    }

    private async Task ResetCategoryAsync(string? category)
    {
        var confirmed = await DialogService.ShowConfirmationAsync($"Reset {category} settings to defaults?");
        if (!confirmed) return;

        switch (category)
        {
            case "appearance":
                ThemePreference = "System";
                SelectedAccentColor = Color.Parse("#3A8DFF");
                SelectedBoxBackgroundColor = Color.Parse("#1C2235");
                BoxesTransparencyPercent = 100;
                break;
            case "behavior":
                AutoSnapEnabled = true;
                ShowBoxOutlines = true;
                RunAtStartup = false;
                break;
            case "customization":
                BoxHeaderHeight = 40;
                ShowBoxHeader = true;
                BoxCornerRadius = 8;
                BoxIconSize = 48;
                ShowShortcutLabels = true;
                BoxContentPadding = 12;
                BoxContentVerticalPadding = 12;
                break;
            case "integrations":
                OneDriveLinked = false;
                GoogleDriveLinked = false;
                break;
        }

        await SaveAsync();
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
            RecentBoxBackgroundColors = RecentBoxBackgroundColors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList(),
            BoxHeaderHeight = BoxHeaderHeight,
            ShowBoxHeader = ShowBoxHeader,
            BoxCornerRadius = BoxCornerRadius,
            BoxIconSize = BoxIconSize,
            ShowShortcutLabels = ShowShortcutLabels,
            BoxContentPadding = BoxContentPadding,
            BoxContentVerticalPadding = BoxContentVerticalPadding
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

        // Load recent colors
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

        // Box customization
        BoxHeaderHeight = settings.BoxHeaderHeight;
        ShowBoxHeader = settings.ShowBoxHeader;
        BoxCornerRadius = settings.BoxCornerRadius;
        BoxIconSize = settings.BoxIconSize;
        ShowShortcutLabels = settings.ShowShortcutLabels;
        BoxContentPadding = settings.BoxContentPadding;
        BoxContentVerticalPadding = settings.BoxContentVerticalPadding;
    }

    // Auto-save box customization when changed
    partial void OnBoxHeaderHeightChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnShowBoxHeaderChanged(bool value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxCornerRadiusChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxIconSizeChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnShowShortcutLabelsChanged(bool value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxContentPaddingChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxContentVerticalPaddingChanged(int value) => _ = SaveBoxCustomizationAsync();

    private async Task SaveBoxCustomizationAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxHeaderHeight = BoxHeaderHeight;
        current.ShowBoxHeader = ShowBoxHeader;
        current.BoxCornerRadius = BoxCornerRadius;
        current.BoxIconSize = BoxIconSize;
        current.ShowShortcutLabels = ShowShortcutLabels;
        current.BoxContentPadding = BoxContentPadding;
        current.BoxContentVerticalPadding = BoxContentVerticalPadding;
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async Task ResetDataAsync()
    {
        var confirmed = await DialogService.ShowConfirmationAsync(
            "This will delete all Boxes saved data and restart the app. Continue?");
        if (!confirmed) return;

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
        OriginalAccentColor = SelectedAccentColor;
        IsAccentColorPickerPopupOpen = true;
    }

    private void CloseAccentColorPickerPopup()
    {
        if (OriginalAccentColor.HasValue)
        {
            SelectedAccentColor = OriginalAccentColor.Value;
            AccentService.ApplyAccent(OriginalAccentColor.Value);
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

        OriginalAccentColor = null;
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
        var existing = RecentAccentColors.FirstOrDefault(c => c.R == color.R && c.G == color.G && c.B == color.B);
        if (existing != default)
        {
            RecentAccentColors.Remove(existing);
        }

        RecentAccentColors.Insert(0, color);

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
            : "Failed to create shortcut. Try running with sufficient permissions.";
        await DialogService.ShowConfirmationAsync(message);
    }

    partial void OnSelectedBoxBackgroundColorChanged(Color value)
    {
        BoxBackgroundColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    partial void OnSelectedAccentColorChanged(Color value)
    {
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
        var existing = RecentBoxBackgroundColors.FirstOrDefault(c => c.R == color.R && c.G == color.G && c.B == color.B);
        if (existing != default)
        {
            RecentBoxBackgroundColors.Remove(existing);
        }

        RecentBoxBackgroundColors.Insert(0, color);

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
