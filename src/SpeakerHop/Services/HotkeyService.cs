using System.Runtime.InteropServices;
using System.Windows.Forms;
using SpeakerHop.Models;

namespace SpeakerHop.Services;

public sealed class HotkeyService : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
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
        _nextId = 100;
        var failures = new List<HotkeyRegistrationFailure>();
        foreach (var hotkey in _settings.Current.Hotkeys.Where(h => h.Enabled))
        {
            if (!Register(hotkey, out var errorCode))
            {
                failures.Add(new HotkeyRegistrationFailure(hotkey, errorCode));
            }
        }

        return failures;
    }

    public IReadOnlyList<HotkeyRegistrationFailure> SaveAndReload()
    {
        _settings.Save();
        return RegisterConfiguredHotkeys();
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
        if (RegisterHotKey(Handle, id, hotkey.Modifiers, hotkey.Key))
        {
            _registered[id] = hotkey;
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return false;
    }

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
