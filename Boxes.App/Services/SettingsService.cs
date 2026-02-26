using System;
using System.Collections.Generic;
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
    private bool _initialized;

    public event EventHandler<ApplicationSettings>? SettingsChanged;

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
            if (_initialized) return;

            if (!File.Exists(_storagePath))
            {
                await PersistAsync().ConfigureAwait(false);
                _initialized = true;
                return;
            }

            var hadCorruption = false;
            string? backupPath = null;

            try
            {
                await using var stream = File.OpenRead(_storagePath);
                var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(stream, _serializerOptions).ConfigureAwait(false);
                if (settings != null)
                {
                    _cache = settings;
                }
            }
            catch (JsonException)
            {
                hadCorruption = true;
                var directory = Path.GetDirectoryName(_storagePath) ?? AppContext.BaseDirectory;
                backupPath = Path.Combine(directory, $"settings.invalid_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                
                try
                {
                    File.Copy(_storagePath, backupPath, true);
                    Console.WriteLine($"[SettingsService] Detected corrupted settings.json. Backup saved to {backupPath}");
                }
                catch
                {
                    backupPath = null;
                }

                // Reset to defaults on corruption
                _cache = new ApplicationSettings();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[SettingsService] Error reading settings: {ex.Message}");
                _cache = new ApplicationSettings();
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

        // Notify listeners after initialization so UI can reflect initial values
        RaiseChanged();
    }

    public async Task<ApplicationSettings> GetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
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
            var success = await PersistWithRetryAsync().ConfigureAwait(false);
            
            if (!success)
            {
                Console.WriteLine("[SettingsService] Warning: Failed to persist settings after retries");
            }
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
    }

    private async Task<bool> PersistWithRetryAsync(int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await PersistAsync().ConfigureAwait(false);
                return true;
            }
            catch (IOException ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"[SettingsService] Persist attempt {attempt} failed: {ex.Message}");
                await Task.Delay(100 * attempt).ConfigureAwait(false); // Exponential backoff
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsService] Persist failed: {ex.Message}");
                break;
            }
        }
        return false;
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
            await JsonSerializer.SerializeAsync(stream, _cache, _serializerOptions).ConfigureAwait(false);
        }

        // Atomic replace
        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
        File.Move(tempPath, _storagePath);
    }

    public async Task ResetAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache = new ApplicationSettings();
            
            // Backup existing file before deleting
            if (File.Exists(_storagePath))
            {
                var directory = Path.GetDirectoryName(_storagePath) ?? AppContext.BaseDirectory;
                var backupPath = Path.Combine(directory, $"settings.backup_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
                try
                {
                    File.Copy(_storagePath, backupPath, true);
                }
                catch
                {
                    // Ignore backup failures on reset
                }
                
                File.Delete(_storagePath);
            }

            await PersistAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RaiseChanged();
    }

    private static ApplicationSettings Clone(ApplicationSettings settings) => new()
    {
        ThemePreference = settings.ThemePreference,
        AutoSnapEnabled = settings.AutoSnapEnabled,
        ShowBoxOutlines = settings.ShowBoxOutlines,
        RunAtStartup = settings.RunAtStartup,
        OneDriveLinked = settings.OneDriveLinked,
        GoogleDriveLinked = settings.GoogleDriveLinked,
        BoxesTransparencyPercent = settings.BoxesTransparencyPercent,
        AccentHex = settings.AccentHex,
        BoxBackgroundColor = settings.BoxBackgroundColor,
        RecentAccentColors = settings.RecentAccentColors != null ? new List<string>(settings.RecentAccentColors) : new List<string>(),
        RecentBoxBackgroundColors = settings.RecentBoxBackgroundColors != null ? new List<string>(settings.RecentBoxBackgroundColors) : new List<string>(),
        // Box Customization
        BoxHeaderHeight = settings.BoxHeaderHeight,
        ShowBoxHeader = settings.ShowBoxHeader,
        BoxCornerRadius = settings.BoxCornerRadius,
        BoxIconSize = settings.BoxIconSize,
        ShowShortcutLabels = settings.ShowShortcutLabels,
        BoxContentPadding = settings.BoxContentPadding,
        BoxContentVerticalPadding = settings.BoxContentVerticalPadding,
        NoteHubDirectoryPath = settings.NoteHubDirectoryPath
    };

    private void RaiseChanged()
    {
        try
        {
            var snapshot = Clone(_cache);
            SettingsChanged?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Failed to raise settings changed: {ex.Message}");
        }
    }
}
