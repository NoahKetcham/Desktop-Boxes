using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Models;

namespace Boxes.App.Services;

public class BoxService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DesktopBox> _boxes = new();

    public BoxService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "boxes.json");
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_storagePath))
            {
                await PersistAsync().ConfigureAwait(false);
                return;
            }

            var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _boxes.Clear();
                await PersistAsync().ConfigureAwait(false);
                return;
            }

            List<DesktopBox>? data;
            string? backupPath = null;
            var hadCorruption = false;

            try
            {
                data = JsonSerializer.Deserialize<List<DesktopBox>>(json, _serializerOptions);
            }
            catch (JsonException)
            {
                hadCorruption = true;
                var directory = Path.GetDirectoryName(_storagePath) ?? AppContext.BaseDirectory;
                backupPath = Path.Combine(directory, $"boxes.invalid_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                try
                {
                    File.Copy(_storagePath, backupPath, true);
                }
                catch
                {
                    backupPath = null;
                }

                data = new List<DesktopBox>();
            }

            _boxes.Clear();

            if (data != null)
            {
                _boxes.AddRange(data);
            }

            if (hadCorruption)
            {
                await PersistAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(backupPath))
                {
                    Console.WriteLine($"[Boxes] Detected invalid boxes.json. Backup saved to {backupPath} and a fresh file was generated.");
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DesktopBox>> GetBoxesAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return _boxes.Select(Clone).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopBox?> GetBoxAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var box = _boxes.FirstOrDefault(b => b.Id == id);
            return box != null ? Clone(box) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopBox> AddOrUpdateAsync(DesktopBox box)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var existing = _boxes.FirstOrDefault(b => b.Id == box.Id);
            if (existing == null)
            {
                _boxes.Add(Clone(box));
            }
            else
            {
                existing.Name = box.Name;
                existing.Description = box.Description;
                existing.TargetPath = box.TargetPath;
                existing.ItemCount = box.ItemCount;
                existing.ShortcutIds = new List<Guid>(box.ShortcutIds);
                existing.Width = box.Width;
                existing.Height = box.Height;
                existing.PositionX = box.PositionX;
                existing.PositionY = box.PositionY;
                existing.CurrentPath = box.CurrentPath;
                existing.IsSnappedToTaskbar = box.IsSnappedToTaskbar;
                existing.IsCollapsed = box.IsCollapsed;
                existing.ExpandedHeight = box.ExpandedHeight;
                existing.ExpandedPositionX = box.ExpandedPositionX;
                existing.ExpandedPositionY = box.ExpandedPositionY;
                existing.WasSnapExpanded = box.WasSnapExpanded;
            }

            await PersistAsync().ConfigureAwait(false);
            return Clone(box);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var index = _boxes.FindIndex(b => b.Id == id);
            if (index >= 0)
            {
                _boxes.RemoveAt(index);
                await PersistAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _boxes.Clear();
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }

            await PersistAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync()
    {
        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, _boxes, _serializerOptions).ConfigureAwait(false);
    }

    private static DesktopBox Clone(DesktopBox box) => new()
    {
        Id = box.Id,
        Name = box.Name,
        Description = box.Description,
        TargetPath = box.TargetPath,
        ItemCount = box.ItemCount,
        ShortcutIds = new List<Guid>(box.ShortcutIds),
        Width = box.Width,
        Height = box.Height,
        PositionX = box.PositionX,
        PositionY = box.PositionY,
        CurrentPath = box.CurrentPath,
        IsSnappedToTaskbar = box.IsSnappedToTaskbar,
        IsCollapsed = box.IsCollapsed,
        ExpandedHeight = box.ExpandedHeight,
        ExpandedPositionX = box.ExpandedPositionX,
        ExpandedPositionY = box.ExpandedPositionY,
        WasSnapExpanded = box.WasSnapExpanded
    };
}

