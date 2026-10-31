using SpeakerHop.Models;

namespace SpeakerHop.Services;

/// <summary>Builds an editable shortcut draft without modifying persisted settings.</summary>
public static class ShortcutDraftService
{
    public static List<HotkeyDefinition> CreateRows(IEnumerable<HotkeyDefinition> hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);

        var rows = hotkeys.Select(Clone).OrderBy(hotkey => hotkey.Action).ToList();
        foreach (var action in Enum.GetValues<HotkeyActionType>())
        {
            if (!rows.Any(hotkey => hotkey.Action == action))
            {
                rows.Add(new HotkeyDefinition
                {
                    Action = action,
                    Name = string.Empty,
                    Key = 0,
                    Modifiers = 0,
                    Enabled = false,
                    VolumeStep = 5
                });
            }
        }

        return rows.OrderBy(hotkey => hotkey.Action).ToList();
    }

    public static List<HotkeyDefinition> ToSettings(IEnumerable<HotkeyDefinition> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .Where(hotkey => hotkey.Key is >= 1 and <= 0xFF && !IsModifierKey(hotkey.Key))
            .Select(Clone)
            .ToList();
    }

    private static HotkeyDefinition Clone(HotkeyDefinition hotkey) => new()
    {
        Id = hotkey.Id,
        Name = hotkey.Name,
        Action = hotkey.Action,
        DeviceId = hotkey.DeviceId,
        Modifiers = hotkey.Modifiers,
        Key = hotkey.Key,
        VolumeStep = hotkey.VolumeStep,
        Enabled = hotkey.Enabled
    };

    private static bool IsModifierKey(uint key) => key is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C or
        0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
}
