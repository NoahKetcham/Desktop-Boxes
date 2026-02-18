using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Models;

namespace Boxes.App.Services;

public class WidgetStateService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, WidgetStateData> _cache = new();
    private bool _initialized;

    public WidgetStateService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "widget-states.json");
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (File.Exists(_storagePath))
            {
                try
                {
                    await using var stream = File.OpenRead(_storagePath);
                    var data = await JsonSerializer.DeserializeAsync<Dictionary<string, WidgetStateData>>(stream, _serializerOptions).ConfigureAwait(false);
                    if (data != null)
                    {
                        _cache = data;
                    }
                }
                catch (JsonException)
                {
                    _cache = new Dictionary<string, WidgetStateData>();
                }
                catch (IOException)
                {
                    _cache = new Dictionary<string, WidgetStateData>();
                }
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WidgetStateData?> GetAsync(string widgetId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            return _cache.TryGetValue(widgetId, out var state) ? state : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MigrateLegacyNotepadStateAsync(Guid notepadId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            var previewKey = $"notepadPreview-{notepadId:N}";
            var editorKey = $"notepadEditor-{notepadId:N}";
            if (!_cache.ContainsKey(previewKey) && _cache.TryGetValue("notepadPreview", out var previewState))
            {
                _cache[previewKey] = previewState;
            }
            if (!_cache.ContainsKey(editorKey) && _cache.TryGetValue("notepadEditor", out var editorState))
            {
                _cache[editorKey] = editorState;
            }
            if (!_cache.ContainsKey(previewKey) && _cache.TryGetValue("notepad", out var legacyState))
            {
                _cache[previewKey] = legacyState;
            }
            await PersistAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(string widgetId, WidgetStateData state)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            _cache[widgetId] = state;
            await PersistAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistAsync()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _storagePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, _cache, _serializerOptions).ConfigureAwait(false);
        }

        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
        File.Move(tempPath, _storagePath);
    }
}
