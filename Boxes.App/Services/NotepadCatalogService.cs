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

public class NotepadCatalogService
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<Notepad> _notepads = new();
    private bool _initialized;

    public NotepadCatalogService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "notepads.json");
    }

    public async Task InitializeAsync()
    {
        // #region agent log
        Program.DebugLog("NotepadCatalog:InitializeAsync:entry", "InitializeAsync entered", "H2a");
        // #endregion
        await _gate.WaitAsync().ConfigureAwait(false);
        // #region agent log
        Program.DebugLog("NotepadCatalog:InitializeAsync:gateAcquired", "Semaphore acquired", "H2a");
        // #endregion
        try
        {
            if (_initialized) return;

            // #region agent log
            var fileExists = File.Exists(_storagePath);
            Program.DebugLog("NotepadCatalog:InitializeAsync:fileCheck", "File check", "H2b", new { storagePath = _storagePath, fileExists });
            // #endregion

            if (!fileExists)
            {
                var directory = Path.GetDirectoryName(_storagePath) ?? string.Empty;
                var legacyPath = Path.Combine(directory, "notepad.md");
                var legacyExists = File.Exists(legacyPath);
                // #region agent log
                Program.DebugLog("NotepadCatalog:InitializeAsync:noFile", "notepads.json missing, checking legacy", "H2b", new { legacyPath, legacyExists });
                // #endregion
                if (legacyExists)
                {
                    var migrated = new Notepad { Id = Guid.NewGuid(), Name = "Notepad" };
                    _notepads.Add(migrated);
                    var newPath = Path.Combine(directory, $"notepad-{migrated.Id:N}.md");
                    try
                    {
                        File.Copy(legacyPath, newPath);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"[NotepadCatalogService] Migration copy failed: {ex.Message}");
                    }
                }
                // #region agent log
                Program.DebugLog("NotepadCatalog:InitializeAsync:beforePersist", "About to PersistAsync (no file path)", "H2c");
                // #endregion
                await PersistAsync().ConfigureAwait(false);
                // #region agent log
                Program.DebugLog("NotepadCatalog:InitializeAsync:afterPersist", "PersistAsync completed", "H2c");
                // #endregion
                _initialized = true;
                return;
            }

            // #region agent log
            Program.DebugLog("NotepadCatalog:InitializeAsync:beforeReadFile", "About to ReadAllTextAsync", "H2d");
            // #endregion
            var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            // #region agent log
            Program.DebugLog("NotepadCatalog:InitializeAsync:afterReadFile", "ReadAllTextAsync completed", "H2d", new { jsonLength = json?.Length ?? -1 });
            // #endregion
            if (string.IsNullOrWhiteSpace(json))
            {
                _notepads.Clear();
                await PersistAsync().ConfigureAwait(false);
                _initialized = true;
                return;
            }

            List<Notepad>? data;
            try
            {
                data = JsonSerializer.Deserialize<List<Notepad>>(json, _serializerOptions);
            }
            catch (JsonException)
            {
                data = new List<Notepad>();
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[NotepadCatalogService] Error reading notepads: {ex.Message}");
                data = new List<Notepad>();
            }

            _notepads.Clear();
            if (data != null)
            {
                _notepads.AddRange(data);
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Notepad>> GetNotepadsAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            return _notepads.Select(Clone).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Notepad?> GetNotepadAsync(Guid id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }
            var notepad = _notepads.FirstOrDefault(n => n.Id == id);
            return notepad != null ? Clone(notepad) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Notepad> AddOrUpdateAsync(Notepad notepad)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            var existing = _notepads.FirstOrDefault(n => n.Id == notepad.Id);
            if (existing == null)
            {
                _notepads.Add(Clone(notepad));
            }
            else
            {
                existing.Name = notepad.Name;
            }

            await PersistWithRetryAsync().ConfigureAwait(false);
            return Clone(notepad);
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
            if (!_initialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            var index = _notepads.FindIndex(n => n.Id == id);
            if (index >= 0)
            {
                _notepads.RemoveAt(index);
                await PersistWithRetryAsync().ConfigureAwait(false);
            }
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
                Console.WriteLine($"[NotepadCatalogService] Persist attempt {attempt} failed: {ex.Message}");
                await Task.Delay(100 * attempt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotepadCatalogService] Persist failed: {ex.Message}");
                break;
            }
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
            await JsonSerializer.SerializeAsync(stream, _notepads, _serializerOptions).ConfigureAwait(false);
        }

        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
        File.Move(tempPath, _storagePath);
    }

    private static Notepad Clone(Notepad n) => new()
    {
        Id = n.Id,
        Name = n.Name
    };
}
