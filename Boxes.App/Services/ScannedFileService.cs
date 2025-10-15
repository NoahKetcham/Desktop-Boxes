using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Models;

namespace Boxes.App.Services;

public class ScannedFileService
{
    private readonly string _storagePath;
    private readonly string _shortcutArchivePath;
    private readonly string _shortcutManifestPath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public record StoredShortcut(Guid Id, string FileName, string TargetPath);

    public ScannedFileService(string rootDirectory)
    {
        _storagePath = Path.Combine(rootDirectory, "scanned_files.json");
        _shortcutArchivePath = Path.Combine(rootDirectory, "shortcuts");
        _shortcutManifestPath = Path.Combine(rootDirectory, "shortcuts.json");
        Directory.CreateDirectory(_shortcutArchivePath);
    }

    public async Task<List<ScannedFile>> ScanAndSaveAsync()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var currentFiles = Directory.EnumerateFiles(desktopPath)
            .Select(p => new { Path = p, Name = Path.GetFileName(p) })
            .ToList();

        await _gate.WaitAsync();
        try
        {
            var existing = await LoadScannedFilesAsync();
            var existingByPath = new Dictionary<string, ScannedFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in existing)
            {
                existingByPath[entry.FilePath] = entry;
            }
            var result = new List<ScannedFile>();

            foreach (var file in currentFiles)
            {
                if (existingByPath.TryGetValue(file.Path, out var existingEntry))
                {
                    existingEntry.FileName = file.Name;
                    existingEntry.IsArchived = false;
                    existingEntry.ArchivedContentPath = null;
                    result.Add(existingEntry);
                    existingByPath.Remove(file.Path);
                }
                else
                {
                    result.Add(new ScannedFile
                    {
                        FilePath = file.Path,
                        FileName = file.Name
                    });
                }
            }

            foreach (var remaining in existingByPath.Values)
            {
                result.Add(remaining);
            }

            await SaveScannedFilesAsync(result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<ScannedFile>> GetScannedFilesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await LoadScannedFilesAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(List<ScannedFile> files)
    {
        await _gate.WaitAsync();
        try
        {
            await SaveScannedFilesAsync(files);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string ShortcutArchiveDirectory => _shortcutArchivePath;

    public async Task<IReadOnlyList<StoredShortcut>> GetStoredShortcutsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await LoadShortcutManifestAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StoredShortcut?> GetStoredShortcutAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            var manifest = await LoadShortcutManifestAsync();
            return manifest.FirstOrDefault(s => s.Id == id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CreateShortcutsAsync(IEnumerable<ScannedFile> files)
    {
        await _gate.WaitAsync();
        try
        {
            var manifest = new List<StoredShortcut>(await LoadShortcutManifestAsync());
            var scannedFiles = await LoadScannedFilesAsync();
            var scannedLookup = scannedFiles.ToDictionary(f => f.Id, f => f);

            foreach (var file in files)
            {
                var shortcutId = file.Id != Guid.Empty ? file.Id : Guid.NewGuid();
                var shortcutName = Path.GetFileNameWithoutExtension(file.FileName) + ".lnk";
                var archiveName = shortcutId.ToString("N") + ".lnk";
                var archivePath = Path.Combine(_shortcutArchivePath, archiveName);

                await CreateShortcutFileAsync(file.FilePath, archivePath);

                manifest.RemoveAll(s => s.Id == shortcutId);
                manifest.Add(new StoredShortcut(shortcutId, shortcutName, file.FilePath));

                if (scannedLookup.TryGetValue(shortcutId, out var existing))
                {
                    existing.ShortcutPath = archivePath;
                    existing.IsArchived = file.IsArchived;
                }
                else
                {
                    scannedFiles.Add(new ScannedFile
                    {
                        Id = shortcutId,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        ShortcutPath = archivePath,
                        IsArchived = file.IsArchived
                    });
                }
            }

            await SaveShortcutManifestAsync(manifest);
            await SaveScannedFilesAsync(scannedFiles);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkFilesAsArchivedAsync(IEnumerable<(string OriginalPath, string ArchiveLocation)> archivedItems)
    {
        var pathLookup = archivedItems.ToDictionary(item => item.OriginalPath, item => item.ArchiveLocation, StringComparer.OrdinalIgnoreCase);

        await _gate.WaitAsync();
        try
        {
            var files = await LoadScannedFilesAsync();
            foreach (var file in files)
            {
                if (pathLookup.TryGetValue(file.FilePath, out var archiveLocation))
                {
                    file.IsArchived = true;
                    file.ArchivedContentPath = archiveLocation;
                }
            }

            await SaveScannedFilesAsync(files);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkFilesAsRestoredAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var files = await LoadScannedFilesAsync();
            foreach (var file in files)
            {
                file.IsArchived = false;
                file.ArchivedContentPath = null;
            }

            await SaveScannedFilesAsync(files);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetShortcutArchivePathAsync(Guid id)
    {
        await _gate.WaitAsync();
        try
        {
            var manifest = await LoadShortcutManifestAsync();
            var entry = manifest.FirstOrDefault(s => s.Id == id);
            if (entry == null)
            {
                return null;
            }

            var archiveName = id.ToString("N") + ".lnk";
            var archivePath = Path.Combine(_shortcutArchivePath, archiveName);
            return File.Exists(archivePath) ? archivePath : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ScannedFile>> LoadScannedFilesAsync()
    {
        if (!File.Exists(_storagePath))
        {
            return new List<ScannedFile>();
        }

        await using var stream = File.OpenRead(_storagePath);
        var files = await JsonSerializer.DeserializeAsync<List<ScannedFile>>(stream, _serializerOptions);
        return files ?? new List<ScannedFile>();
    }

    private async Task SaveScannedFilesAsync(List<ScannedFile> files)
    {
        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, files, _serializerOptions);
    }

    private async Task<IReadOnlyList<StoredShortcut>> LoadShortcutManifestAsync()
    {
        if (!File.Exists(_shortcutManifestPath))
        {
            return Array.Empty<StoredShortcut>();
        }

        await using var stream = File.OpenRead(_shortcutManifestPath);
        var shortcuts = await JsonSerializer.DeserializeAsync<List<StoredShortcut>>(stream, _serializerOptions);
        return (IReadOnlyList<StoredShortcut>?)shortcuts ?? Array.Empty<StoredShortcut>();
    }

    private async Task SaveShortcutManifestAsync(List<StoredShortcut> manifest)
    {
        await using var stream = File.Create(_shortcutManifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, _serializerOptions);
    }

    private static async Task CreateShortcutFileAsync(string targetPath, string shortcutPath)
    {
        await Task.Run(() => CreateShortcut(targetPath, shortcutPath));
    }

    private static void CreateShortcut(string targetPath, string shortcutPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return;
        }

        dynamic? shell = Activator.CreateInstance(shellType);
        if (shell is null)
        {
            return;
        }

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Save();
    }
}
