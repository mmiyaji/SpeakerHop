namespace SpeakerHop.Models;

public sealed class AppSettings
{
    public bool InitialLaunchCompleted { get; set; }
    public bool ShowMainWindowOnStartup { get; set; }
    public bool StartWithWindows { get; set; }
    public string Language { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public List<string> CycleDeviceIds { get; set; } = [];
    public List<RememberedAudioDevice> RememberedAudioDevices { get; set; } = [];
    public bool NotifyOnDeviceSwitch { get; set; } = true;
    public List<HotkeyDefinition> Hotkeys { get; set; } =
    [
        new()
        {
            Name = "出力デバイスを順番に切り替え",
            Action = HotkeyActionType.CycleDevice,
            Modifiers = 0x0002 | 0x0004,
            Key = 0x41
        },
        new()
        {
            Name = "音量を上げる",
            Action = HotkeyActionType.VolumeUp,
            Modifiers = 0x0002 | 0x0004,
            Key = 0x26,
            VolumeStep = 5
        },
        new()
        {
            Name = "音量を下げる",
            Action = HotkeyActionType.VolumeDown,
            Modifiers = 0x0002 | 0x0004,
            Key = 0x28,
            VolumeStep = 5
        }
    ];
}
