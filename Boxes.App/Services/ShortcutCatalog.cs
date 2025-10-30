using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Boxes.App.Models;
using Boxes.App.Services;

namespace Boxes.App.Services;

public static class ShortcutCatalog
{
    public static IReadOnlyList<ScannedFile> GetBoxShortcuts(DesktopBox box, IReadOnlyList<ScannedFile> allFiles, IReadOnlyList<ScannedFileService.StoredShortcut> storedShortcuts)
    {
        var archiveDirectory = AppServices.ScannedFileService.ShortcutArchiveDirectory;
        var storedLookup = storedShortcuts.ToDictionary(s => s.Id, s => s);
        var allLookup = allFiles.ToDictionary(f => f.Id, f => f);
        var selectedIds = new HashSet<Guid>(box.ShortcutIds);

        var result = new List<ScannedFile>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in selectedIds)
        {
            if (!allLookup.TryGetValue(id, out var source))
            {
                continue;
            }

            var clone = Clone(source);
            ApplyStoredMetadata(clone, storedLookup, archiveDirectory);
            clone.ParentId = null;

            if (!dedupe.Add(NormalizeKey(clone)))
            {
                continue;
            }

            result.Add(clone);
            AppendDescendants(clone, selectedIds, allLookup, storedLookup, archiveDirectory, dedupe, result);
        }

        return result;
    }

    public static IReadOnlyList<ScannedFile> GetAllShortcutsDeduped(IEnumerable<ScannedFile> files)
    {
        var result = new List<ScannedFile>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var clone = Clone(file);
            if (dedupe.Add(NormalizeKey(clone)))
            {
                result.Add(clone);
            }
        }

        return result;
    }

    private static void AppendDescendants(ScannedFile parent, HashSet<Guid> selectedIds, Dictionary<Guid, ScannedFile> allLookup,
        Dictionary<Guid, ScannedFileService.StoredShortcut> storedLookup, string archiveDirectory,
        HashSet<string> dedupe, List<ScannedFile> result)
    {
        foreach (var child in allLookup.Values.Where(f => f.ParentId == parent.Id))
        {
            if (!selectedIds.Contains(child.Id))
            {
                continue;
            }

            var clone = Clone(child);
            ApplyStoredMetadata(clone, storedLookup, archiveDirectory);
            clone.ParentId = parent.Id;

            if (!dedupe.Add(NormalizeKey(clone)))
            {
                continue;
            }

            result.Add(clone);
            AppendDescendants(clone, selectedIds, allLookup, storedLookup, archiveDirectory, dedupe, result);
        }
    }

    private static void ApplyStoredMetadata(ScannedFile file, Dictionary<Guid, ScannedFileService.StoredShortcut> storedLookup, string archiveDirectory)
    {
        if (!storedLookup.TryGetValue(file.Id, out var stored))
        {
            return;
        }

        file.ShortcutPath = System.IO.Path.Combine(archiveDirectory, stored.Id.ToString("N") + ".lnk");
        file.ItemType = stored.ItemType;
        file.ParentId = stored.ParentId;
    }

    private static ScannedFile Clone(ScannedFile file) => new()
    {
        Id = file.Id,
        FilePath = file.FilePath,
        FileName = file.FileName,
        ItemType = file.ItemType,
        ParentId = file.ParentId,
        ShortcutPath = file.ShortcutPath,
        IsArchived = file.IsArchived,
        ArchivedContentPath = file.ArchivedContentPath,
        RootId = file.RootId
    };

    private static string NormalizeKey(ScannedFile file)
    {
        var primary = !string.IsNullOrWhiteSpace(file.FilePath) ? file.FilePath : file.ShortcutPath;
        if (string.IsNullOrWhiteSpace(primary))
        {
            return string.Empty;
        }

        try
        {
            return System.IO.Path.GetFullPath(primary).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).ToUpperInvariant();
        }
        catch
        {
            return primary.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).ToUpperInvariant();
        }
    }
}
