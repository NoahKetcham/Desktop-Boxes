using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Boxes.App.Services;

public static class DesktopIntegrationService
{
    private const string ContextMenuBaseKey = "Software\\Classes\\DesktopBackground\\shell\\ConfigureBoxes";
    private const string PipeName = "Boxes.App.DesktopIntegration";

    public static void EnsureContextMenuRegistered(bool areBoxesVisible)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            using var baseKey = Registry.CurrentUser.CreateSubKey(ContextMenuBaseKey);
            if (baseKey is null)
            {
                return;
            }

            baseKey.SetValue("MUIVerb", "Configure Boxes", RegistryValueKind.String);
            baseKey.SetValue("Icon", exePath, RegistryValueKind.String);
            baseKey.SetValue("SubCommands", string.Empty, RegistryValueKind.String);
            baseKey.DeleteSubKeyTree("command", throwOnMissingSubKey: false);
            baseKey.DeleteSubKeyTree("shell", throwOnMissingSubKey: false);

            using var shellKey = baseKey.CreateSubKey("shell");
            if (shellKey is null)
            {
                return;
            }

            var label = areBoxesVisible ? "Hide Boxes" : "Show Boxes";
            using var toggleKey = shellKey.CreateSubKey("ToggleBoxes");
            if (toggleKey is null)
            {
                return;
            }

            toggleKey.SetValue("MUIVerb", label, RegistryValueKind.String);
            toggleKey.SetValue("Icon", exePath, RegistryValueKind.String);

            using var commandKey = toggleKey.CreateSubKey("command");
            if (commandKey is null)
            {
                return;
            }

            var command = $"\"{exePath}\" --boxes-command toggle";
            commandKey.SetValue(null, command, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DesktopIntegrationService] Failed to register context menu: {ex}");
        }
    }

    public static void StartCommandListener(Func<string, Task> handler, CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        await handler(command).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DesktopIntegrationService] Command listener error: {ex}");
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    public static async Task<bool> SendCommandAsync(string command, int timeoutMilliseconds = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            var connectCts = new CancellationTokenSource(timeoutMilliseconds);
            await client.ConnectAsync(timeoutMilliseconds, connectCts.Token).ConfigureAwait(false);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(command).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DesktopIntegrationService] Failed to send command '{command}': {ex.Message}");
            return false;
        }
    }
}
