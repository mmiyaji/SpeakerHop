using System.Text.Json.Serialization;

namespace SpeakerHop.Models;

public sealed class HotkeyDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新しいショートカット";
    public HotkeyActionType Action { get; set; } = HotkeyActionType.CycleDevice;
    public string? DeviceId { get; set; }
    public uint Modifiers { get; set; } = 0x0002 | 0x0004;
    public uint Key { get; set; } = 0x41;
    public int VolumeStep { get; set; } = 5;
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public string GestureText
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
            if ((Modifiers & 0x0008) != 0) parts.Add("Win");
            parts.Add(KeyText);
            return string.Join(" + ", parts);
        }
    }

    [JsonIgnore]
    public string KeyText => Key switch
    {
        >= 0x30 and <= 0x5A => ((char)Key).ToString(),
        >= 0x70 and <= 0x87 => $"F{Key - 0x6F}",
        0x25 => "←",
        0x26 => "↑",
        0x27 => "→",
        0x28 => "↓",
        _ => $"VK 0x{Key:X2}"
    };
}
