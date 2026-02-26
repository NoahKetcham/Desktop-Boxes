using System;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using Boxes.App.Services;

namespace Boxes.App;

sealed class Program
{
    /// <summary>
    /// Command to run when app starts (e.g. opennotepad when launched via context menu with no existing instance).
    /// </summary>
    public static string? PendingCommand { get; set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    // #region agent log helper
    internal static void DebugLog(string location, string message, string hypothesisId, object? data = null)
    {
        try
        {
            var logPath = @"C:\Users\noahk\OneDrive\Documents\GitHub\Desktop-Boxes\debug-ce66e4.log";
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = "ce66e4",
                id = $"log_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                location,
                message,
                hypothesisId,
                data
            });
            System.IO.File.AppendAllText(logPath, json + "\n");
        }
        catch { }
    }
    // #endregion

    [STAThread]
    public static void Main(string[] args)
    {
        // #region agent log
        DebugLog("Program.cs:Main:entry", "Main() entered", "H5", new { argsLength = args.Length, args = string.Join(",", args), pid = Environment.ProcessId });
        // #endregion

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            // #region agent log
            DebugLog("Program.cs:UnhandledException", "Unhandled exception caught", "H4", new { exception = (e.ExceptionObject as Exception)?.ToString() ?? "Unknown" });
            // #endregion
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Boxes", "startup-error.txt");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.WriteAllText(logPath, $"{DateTime.Now:O}\n{(e.ExceptionObject as Exception)?.ToString() ?? "Unknown"}");
            }
            catch { /* ignore */ }
        };

        if (args.Length >= 2 && args[0] == "--boxes-command")
        {
            // #region agent log
            DebugLog("Program.cs:Main:commandMode", "Entering RunCommandMode", "H5", new { command = args[1] });
            // #endregion
            RunCommandMode(args[1], args);
            return;
        }

        // #region agent log
        DebugLog("Program.cs:Main:normalStart", "Normal startup path - calling BuildAvaloniaApp().StartWithClassicDesktopLifetime", "H5");
        // #endregion

        // Assign a distinct AppUserModelID for the main application windows
        AppUserModelService.TrySetCurrentProcessAppUserModelId("Boxes.App.Main");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RunCommandMode(string command, string[] args)
    {
        // Use a different AppUserModelID for special commands to avoid taskbar grouping
        if (string.Equals(command, "showburst", StringComparison.OrdinalIgnoreCase))
        {
            AppUserModelService.TrySetCurrentProcessAppUserModelId("Boxes.App.ShowBoxes");
        }
        else
        {
            AppUserModelService.TrySetCurrentProcessAppUserModelId("Boxes.App.Cli");
        }

        DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);

        if (DesktopIntegrationService.SendCommandAsync(command).GetAwaiter().GetResult())
        {
            return;
        }

        // For opennotepad, start full app so notepad can be shown
        if (string.Equals(command, "opennotepad", StringComparison.OrdinalIgnoreCase))
        {
            PendingCommand = "opennotepad";
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return;
        }

        BuildAvaloniaApp().SetupWithoutStarting();
        AppServices.Initialize();

        Dispatcher.UIThread.Post(async () =>
        {
            if (string.Equals(command, "hide", StringComparison.OrdinalIgnoreCase))
            {
                await AppServices.BoxWindowManager.SetWindowsVisibility(false);
            }
            else if (string.Equals(command, "show", StringComparison.OrdinalIgnoreCase))
            {
                await AppServices.BoxWindowManager.SetWindowsVisibility(true);
            }
            else if (string.Equals(command, "showburst", StringComparison.OrdinalIgnoreCase))
            {
                await AppServices.BoxWindowManager.ToggleBurstAsync(TimeSpan.FromSeconds(5));
            }
            else
            {
                await AppServices.BoxWindowManager.ToggleAllWindowsVisibility();
            }

            DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);
            Dispatcher.UIThread.ExitAllFrames();
        });

        Dispatcher.UIThread.MainLoop(CancellationToken.None);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
