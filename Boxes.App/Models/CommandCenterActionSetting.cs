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
    public const string OpenNoteHub = "open-notehub";
    public const string ToggleBoxes = "toggle-boxes";
    public const string ToggleDesktopIcons = "toggle-desktop-icons";

    private static readonly IReadOnlyList<CommandCenterActionDefinition> DefinitionsInternal =
    [
        new(OpenNoteHub, "NoteHub", "Open your notes workspace", "[]"),
        new(ToggleBoxes, "Boxes", "Show or hide desktop boxes", "##"),
        new(ToggleDesktopIcons, "Desktop", "Clean or restore icons", "**")
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
