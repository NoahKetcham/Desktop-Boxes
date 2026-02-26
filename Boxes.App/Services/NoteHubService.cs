using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Boxes.App.Services;

public class NoteHubService
{
    private readonly string _rootDirectory;

    public NoteHubService(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        _rootDirectory = rootDirectory;
    }

    private string GetAbsolutePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return _rootDirectory;

        var normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalized))
            return _rootDirectory;

        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, normalized));
        if (!fullPath.StartsWith(_rootDirectory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes NoteHub root.", nameof(relativePath));

        return fullPath;
    }

    public async Task<string> GetContentAsync(string relativePath)
    {
        var path = GetAbsolutePath(relativePath);
        if (!File.Exists(path))
            return string.Empty;

        try
        {
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    public async Task SaveContentAsync(string relativePath, string content)
    {
        var path = GetAbsolutePath(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, content ?? string.Empty).ConfigureAwait(false);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NoteHubService] Failed to save: {ex.Message}");
        }
    }

    public record NoteHubEntry(string Name, string RelativePath, bool IsFolder);

    public Task<IReadOnlyList<NoteHubEntry>> GetEntriesAsync(string relativeDir)
    {
        var dirPath = GetAbsolutePath(relativeDir);
        if (!Directory.Exists(dirPath))
            return Task.FromResult<IReadOnlyList<NoteHubEntry>>(Array.Empty<NoteHubEntry>());

        var prefix = string.IsNullOrWhiteSpace(relativeDir)
            ? string.Empty
            : relativeDir.Replace('\\', '/').TrimEnd('/') + "/";

        var list = new List<NoteHubEntry>();

        try
        {
            foreach (var dir in Directory.GetDirectories(dirPath))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name) || name.StartsWith('.'))
                    continue;
                list.Add(new NoteHubEntry(name, prefix + name, IsFolder: true));
            }

            foreach (var file in Directory.GetFiles(dirPath, "*.md"))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrEmpty(name))
                    continue;
                list.Add(new NoteHubEntry(name, prefix + name, IsFolder: false));
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[NoteHubService] Error listing directory: {ex.Message}");
        }

        return Task.FromResult<IReadOnlyList<NoteHubEntry>>(list);
    }

    public Task CreateFolderAsync(string relativePath)
    {
        var path = GetAbsolutePath(relativePath);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string oldRelativePath, string newName)
    {
        var oldPath = GetAbsolutePath(oldRelativePath);
        if (!File.Exists(oldPath) && !Directory.Exists(oldPath))
            return Task.CompletedTask;

        var parts = oldRelativePath.Replace('\\', '/').Split('/');
        var dir = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : string.Empty;
        var prefix = string.IsNullOrEmpty(dir) ? "" : dir.TrimEnd('/') + "/";
        var newRelativePath = prefix + newName.Trim();

        var newPath = GetAbsolutePath(newRelativePath);
        if (File.Exists(newPath) || Directory.Exists(newPath))
            return Task.CompletedTask;

        if (File.Exists(oldPath))
            File.Move(oldPath, newPath);
        else if (Directory.Exists(oldPath))
            Directory.Move(oldPath, newPath);

        return Task.CompletedTask;
    }
}
