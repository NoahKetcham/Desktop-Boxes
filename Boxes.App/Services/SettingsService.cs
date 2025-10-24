using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Models;

namespace Boxes.App.Services;

public class SettingsService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ApplicationSettings _cache = new();

    public SettingsService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "settings.json");
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
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, _serializerOptions).ConfigureAwait(false);
            if (settings != null)
            {
                _cache = settings;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ApplicationSettings> GetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return Clone(_cache);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ApplicationSettings settings)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache = Clone(settings);
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
        await JsonSerializer.SerializeAsync(stream, _cache, _serializerOptions).ConfigureAwait(false);
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache = new ApplicationSettings();
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

    private static ApplicationSettings Clone(ApplicationSettings settings) => new()
    {
        ThemePreference = settings.ThemePreference,
        AutoSnapEnabled = settings.AutoSnapEnabled,
        ShowBoxOutlines = settings.ShowBoxOutlines,
        OneDriveLinked = settings.OneDriveLinked,
        GoogleDriveLinked = settings.GoogleDriveLinked
    };
}

