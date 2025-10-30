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

public class WindowStateService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, BoxWindowState> _states = new();

    public WindowStateService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "windows.json");
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
            var list = string.IsNullOrWhiteSpace(json) ? new List<BoxWindowState>() : JsonSerializer.Deserialize<List<BoxWindowState>>(json, _serializerOptions) ?? new List<BoxWindowState>();
            _states.Clear();
            foreach (var s in list)
            {
                _states[s.BoxId] = s;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BoxWindowState> GetAsync(Guid boxId)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_states.TryGetValue(boxId, out var s))
            {
                return Clone(s);
            }
            var created = new BoxWindowState { BoxId = boxId };
            _states[boxId] = Clone(created);
            await PersistAsync().ConfigureAwait(false);
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(BoxWindowState state)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _states[state.BoxId] = Clone(state);
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
        await JsonSerializer.SerializeAsync(stream, _states.Values.ToList(), _serializerOptions).ConfigureAwait(false);
    }

    private static BoxWindowState Clone(BoxWindowState s) => new()
    {
        BoxId = s.BoxId,
        Mode = s.Mode,
        Width = s.Width,
        Height = s.Height,
        X = s.X,
        Y = s.Y,
        IsCollapsed = s.IsCollapsed,
        ExpandedHeight = s.ExpandedHeight,
        ExpandedPosX = s.ExpandedPosX,
        ExpandedPosY = s.ExpandedPosY,
        NormalWidth = s.NormalWidth,
        NormalHeight = s.NormalHeight,
        NormalX = s.NormalX,
        NormalY = s.NormalY,
        CurrentPath = s.CurrentPath
    };
}


