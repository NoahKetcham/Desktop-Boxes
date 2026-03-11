using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Boxes.App.Models;
using Boxes.App.Services;
using Boxes.App.ViewModels.Widgets;
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

    // NoteHub
    [ObservableProperty]
    private string? noteHubDirectoryPath;

    // Command Center
    [ObservableProperty]
    private bool commandCenterShowBorder;

    [ObservableProperty]
    private bool commandCenterLocked;

    [ObservableProperty]
    private bool commandCenterShowTitles = true;

    [ObservableProperty]
    private string commandCenterActionButtonColorSource = "App Accent";

    [ObservableProperty]
    private Color selectedCommandCenterActionButtonCustomColor = Color.Parse("#3A8DFF");

    [ObservableProperty]
    private string? commandCenterActionButtonCustomColorHex = "#3A8DFF";

    [ObservableProperty]
    private bool commandCenterActionButtonShowBackground = true;

    [ObservableProperty]
    private bool commandCenterAutoScale = true;

    [ObservableProperty]
    private double commandCenterButtonScaleMultiplier = 1.0;

    [ObservableProperty]
    private int commandCenterPaddingLeft = 6;

    [ObservableProperty]
    private int commandCenterPaddingRight = 6;

    [ObservableProperty]
    private int commandCenterPaddingVertical = 4;

    [ObservableProperty]
    private int commandCenterActionSpacingHorizontal;

    [ObservableProperty]
    private int commandCenterActionSpacingVertical;

    public ObservableCollection<CommandCenterActionItemViewModel> CommandCenterActions { get; } = new();

    public static string[] CommandCenterActionButtonColorSourceOptions { get; } = ["App Accent", "System Accent", "Custom"];

    public bool IsCommandCenterCustomColorMode => CommandCenterActionButtonColorSource == "Custom";

    // Box Customization
    [ObservableProperty]
    private int boxHeaderHeight = 40;

    [ObservableProperty]
    private bool showBoxHeader = true;

    [ObservableProperty]
    private bool showBoxTitle = true;

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

    // Recent Colors
    [ObservableProperty]
    private ObservableCollection<Color> recentAccentColors = new();

    [ObservableProperty]
    private ObservableCollection<Color> recentBoxBackgroundColors = new();

    private bool _updatingCommandCenterActions;
    private bool _loadingCommandCenterSettings;
    private bool _isLoading;

    // Commands
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ResetDataCommand { get; }
    public IAsyncRelayCommand OpenAllWindowsCommand { get; }
    public IAsyncRelayCommand CloseAllWindowsCommand { get; }
    public IAsyncRelayCommand ResetAccentCommand { get; }
    public IAsyncRelayCommand CreateShowBoxesShortcutCommand { get; }
    public IRelayCommand StopDesktopBoxesCommand { get; }
    public IAsyncRelayCommand BrowseNoteHubPathCommand { get; }
    public IAsyncRelayCommand<string> SelectCategoryCommand { get; }
    public IAsyncRelayCommand<string> ResetCategoryCommand { get; }


    public SettingsPageViewModel()
    {
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ResetDataCommand = new AsyncRelayCommand(ResetDataAsync);
        OpenAllWindowsCommand = new AsyncRelayCommand(OpenAllWindowsAsync);
        CloseAllWindowsCommand = new AsyncRelayCommand(CloseAllWindowsAsync);
        ResetAccentCommand = new AsyncRelayCommand(ResetAccentAsync);
        CreateShowBoxesShortcutCommand = new AsyncRelayCommand(CreateShowBoxesShortcutAsync);
        StopDesktopBoxesCommand = new RelayCommand(StopDesktopBoxes);
        BrowseNoteHubPathCommand = new AsyncRelayCommand(BrowseNoteHubPathAsync);
        SelectCategoryCommand = new AsyncRelayCommand<string>(SelectCategoryAsync);
        ResetCategoryCommand = new AsyncRelayCommand<string>(ResetCategoryAsync);

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
                ShowBoxTitle = true;
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
            case "notehub":
                NoteHubDirectoryPath = null;
                break;
            case "commandcenter":
                CommandCenterShowBorder = false;
                CommandCenterLocked = false;
                CommandCenterShowTitles = true;
                CommandCenterActionButtonColorSource = "App Accent";
                CommandCenterActionButtonCustomColorHex = "#3A8DFF";
                SelectedCommandCenterActionButtonCustomColor = Color.Parse("#3A8DFF");
                CommandCenterActionButtonShowBackground = true;
                CommandCenterAutoScale = true;
                CommandCenterButtonScaleMultiplier = 1.0;
                CommandCenterPaddingLeft = 6;
                CommandCenterPaddingRight = 6;
                CommandCenterPaddingVertical = 4;
                CommandCenterActionSpacingHorizontal = 0;
                CommandCenterActionSpacingVertical = 0;
                LoadCommandCenterActions(CommandCenterActionCatalog.CreateDefaultSettings());
                break;
        }

        await SaveAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var settings = await AppServices.SettingsService.GetAsync();
            Apply(settings);
        }
        finally
        {
            _isLoading = false;
        }
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
            ShowBoxTitle = ShowBoxTitle,
            BoxCornerRadius = BoxCornerRadius,
            BoxIconSize = BoxIconSize,
            ShowShortcutLabels = ShowShortcutLabels,
            BoxContentPadding = BoxContentPadding,
            BoxContentVerticalPadding = BoxContentVerticalPadding,
            NoteHubDirectoryPath = string.IsNullOrWhiteSpace(NoteHubDirectoryPath) ? null : NoteHubDirectoryPath.Trim(),
            CommandCenterShowBorder = CommandCenterShowBorder,
            CommandCenterLocked = CommandCenterLocked,
            CommandCenterShowTitles = CommandCenterShowTitles,
            CommandCenterActions = BuildCommandCenterActionSettings(),
            CommandCenterActionButtonColorSource = CommandCenterActionButtonColorSource,
            CommandCenterActionButtonCustomColor = IsCommandCenterCustomColorMode ? CommandCenterActionButtonCustomColorHex : null,
            CommandCenterActionButtonShowBackground = CommandCenterActionButtonShowBackground,
            CommandCenterAutoScale = CommandCenterAutoScale,
            CommandCenterButtonScaleMultiplier = CommandCenterButtonScaleMultiplier,
            CommandCenterPaddingLeft = CommandCenterPaddingLeft,
            CommandCenterPaddingRight = CommandCenterPaddingRight,
            CommandCenterPaddingVertical = CommandCenterPaddingVertical,
            CommandCenterActionSpacingHorizontal = CommandCenterActionSpacingHorizontal,
            CommandCenterActionSpacingVertical = CommandCenterActionSpacingVertical
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
        _loadingCommandCenterSettings = true;
        try
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
            ShowBoxTitle = settings.ShowBoxTitle;
            BoxCornerRadius = settings.BoxCornerRadius;
            BoxIconSize = settings.BoxIconSize;
            ShowShortcutLabels = settings.ShowShortcutLabels;
            BoxContentPadding = settings.BoxContentPadding;
            BoxContentVerticalPadding = settings.BoxContentVerticalPadding;
            NoteHubDirectoryPath = settings.NoteHubDirectoryPath;
            CommandCenterShowBorder = settings.CommandCenterShowBorder;
            CommandCenterLocked = settings.CommandCenterLocked;
            CommandCenterShowTitles = settings.CommandCenterShowTitles;
            CommandCenterActionButtonShowBackground = settings.CommandCenterActionButtonShowBackground;
            CommandCenterAutoScale = settings.CommandCenterAutoScale;
            CommandCenterButtonScaleMultiplier = Math.Clamp(settings.CommandCenterButtonScaleMultiplier, 0.5, 2.0);
            CommandCenterPaddingLeft = settings.CommandCenterPaddingLeft;
            CommandCenterPaddingRight = settings.CommandCenterPaddingRight;
            CommandCenterPaddingVertical = settings.CommandCenterPaddingVertical;
            CommandCenterActionSpacingHorizontal = settings.CommandCenterActionSpacingHorizontal;
            CommandCenterActionSpacingVertical = settings.CommandCenterActionSpacingVertical;
            CommandCenterActionButtonColorSource = settings.CommandCenterActionButtonColorSource ?? "App Accent";
            if (!string.IsNullOrWhiteSpace(settings.CommandCenterActionButtonCustomColor) &&
                Color.TryParse(settings.CommandCenterActionButtonCustomColor, out var ccCustomColor))
            {
                CommandCenterActionButtonCustomColorHex = settings.CommandCenterActionButtonCustomColor;
                SelectedCommandCenterActionButtonCustomColor = ccCustomColor;
            }
            LoadCommandCenterActions(settings.CommandCenterActions);
        }
        finally
        {
            _loadingCommandCenterSettings = false;
        }
    }

    // Auto-save box customization when changed
    partial void OnBoxHeaderHeightChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnShowBoxHeaderChanged(bool value) => _ = SaveBoxCustomizationAsync();
    partial void OnShowBoxTitleChanged(bool value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxCornerRadiusChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxIconSizeChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnShowShortcutLabelsChanged(bool value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxContentPaddingChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnBoxContentVerticalPaddingChanged(int value) => _ = SaveBoxCustomizationAsync();
    partial void OnCommandCenterShowBorderChanged(bool value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterLockedChanged(bool value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterShowTitlesChanged(bool value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterActionButtonShowBackgroundChanged(bool value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterAutoScaleChanged(bool value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterButtonScaleMultiplierChanged(double value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterPaddingLeftChanged(int value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterPaddingRightChanged(int value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterPaddingVerticalChanged(int value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterActionSpacingHorizontalChanged(int value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterActionSpacingVerticalChanged(int value) => _ = SaveCommandCenterSettingsAsync();
    partial void OnCommandCenterActionButtonColorSourceChanged(string value)
    {
        OnPropertyChanged(nameof(IsCommandCenterCustomColorMode));
        _ = SaveCommandCenterSettingsAsync();
    }
    partial void OnSelectedCommandCenterActionButtonCustomColorChanged(Color value)
    {
        CommandCenterActionButtonCustomColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        if (!_isLoading) _ = SaveCommandCenterSettingsAsync();
    }

    // Auto-save appearance when changed
    partial void OnThemePreferenceChanged(string value) { if (!_isLoading) _ = SaveAppearanceAsync(); }
    partial void OnBoxesTransparencyPercentChanged(int value) { if (!_isLoading) _ = SaveAppearanceAsync(); }

    // Auto-save behavior when changed
    partial void OnAutoSnapEnabledChanged(bool value) { if (!_isLoading) _ = SaveBehaviorAsync(); }
    partial void OnShowBoxOutlinesChanged(bool value) { if (!_isLoading) _ = SaveBehaviorAsync(); }
    partial void OnRunAtStartupChanged(bool value) { if (!_isLoading) _ = SaveBehaviorAsync(); }

    // Auto-save integrations when changed
    partial void OnOneDriveLinkedChanged(bool value) { if (!_isLoading) _ = SaveIntegrationsAsync(); }
    partial void OnGoogleDriveLinkedChanged(bool value) { if (!_isLoading) _ = SaveIntegrationsAsync(); }

    // Auto-save notehub when changed
    partial void OnNoteHubDirectoryPathChanged(string? value) { if (!_isLoading) _ = SaveNoteHubAsync(); }

    private async Task SaveBoxCustomizationAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxHeaderHeight = BoxHeaderHeight;
        current.ShowBoxHeader = ShowBoxHeader;
        current.ShowBoxTitle = ShowBoxTitle;
        current.BoxCornerRadius = BoxCornerRadius;
        current.BoxIconSize = BoxIconSize;
        current.ShowShortcutLabels = ShowShortcutLabels;
        current.BoxContentPadding = BoxContentPadding;
        current.BoxContentVerticalPadding = BoxContentVerticalPadding;
        await AppServices.SettingsService.SaveAsync(current);
    }

    private List<CommandCenterActionSetting> BuildCommandCenterActionSettings()
    {
        return CommandCenterActionCatalog.Normalize(CommandCenterActions.Select(action => new CommandCenterActionSetting
        {
            Key = action.Key,
            IsEnabled = action.IsEnabled
        }));
    }

    private void LoadCommandCenterActions(IEnumerable<CommandCenterActionSetting>? actionSettings)
    {
        _updatingCommandCenterActions = true;
        try
        {
            foreach (var action in CommandCenterActions)
            {
                action.PropertyChanged -= OnCommandCenterActionPropertyChanged;
            }

            CommandCenterActions.Clear();
            var normalized = CommandCenterActionCatalog.Normalize(actionSettings);
            for (var i = 0; i < normalized.Count; i++)
            {
                var action = normalized[i];
                var definition = CommandCenterActionCatalog.GetDefinition(action.Key);
                var key = action.Key;
                var item = new CommandCenterActionItemViewModel(
                    key,
                    definition.Title,
                    definition.Description,
                    definition.Icon,
                    action.IsEnabled,
                    () => MoveCommandCenterAction(key, -1),
                    () => MoveCommandCenterAction(key, 1));
                item.PropertyChanged += OnCommandCenterActionPropertyChanged;
                CommandCenterActions.Add(item);
            }
        }
        finally
        {
            _updatingCommandCenterActions = false;
        }
    }

    private void MoveCommandCenterAction(string key, int delta)
    {
        var index = CommandCenterActions.ToList().FindIndex(action => action.Key == key);
        if (index < 0)
        {
            return;
        }

        var newIndex = index + delta;
        if (newIndex < 0 || newIndex >= CommandCenterActions.Count)
        {
            return;
        }

        CommandCenterActions.Move(index, newIndex);
        _ = SaveCommandCenterSettingsAsync();
    }

    private void OnCommandCenterActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandCenterActionItemViewModel.IsEnabled))
        {
            _ = SaveCommandCenterSettingsAsync();
        }
    }

    private async Task SaveCommandCenterSettingsAsync()
    {
        if (_updatingCommandCenterActions || _loadingCommandCenterSettings)
        {
            return;
        }

        var current = await AppServices.SettingsService.GetAsync();
        current.CommandCenterShowBorder = CommandCenterShowBorder;
        current.CommandCenterLocked = CommandCenterLocked;
        current.CommandCenterShowTitles = CommandCenterShowTitles;
        current.CommandCenterActionButtonShowBackground = CommandCenterActionButtonShowBackground;
        current.CommandCenterAutoScale = CommandCenterAutoScale;
        current.CommandCenterButtonScaleMultiplier = CommandCenterButtonScaleMultiplier;
        current.CommandCenterPaddingLeft = CommandCenterPaddingLeft;
        current.CommandCenterPaddingRight = CommandCenterPaddingRight;
        current.CommandCenterPaddingVertical = CommandCenterPaddingVertical;
        current.CommandCenterActionSpacingHorizontal = CommandCenterActionSpacingHorizontal;
        current.CommandCenterActionSpacingVertical = CommandCenterActionSpacingVertical;
        current.CommandCenterActionButtonColorSource = CommandCenterActionButtonColorSource;
        current.CommandCenterActionButtonCustomColor = IsCommandCenterCustomColorMode ? CommandCenterActionButtonCustomColorHex : null;
        current.CommandCenterActions = BuildCommandCenterActionSettings();
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async Task SaveAppearanceAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.ThemePreference = ThemePreference;
        current.BoxesTransparencyPercent = BoxesTransparencyPercent;
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async Task SaveBehaviorAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.AutoSnapEnabled = AutoSnapEnabled;
        current.ShowBoxOutlines = ShowBoxOutlines;
        current.RunAtStartup = RunAtStartup;
        await AppServices.SettingsService.SaveAsync(current);

        if (RunAtStartup)
            _ = DesktopIntegrationService.EnableRunAtStartup();
        else
            DesktopIntegrationService.DisableRunAtStartup();
    }

    private async Task SaveIntegrationsAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.OneDriveLinked = OneDriveLinked;
        current.GoogleDriveLinked = GoogleDriveLinked;
        await AppServices.SettingsService.SaveAsync(current);
    }

    private async Task SaveNoteHubAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.NoteHubDirectoryPath = string.IsNullOrWhiteSpace(NoteHubDirectoryPath) ? null : NoteHubDirectoryPath.Trim();
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

    private async Task SaveAccentColorAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.AccentHex = $"#{SelectedAccentColor.R:X2}{SelectedAccentColor.G:X2}{SelectedAccentColor.B:X2}";
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

    private void StopDesktopBoxes()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private async Task BrowseNoteHubPathAsync()
    {
        var window = AppServices.MainWindowOwner;
        if (window == null) return;

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NoteHub directory",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            NoteHubDirectoryPath = path;
        }
    }

    partial void OnSelectedBoxBackgroundColorChanged(Color value)
    {
        BoxBackgroundColorHex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
        if (!_isLoading) _ = SaveBoxBackgroundColorAsync();
    }

    partial void OnSelectedAccentColorChanged(Color value)
    {
        AccentService.ApplyAccent(value);
        if (!_isLoading) _ = SaveAccentColorAsync();
    }

    private async Task SaveBoxBackgroundColorAsync()
    {
        var current = await AppServices.SettingsService.GetAsync();
        current.BoxBackgroundColor = BoxBackgroundColorHex ?? "#1C2235";
        await AppServices.SettingsService.SaveAsync(current);
    }


}
