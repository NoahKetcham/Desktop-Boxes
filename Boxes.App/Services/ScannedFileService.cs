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
}
