using System.Collections.Generic;
using System.Linq;

namespace Boxes.App.Models;

public class CommandCenterActionSetting
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed record CommandCenterActionDefinition(string Key, string Title, string Description, string Icon);

public static class CommandCenterActionCatalog
{
    public const string ToggleLock = "toggle-lock";
    public const string OpenSettings = "open-settings";
    public const string OpenNoteHub = "open-notehub";
    public const string ToggleBoxes = "toggle-boxes";
    public const string ToggleDesktopIcons = "toggle-desktop-icons";
    public const string Placeholder1 = "placeholder-1";
    public const string Placeholder2 = "placeholder-2";
    public const string Placeholder3 = "placeholder-3";
    public const string Placeholder4 = "placeholder-4";

    private static readonly IReadOnlyList<CommandCenterActionDefinition> DefinitionsInternal =
    [
        new(ToggleLock, "Lock", "Lock window position and size", "📌"),
        new(OpenSettings, "Settings", "Open the main settings page", "⚙"),
        new(OpenNoteHub, "NoteHub", "Open your notes workspace", "📝"),
        new(ToggleBoxes, "Boxes", "Show or hide desktop boxes", "⌂"),
        new(ToggleDesktopIcons, "Desktop", "Clean or restore icons", "✦"),
        new(Placeholder1, "Slot 1", "Placeholder action", "•"),
        new(Placeholder2, "Slot 2", "Placeholder action", "•"),
        new(Placeholder3, "Slot 3", "Placeholder action", "•"),
        new(Placeholder4, "Slot 4", "Placeholder action", "•")
    ];

    public static IReadOnlyList<CommandCenterActionDefinition> Definitions => DefinitionsInternal;

    public static List<CommandCenterActionSetting> CreateDefaultSettings()
    {
        return DefinitionsInternal
            .Select(definition => new CommandCenterActionSetting
            {
                Key = definition.Key,
                IsEnabled = true
            })
            .ToList();
    }

    public static List<CommandCenterActionSetting> Normalize(IEnumerable<CommandCenterActionSetting>? settings)
    {
        var incoming = settings?
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First())
            ?? new Dictionary<string, CommandCenterActionSetting>();

        var normalized = new List<CommandCenterActionSetting>();
        foreach (var definition in DefinitionsInternal)
        {
            if (incoming.TryGetValue(definition.Key, out var existing))
            {
                normalized.Add(new CommandCenterActionSetting
                {
                    Key = definition.Key,
                    IsEnabled = existing.IsEnabled
                });
            }
            else
            {
                normalized.Add(new CommandCenterActionSetting
                {
                    Key = definition.Key,
                    IsEnabled = true
                });
            }
        }

        return normalized;
    }

    public static CommandCenterActionDefinition GetDefinition(string key)
    {
        return DefinitionsInternal.FirstOrDefault(definition => definition.Key == key)
            ?? new CommandCenterActionDefinition(key, key, string.Empty, "??");
    }
}
