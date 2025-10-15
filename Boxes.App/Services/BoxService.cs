using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        WriteIndented = true
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

            await using var stream = File.OpenRead(_storagePath);
            var data = await JsonSerializer.DeserializeAsync<List<DesktopBox>>(stream, _serializerOptions).ConfigureAwait(false);
            _boxes.Clear();

            if (data != null)
            {
                _boxes.AddRange(data);
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
        ShortcutIds = new List<Guid>(box.ShortcutIds)
    };
}

