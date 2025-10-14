using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Models;

namespace Boxes.App.Services;

public class ScannedFileService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScannedFileService(string rootDirectory)
    {
        _storagePath = Path.Combine(rootDirectory, "scanned_files.json");
    }

    public async Task<List<ScannedFile>> ScanAndSaveAsync()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var files = Directory.EnumerateFiles(desktopPath)
            .Select(p => new ScannedFile { FilePath = p, FileName = Path.GetFileName(p) })
            .ToList();

        await SaveAsync(files);
        return files;
    }

    public async Task<List<ScannedFile>> GetScannedFilesAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(_storagePath))
            {
                return new List<ScannedFile>();
            }

            await using var stream = File.OpenRead(_storagePath);
            var files = await JsonSerializer.DeserializeAsync<List<ScannedFile>>(stream, _serializerOptions);
            return files ?? new List<ScannedFile>();
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
            await using var stream = File.Create(_storagePath);
            await JsonSerializer.SerializeAsync(stream, files, _serializerOptions);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CreateShortcutsAsync(IEnumerable<ScannedFile> files, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in files)
        {
            var shortcutPath = Path.Combine(destinationDirectory, file.FileName + ".lnk");
            await Task.Run(() => CreateShortcut(file.FilePath, shortcutPath));
        }
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
