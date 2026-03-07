using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels.Widgets;

public partial class CommandCenterWindowViewModel : ViewModelBase
{
    public ObservableCollection<CommandCenterActionButtonViewModel> Actions { get; } = new();

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _showTitles = true;

    public string PinButtonText => IsLocked ? "📌" : "📍";

    public IAsyncRelayCommand ToggleLockCommand { get; }
    public IAsyncRelayCommand OpenNoteHubCommand { get; }
    public IAsyncRelayCommand ToggleBoxesCommand { get; }
    public IAsyncRelayCommand ToggleDesktopIconsCommand { get; }

    public CommandCenterWindowViewModel()
    {
        ToggleLockCommand = new AsyncRelayCommand(ToggleLockAsync);
        OpenNoteHubCommand = new AsyncRelayCommand(OpenNoteHubAsync);
        ToggleBoxesCommand = new AsyncRelayCommand(ToggleBoxesAsync);
        ToggleDesktopIconsCommand = new AsyncRelayCommand(ToggleDesktopIconsAsync);

        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var settings = await AppServices.SettingsService.GetAsync().ConfigureAwait(false);
        RebuildActions(settings);
        await RefreshActionStateAsync().ConfigureAwait(false);
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings settings)
    {
        RebuildActions(settings);
        _ = RefreshActionStateAsync();
    }

    private void RebuildActions(ApplicationSettings settings)
    {
        IsLocked = settings.CommandCenterLocked;
        ShowTitles = settings.CommandCenterShowTitles;
        var configuredActions = CommandCenterActionCatalog.Normalize(settings.CommandCenterActions);

        void Rebuild()
        {
            Actions.Clear();
            foreach (var configuredAction in configuredActions.Where(action => action.IsEnabled))
            {
                var definition = CommandCenterActionCatalog.GetDefinition(configuredAction.Key);
                var command = configuredAction.Key switch
                {
                    CommandCenterActionCatalog.OpenNoteHub => OpenNoteHubCommand,
                    CommandCenterActionCatalog.ToggleBoxes => ToggleBoxesCommand,
                    CommandCenterActionCatalog.ToggleDesktopIcons => ToggleDesktopIconsCommand,
                    _ => null
                };

                if (command is null)
                {
                    continue;
                }

                Actions.Add(new CommandCenterActionButtonViewModel(
                    configuredAction.Key,
                    definition.Title,
                    definition.Description,
                    definition.Icon,
                    settings.CommandCenterShowTitles,
                    command));
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Rebuild();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(Rebuild).GetAwaiter().GetResult();
        }
    }

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(PinButtonText));
    }

    private async Task RefreshActionStateAsync()
    {
        var isDesktopClean = await AppServices.DesktopCleanupService.IsDesktopCleanAsync().ConfigureAwait(false);
        var areBoxesVisible = AppServices.BoxWindowManager.AreWindowsVisible;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var toggleBoxes = Actions.FirstOrDefault(action => action.Key == CommandCenterActionCatalog.ToggleBoxes);
            if (toggleBoxes != null)
            {
                toggleBoxes.Title = areBoxesVisible ? "Hide Boxes" : "Show Boxes";
                toggleBoxes.Description = areBoxesVisible ? "Hide Boxes" : "Show Boxes";
            }

            var toggleDesktopIcons = Actions.FirstOrDefault(action => action.Key == CommandCenterActionCatalog.ToggleDesktopIcons);
            if (toggleDesktopIcons != null)
            {
                toggleDesktopIcons.Title = isDesktopClean ? "Restore Icons" : "Clean Desktop";
                toggleDesktopIcons.Description = isDesktopClean ? "Restore Icons" : "Clean Desktop";
            }
        });
    }

    private async Task OpenNoteHubAsync()
    {
        await AppServices.WidgetWindowManager.ShowNoteHubAsync().ConfigureAwait(false);
    }

    private async Task ToggleLockAsync()
    {
        var current = await AppServices.SettingsService.GetAsync().ConfigureAwait(false);
        current.CommandCenterLocked = !current.CommandCenterLocked;
        await AppServices.SettingsService.SaveAsync(current).ConfigureAwait(false);
    }

    private async Task ToggleBoxesAsync()
    {
        await AppServices.BoxWindowManager.ToggleAllWindowsVisibility().ConfigureAwait(false);
        DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
        await RefreshActionStateAsync().ConfigureAwait(false);
    }

    private async Task ToggleDesktopIconsAsync()
    {
        var isDesktopClean = await AppServices.DesktopCleanupService.IsDesktopCleanAsync().ConfigureAwait(false);
        if (isDesktopClean)
        {
            await AppServices.DesktopCleanupService.RestoreAsync().ConfigureAwait(false);
        }
        else
        {
            await AppServices.DesktopCleanupService.CleanAsync().ConfigureAwait(false);
        }

        await AppServices.ScannedFileService.ScanAndSaveAsync().ConfigureAwait(false);
        var boxes = await AppServices.BoxService.GetBoxesAsync().ConfigureAwait(false);
        foreach (var box in boxes)
        {
            await AppServices.BoxWindowManager.UpdateAsync(box).ConfigureAwait(false);
        }

        await RefreshActionStateAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
