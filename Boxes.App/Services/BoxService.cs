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
    private bool _initialized;

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
            if (_initialized) return;

            if (!File.Exists(_storagePath))
            {
                await PersistAsync().ConfigureAwait(false);
                _initialized = true;
                return;
            }

            var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                _boxes.Clear();
                await PersistAsync().ConfigureAwait(false);
                _initialized = true;
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
                    Console.WriteLine($"[BoxService] Detected invalid boxes.json. Backup saved to {backupPath}");
                }
                catch
                {
                    backupPath = null;
                }

                data = new List<DesktopBox>();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[BoxService] Error reading boxes: {ex.Message}");
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
            }

            _initialized = true;
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
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
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
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
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
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            var existing = _boxes.FirstOrDefault(b => b.Id == box.Id);
            if (existing == null)
            {
                _boxes.Add(Clone(box));
            }
            else
            {
                existing.Name = box.Name;
                existing.Description = box.Description;
                existing.TemplateKey = box.TemplateKey;
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
                existing.ItemOrder = new Dictionary<Guid, List<Guid>>(box.ItemOrder.ToDictionary(kvp => kvp.Key, kvp => new List<Guid>(kvp.Value)));
            }

            await PersistWithRetryAsync().ConfigureAwait(false);
            return Clone(box);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistWithRetryAsync(int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await PersistAsync().ConfigureAwait(false);
                return;
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"[BoxService] Persist attempt {attempt} failed: {ex.Message}");
                await Task.Delay(100 * attempt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BoxService] Persist failed: {ex.Message}");
                break;
            }
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            var index = _boxes.FindIndex(b => b.Id == id);
            if (index >= 0)
            {
                _boxes.RemoveAt(index);
                await PersistWithRetryAsync().ConfigureAwait(false);
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
            // Backup existing file before resetting
            if (File.Exists(_storagePath))
            {
                var directory = Path.GetDirectoryName(_storagePath) ?? AppContext.BaseDirectory;
                var backupPath = Path.Combine(directory, $"boxes.backup_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                try
                {
                    File.Copy(_storagePath, backupPath, true);
                }
                catch
                {
                    // Ignore backup failures
                }
            }

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
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to temp file first, then atomic rename
        var tempPath = _storagePath + ".tmp";
        
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, _boxes, _serializerOptions).ConfigureAwait(false);
        }

        // Atomic replace
        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
        File.Move(tempPath, _storagePath);
    }

    private static DesktopBox Clone(DesktopBox box) => new()
    {
        Id = box.Id,
        Name = box.Name,
        Description = box.Description,
        TemplateKey = box.TemplateKey,
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
        WasSnapExpanded = box.WasSnapExpanded,
        ItemOrder = new Dictionary<Guid, List<Guid>>(box.ItemOrder.ToDictionary(kvp => kvp.Key, kvp => new List<Guid>(kvp.Value)))
    };
}
