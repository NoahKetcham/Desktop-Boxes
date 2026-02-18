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
    public static WidgetWindowManager WidgetWindowManager { get; } = new();
    public static WidgetStateService WidgetStateService { get; private set; } = null!;
    public static NotepadService NotepadService { get; private set; } = null!;
    public static NotepadCatalogService NotepadCatalogService { get; private set; } = null!;
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

            // #region agent log
            Program.DebugLog("AppServices.cs:Initialize:start", "Starting service init", "H1", new { rootDirectory });
            // #endregion

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

            WidgetStateService = new WidgetStateService(rootDirectory);
            WidgetStateService.InitializeAsync().GetAwaiter().GetResult();
            NotepadService = new NotepadService(rootDirectory);
            NotepadCatalogService = new NotepadCatalogService(rootDirectory);
            // #region agent log
            Program.DebugLog("AppServices.cs:Initialize:beforeNotepadCatalog", "About to init NotepadCatalogService", "H2");
            // #endregion
            NotepadCatalogService.InitializeAsync().GetAwaiter().GetResult();
            // #region agent log
            Program.DebugLog("AppServices.cs:Initialize:afterNotepadCatalog", "NotepadCatalogService initialized", "H2");
            // #endregion

            ScannedFileService = new ScannedFileService(rootDirectory);
            DesktopCleanupService = new DesktopCleanupService();
            WindowStateService = new WindowStateService(rootDirectory);
            WindowStateService.InitializeAsync().GetAwaiter().GetResult();
            DesktopBuildService = new DesktopBuildService(rootDirectory);
            DesktopBuildService.InitializeAsync().GetAwaiter().GetResult();
            ShellIconService = new ShellIconService();
            DataMaintenanceService = new DataMaintenanceService(rootDirectory);
            _initialized = true;
            // #region agent log
            Program.DebugLog("AppServices.cs:Initialize:complete", "All services initialized", "H1");
            // #endregion
        }
    }

    public static void NotifyBoxUpdated(DesktopBox box)
    {
        BoxUpdated?.Invoke(null, box);
    }
}

