using System.Runtime.InteropServices;
using System.Windows.Forms;
using SpeakerHop.Models;

namespace SpeakerHop.Services;

public sealed class HotkeyService : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private readonly AudioCommandService _commands;
    private readonly SettingsService _settings;
    private readonly Dictionary<int, HotkeyDefinition> _registered = [];
    private int _nextId = 100;

    public HotkeyService(AudioCommandService commands, SettingsService settings)
    {
        _commands = commands;
        _settings = settings;
        CreateHandle(new CreateParams { Caption = "SpeakerHopHotkeys" });
    }

    public IReadOnlyList<HotkeyRegistrationFailure> RegisterConfiguredHotkeys()
    {
        UnregisterAll();
        return RegisterAll(Clone(_settings.Current.Hotkeys));
    }

    public IReadOnlyList<HotkeyRegistrationFailure> SaveAndReload(IEnumerable<HotkeyDefinition> hotkeys)
    {
        var previousRegistered = _registered.Values.Select(Clone).ToList();
        var previousSettings = _settings.Current.Hotkeys.Select(Clone).ToList();
        var proposed = hotkeys.Select(Clone).ToList();

        UnregisterAll();
        var failures = RegisterAll(proposed);
        if (failures.Count != 0)
        {
            UnregisterAll();
            RegisterAll(previousRegistered);
            return failures;
        }

        try
        {
            _settings.Current.Hotkeys = proposed.Select(Clone).ToList();
            _settings.Save();
        }
        catch
        {
            _settings.Current.Hotkeys = previousSettings;
            UnregisterAll();
            RegisterAll(previousRegistered);
            throw;
        }

        return failures;
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && _registered.TryGetValue(m.WParam.ToInt32(), out var hotkey))
        {
            _commands.Apply(hotkey);
            return;
        }

        base.WndProc(ref m);
    }

    private bool Register(HotkeyDefinition hotkey, out int errorCode)
    {
        var id = _nextId++;
        if (RegisterHotKey(Handle, id, hotkey.Modifiers | ModNoRepeat, hotkey.Key))
        {
            _registered[id] = Clone(hotkey);
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return false;
    }

    private List<HotkeyRegistrationFailure> RegisterAll(IEnumerable<HotkeyDefinition> hotkeys)
    {
        _nextId = 100;
        var failures = new List<HotkeyRegistrationFailure>();
        var gestures = new HashSet<(uint Modifiers, uint Key)>();
        foreach (var hotkey in hotkeys.Where(h => h.Enabled))
        {
            if (!gestures.Add((hotkey.Modifiers, hotkey.Key)))
            {
                failures.Add(new HotkeyRegistrationFailure(Clone(hotkey), ErrorHotkeyAlreadyRegistered));
                continue;
            }

            if (!Register(hotkey, out var errorCode))
            {
                failures.Add(new HotkeyRegistrationFailure(Clone(hotkey), errorCode));
            }
        }

        return failures;
    }

    private static IEnumerable<HotkeyDefinition> Clone(IEnumerable<HotkeyDefinition> hotkeys) =>
        hotkeys.Select(Clone);

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

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys.ToList())
        {
            UnregisterHotKey(Handle, id);
        }

        _registered.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed record HotkeyRegistrationFailure(HotkeyDefinition Hotkey, int ErrorCode);
