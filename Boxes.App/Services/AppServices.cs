using System;
using System.IO;

namespace Boxes.App.Services;

public static class AppServices
{
    private static bool _initialized;
    private static readonly object SyncRoot = new();

    public static BoxService BoxService { get; private set; } = null!;
    public static SettingsService SettingsService { get; private set; } = null!;
    public static BoxWindowManager BoxWindowManager { get; } = new();
    public static ScannedFileService ScannedFileService { get; private set; } = null!;

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

            BoxService = new BoxService(rootDirectory);
            BoxService.InitializeAsync().GetAwaiter().GetResult();
            
            ScannedFileService = new ScannedFileService(rootDirectory);

            _initialized = true;
        }
    }
}

