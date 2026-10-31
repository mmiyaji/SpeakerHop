using System.Runtime.InteropServices;
using System.Text;
using SpeakerHop.Models;

namespace SpeakerHop.Services;

/// <summary>Uses the current Windows keyboard layout for layout-dependent key names.</summary>
internal static class KeyboardKeyDisplay
{
    internal static string GestureText(HotkeyDefinition hotkey) =>
        hotkey.FormatGesture(LayoutKeyName(hotkey.Key));

    private static string? LayoutKeyName(uint key)
    {
        // OEM virtual keys identify positions, not fixed punctuation. Their names
        // must not assume that the user has a US keyboard.
        if (key is not (>= 0xBA and <= 0xC0) and not (>= 0xDB and <= 0xDF) and not 0xE2)
        {
            return null;
        }

        var scanCode = MapVirtualKeyEx(key, 4, GetKeyboardLayout(0));
        if (scanCode == 0)
        {
            return null;
        }

        var keyData = (scanCode & 0xFF) << 16;
        if ((scanCode & 0xFF00) != 0) keyData |= 1u << 24;
        var name = new StringBuilder(128);
        return GetKeyNameText(unchecked((int)keyData), name, name.Capacity) > 0
            ? name.ToString()
            : null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

    [DllImport("user32.dll", EntryPoint = "GetKeyNameTextW", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int keyData, StringBuilder text, int size);
}
