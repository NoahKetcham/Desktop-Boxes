using System;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using Boxes.App.Services;

namespace Boxes.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--boxes-command")
        {
            RunCommandMode(args[1]);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RunCommandMode(string command)
    {
        DesktopIntegrationService.EnsureContextMenuRegistered(AppServices.BoxWindowManager.AreWindowsVisible);

        if (DesktopIntegrationService.SendCommandAsync(command).GetAwaiter().GetResult())
        {
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
