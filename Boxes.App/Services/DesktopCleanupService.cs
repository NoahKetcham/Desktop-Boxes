using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Boxes.App.Services;

public class DesktopCleanupService
{
    private readonly string _statePath;
    private readonly string _archivePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    private record CleanupEntry(string OriginalPath, string StoredName, bool IsDirectory);
    private record CleanupState(List<CleanupEntry> Entries);

    public DesktopCleanupService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _statePath = Path.Combine(rootDirectory, "desktop_cleanup.json");
        _archivePath = Path.Combine(rootDirectory, "desktop_archive");
        Directory.CreateDirectory(_archivePath);
    }

    public async Task<bool> IsDesktopCleanAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_statePath))
            {
                return false;
            }

            var state = await LoadStateAsync().ConfigureAwait(false);
            return state.Entries.Count > 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<(string OriginalPath, string StoredName, bool IsDirectory, string ArchiveLocation)>> CleanAsync()
    {
        var desktopPaths = GetDesktopRoots();
        var entries = new List<(string, string, bool, string)>();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var desktopPath in desktopPaths)
            {
                if (!Directory.Exists(desktopPath))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFileSystemEntries(desktopPath))
                {
                    var name = Path.GetFileName(path);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        continue;
                    }

                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.Hidden) != 0)
                        {
                            continue;
                        }

                        var isDirectory = (attributes & FileAttributes.Directory) != 0;
                        var extension = isDirectory ? string.Empty : Path.GetExtension(path);
                        var storedName = Guid.NewGuid().ToString("N") + extension;
                        var destinationPath = Path.Combine(_archivePath, storedName);

                        if (isDirectory)
                        {
                            Directory.Move(path, destinationPath);
                        }
                        else
                        {
                            File.Move(path, destinationPath);
                        }

                        entries.Add((path, storedName, isDirectory, destinationPath));
                    }
                    catch
                    {
                        // Ignore items we cannot move.
                    }
                }
            }

            if (entries.Count > 0)
            {
                var cleanupEntries = entries.Select(e => new CleanupEntry(e.Item1, e.Item2, e.Item3)).ToList();
                await SaveStateAsync(new CleanupState(cleanupEntries)).ConfigureAwait(false);
                NotifyDesktopChanged();
            }

            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RestoreAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_statePath))
            {
                return false;
            }

            var state = await LoadStateAsync().ConfigureAwait(false);
            var restoredAny = false;

            foreach (var entry in state.Entries)
            {
                    var storedPath = Path.Combine(_archivePath, entry.StoredName);
                if (!File.Exists(storedPath) && !Directory.Exists(storedPath))
                {
                    continue;
                }

                try
                {
                    var parentDir = Path.GetDirectoryName(entry.OriginalPath);
                    if (!string.IsNullOrWhiteSpace(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    if (entry.IsDirectory)
                    {
                        Directory.Move(storedPath, entry.OriginalPath);
                    }
                    else
                    {
                        File.Move(storedPath, entry.OriginalPath, overwrite: true);
                    }

                    restoredAny = true;
                }
                catch
                {
                    // Skip items we cannot restore, leave them archived.
                }
            }

            if (restoredAny)
            {
                if (File.Exists(_statePath))
                {
                    File.Delete(_statePath);
                }
                NotifyDesktopChanged();
            }

            return restoredAny;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CleanupState> LoadStateAsync()
    {
        await using var stream = File.OpenRead(_statePath);
        var state = await JsonSerializer.DeserializeAsync<CleanupState>(stream, _serializerOptions).ConfigureAwait(false);
        return state ?? new CleanupState(new List<CleanupEntry>());
    }

    private async Task SaveStateAsync(CleanupState state)
    {
        await using var stream = File.Create(_statePath);
        await JsonSerializer.SerializeAsync(stream, state, _serializerOptions).ConfigureAwait(false);
    }

    private static IEnumerable<string> GetDesktopRoots()
    {
        var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        if (!string.IsNullOrWhiteSpace(userDesktop))
        {
            yield return userDesktop;
        }

        if (!string.IsNullOrWhiteSpace(publicDesktop) && !string.Equals(publicDesktop, userDesktop, StringComparison.OrdinalIgnoreCase))
        {
            yield return publicDesktop;
        }
    }

    private static void NotifyDesktopChanged()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        SHChangeNotify(0x8000000, 0x0005, desktopPath, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);
}
