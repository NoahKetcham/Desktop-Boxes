using System;
using System.IO;
using System.Threading.Tasks;

namespace Boxes.App.Services;

public class NotepadService
{
    private readonly string _rootDirectory;

    public NotepadService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _rootDirectory = rootDirectory;
    }

    private string GetContentPath(Guid notepadId) =>
        Path.Combine(_rootDirectory, $"notepad-{notepadId:N}.md");

    public async Task<string> GetContentAsync(Guid notepadId)
    {
        var path = GetContentPath(notepadId);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    public async Task SaveContentAsync(Guid notepadId, string content)
    {
        var path = GetContentPath(notepadId);
        try
        {
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, content ?? string.Empty).ConfigureAwait(false);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotepadService] Failed to save: {ex.Message}");
        }
    }
}
