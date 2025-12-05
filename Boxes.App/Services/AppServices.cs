using System;
using System.IO;
using Avalonia.Controls;
using Boxes.App.Models;

namespace Boxes.App.Services;

public static class AppServices
{
    private static bool _initialized;
    private static readonly object SyncRoot = new();

    public static BoxService BoxService { get; private set; } = null!;
    public static SettingsService SettingsService { get; private set; } = null!;
    public static BoxWindowManager BoxWindowManager { get; } = new();
    public static ScannedFileService ScannedFileService { get; private set; } = null!;
    public static DesktopCleanupService DesktopCleanupService { get; private set; } = null!;
    public static ShellIconService ShellIconService { get; private set; } = null!;
    public static DataMaintenanceService DataMaintenanceService { get; private set; } = null!;
    public static WindowStateService WindowStateService { get; private set; } = null!;
    public static DesktopBuildService DesktopBuildService { get; private set; } = null!;
    public static Window? MainWindowOwner { get; set; }

    public static event EventHandler<DesktopBox>? BoxUpdated;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var rootDirectory = Path.Combine(appData, "Boxes");
            Directory.CreateDirectory(rootDirectory);

            SettingsService = new SettingsService(rootDirectory);
            SettingsService.InitializeAsync().GetAwaiter().GetResult();
            // Apply persisted accent if present
            try
            {
                var settings = SettingsService.GetAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(settings.AccentHex))
                {
                    AccentService.TryApplyAccentHex(settings.AccentHex);
                }

                // Ensure run-at-startup shortcut matches persisted setting
                if (settings.RunAtStartup)
                {
                    _ = DesktopIntegrationService.EnableRunAtStartup();
                }
                else
                {
                    DesktopIntegrationService.DisableRunAtStartup();
                }
            }
            catch
            {
                // ignore accent application failures on startup
            }

            BoxService = new BoxService(rootDirectory);
            BoxService.InitializeAsync().GetAwaiter().GetResult();

            ScannedFileService = new ScannedFileService(rootDirectory);
            DesktopCleanupService = new DesktopCleanupService(rootDirectory);
            WindowStateService = new WindowStateService(rootDirectory);
            WindowStateService.InitializeAsync().GetAwaiter().GetResult();
            DesktopBuildService = new DesktopBuildService(rootDirectory);
            DesktopBuildService.InitializeAsync().GetAwaiter().GetResult();
            ShellIconService = new ShellIconService();
            DataMaintenanceService = new DataMaintenanceService(rootDirectory);
            _initialized = true;
        }
    }

    public static void NotifyBoxUpdated(DesktopBox box)
    {
        BoxUpdated?.Invoke(null, box);
    }
}

