using System.Text.Json.Serialization;

namespace SpeakerHop.Models;

public sealed class HotkeyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新しいショートカット";
    public HotkeyActionType Action { get; set; } = HotkeyActionType.CycleDevice;
    public string? DeviceId { get; set; }
    public uint Modifiers { get; set; } = 0x0001 | 0x0002 | 0x0004;
    public uint Key { get; set; } = 0x78;
    public int VolumeStep { get; set; } = 5;
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public string GestureText
    {
        get => FormatGesture();
    }

    public string FormatGesture(string? keyText = null)
    {
        var parts = new List<string>();
        if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((Modifiers & 0x0008) != 0) parts.Add("Win");
        parts.Add(keyText ?? KeyText);
        return string.Join(" + ", parts);
    }

    [JsonIgnore]
    public string KeyText => Key switch
    {
        >= 0x30 and <= 0x39 => ((char)Key).ToString(),
        >= 0x41 and <= 0x5A => ((char)Key).ToString(),
        >= 0x70 and <= 0x87 => $"F{Key - 0x6F}",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0C => "Clear",
        0x0D => "Enter",
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        0x13 => "Pause",
        0x14 => "Caps Lock",
        0x15 => "Kana/Hangul",
        0x19 => "Hanja/Kanji",
        0x1C => "Convert",
        0x1D => "NonConvert",
        0x1F => "Mode Change",
        0x1B => "Escape",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        0x2F => "Help",
        0x5B => "Left Windows",
        0x5C => "Right Windows",
        0x5D => "Application",
        0x5F => "Sleep",
        >= 0x60 and <= 0x69 => $"Numpad {Key - 0x60}",
        0x6A => "Numpad Multiply",
        0x6B => "Numpad Add",
        0x6C => "Numpad Separator",
        0x6D => "Numpad Subtract",
        0x6E => "Numpad Decimal",
        0x6F => "Numpad Divide",
        0x90 => "Num Lock",
        0x91 => "Scroll Lock",
        0xA0 => "Left Shift",
        0xA1 => "Right Shift",
        0xA2 => "Left Ctrl",
        0xA3 => "Right Ctrl",
        0xA4 => "Left Alt",
        0xA5 => "Right Alt",
        0xA6 => "Browser Back",
        0xA7 => "Browser Forward",
        0xA8 => "Browser Refresh",
        0xA9 => "Browser Stop",
        0xAA => "Browser Search",
        0xAB => "Browser Favorites",
        0xAC => "Browser Home",
        0xAD => "Mute",
        0xAE => "Volume Down",
        0xAF => "Volume Up",
        0xB0 => "Media Next",
        0xB1 => "Media Previous",
        0xB2 => "Media Stop",
        0xB3 => "Media Play/Pause",
        0xB4 => "Launch Mail",
        0xB5 => "Launch Media Player",
        0xB6 => "Launch App 1",
        0xB7 => "Launch App 2",
        0xBA => "OEM1",
        0xBB => "OEM Plus",
        0xBC => "OEM Comma",
        0xBD => "OEM Minus",
        0xBE => "OEM Period",
        0xBF => "OEM2",
        0xC0 => "OEM3",
        0xDB => "OEM4",
        0xDC => "OEM5",
        0xDD => "OEM6",
        0xDE => "OEM7",
        0xDF => "OEM8",
        0xE2 => "OEM102",
        _ => $"VK 0x{Key:X2}"
    };
}
