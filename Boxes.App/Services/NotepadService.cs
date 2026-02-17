using System;
using System.IO;
using System.Threading.Tasks;

namespace Boxes.App.Services;

public class NotepadService
{
    private readonly string _storagePath;

    public NotepadService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _storagePath = Path.Combine(rootDirectory, "notepad.md");
    }

    public async Task<string> GetContentAsync()
    {
        if (!File.Exists(_storagePath))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    public async Task SaveContentAsync(string content)
    {
        try
        {
            var tempPath = _storagePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, content ?? string.Empty).ConfigureAwait(false);
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
            File.Move(tempPath, _storagePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotepadService] Failed to save: {ex.Message}");
        }
    }
}
