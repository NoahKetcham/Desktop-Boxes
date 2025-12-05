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

public class DesktopBuildService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DesktopBuild> _builds = new();

    public DesktopBuildService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "desktop-builds.json");
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
                _builds.Clear();
                await PersistAsync().ConfigureAwait(false);
                return;
            }

            List<DesktopBuild>? data;
            string? backupPath = null;
            var hadCorruption = false;

            try
            {
                data = JsonSerializer.Deserialize<List<DesktopBuild>>(json, _serializerOptions);
            }
            catch (JsonException)
            {
                hadCorruption = true;
                var directory = Path.GetDirectoryName(_storagePath) ?? AppContext.BaseDirectory;
                backupPath = Path.Combine(directory, $"desktop-builds.invalid_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                try
                {
                    File.Copy(_storagePath, backupPath, true);
                }
                catch
                {
                    backupPath = null;
                }

                data = new List<DesktopBuild>();
            }

            _builds.Clear();

            if (data != null)
            {
                _builds.AddRange(data);
            }

            if (hadCorruption)
            {
                await PersistAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(backupPath))
                {
                    Console.WriteLine($"[Boxes] Detected invalid desktop-builds.json. Backup saved to {backupPath} and a fresh file was generated.");
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DesktopBuild>> GetAllAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return _builds.Select(Clone).OrderByDescending(b => b.CreatedAt).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopBuild?> GetAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var build = _builds.FirstOrDefault(b => b.Id == id);
            return build != null ? Clone(build) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopBuild> SaveAsync(string name, IReadOnlyList<DesktopBox> boxes)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var build = new DesktopBuild
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = DateTime.UtcNow,
                Boxes = boxes.Select(CloneBox).ToList()
            };

            _builds.Add(build);
            await PersistAsync().ConfigureAwait(false);
            return Clone(build);
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
            var index = _builds.FindIndex(b => b.Id == id);
            if (index >= 0)
            {
                _builds.RemoveAt(index);
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
            _builds.Clear();
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
        await JsonSerializer.SerializeAsync(stream, _builds, _serializerOptions).ConfigureAwait(false);
    }

    private static DesktopBuild Clone(DesktopBuild build) => new()
    {
        Id = build.Id,
        Name = build.Name,
        CreatedAt = build.CreatedAt,
        Boxes = build.Boxes.Select(CloneBox).ToList()
    };

    private static DesktopBox CloneBox(DesktopBox box) => new()
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

