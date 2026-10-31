using SpeakerHop.Models;

namespace SpeakerHop.Tests;

public sealed class HotkeyDefinitionTests
{
    [Fact]
    public void GestureText_IncludesEnabledModifierNamesInOrder()
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = 0x0001 | 0x0002 | 0x0004 | 0x0008,
            Key = 0x70
        };

        Assert.Equal("Alt + Ctrl + Shift + Win + F1", hotkey.GestureText);
    }

    [Fact]
    public void GestureText_WhenNoModifiers_UsesOnlyKeyText()
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = 0,
            Key = 0x5A
        };

        Assert.Equal("Z", hotkey.GestureText);
    }

    [Fact]
    public void FormatGesture_UsesSuppliedKeyLabelAndKeepsModifierOrder()
    {
        var hotkey = new HotkeyDefinition
        {
            Modifiers = 0x0001 | 0x0002 | 0x0004 | 0x0008,
            Key = 0xBA
        };

        Assert.Equal("Alt + Ctrl + Shift + Win + ;", hotkey.FormatGesture(";"));
    }

    [Fact]
    public void FormatGesture_WithoutOverrideUsesCoreKeyLabel()
    {
        var hotkey = new HotkeyDefinition { Modifiers = 0, Key = 0x20 };

        Assert.Equal("Space", hotkey.FormatGesture());
    }

    [Theory]
    [InlineData(0x30u, "0")]
    [InlineData(0x39u, "9")]
    [InlineData(0x3Au, "VK 0x3A")]
    [InlineData(0x40u, "VK 0x40")]
    [InlineData(0x41u, "A")]
    [InlineData(0x5Au, "Z")]
    [InlineData(0x70u, "F1")]
    [InlineData(0x87u, "F24")]
    [InlineData(0x07u, "VK 0x07")]
    public void KeyText_FormatsKnownRangesAndFallback(uint key, string expected)
    {
        var hotkey = new HotkeyDefinition { Key = key };

        Assert.Equal(expected, hotkey.KeyText);
    }

    [Theory]
    [InlineData(0x20u, "Space")]
    [InlineData(0x09u, "Tab")]
    [InlineData(0x0Du, "Enter")]
    [InlineData(0x1Bu, "Escape")]
    [InlineData(0x08u, "Backspace")]
    [InlineData(0x0Cu, "Clear")]
    [InlineData(0x15u, "Kana/Hangul")]
    [InlineData(0x19u, "Hanja/Kanji")]
    [InlineData(0x1Cu, "Convert")]
    [InlineData(0x1Du, "NonConvert")]
    [InlineData(0x1Fu, "Mode Change")]
    [InlineData(0x2Eu, "Delete")]
    [InlineData(0x2Fu, "Help")]
    [InlineData(0x21u, "Page Up")]
    [InlineData(0x22u, "Page Down")]
    [InlineData(0x60u, "Numpad 0")]
    [InlineData(0x69u, "Numpad 9")]
    [InlineData(0x6Au, "Numpad Multiply")]
    [InlineData(0x6Cu, "Numpad Separator")]
    [InlineData(0x6Fu, "Numpad Divide")]
    [InlineData(0xADu, "Mute")]
    [InlineData(0xAEu, "Volume Down")]
    [InlineData(0xAFu, "Volume Up")]
    [InlineData(0xB0u, "Media Next")]
    [InlineData(0xA6u, "Browser Back")]
    [InlineData(0xBAu, "OEM1")]
    [InlineData(0xBBu, "OEM Plus")]
    [InlineData(0xBCu, "OEM Comma")]
    [InlineData(0xBDu, "OEM Minus")]
    [InlineData(0xBEu, "OEM Period")]
    [InlineData(0xBFu, "OEM2")]
    [InlineData(0xC0u, "OEM3")]
    [InlineData(0xDBu, "OEM4")]
    [InlineData(0xDCu, "OEM5")]
    [InlineData(0xDDu, "OEM6")]
    [InlineData(0xDEu, "OEM7")]
    [InlineData(0xDFu, "OEM8")]
    [InlineData(0xE2u, "OEM102")]
    public void KeyText_FormatsCommonNamedKeys(uint key, string expected)
    {
        var hotkey = new HotkeyDefinition { Key = key };

        Assert.Equal(expected, hotkey.KeyText);
    }

    [Theory]
    [InlineData(0x25u)]
    [InlineData(0x26u)]
    [InlineData(0x27u)]
    [InlineData(0x28u)]
    public void KeyText_FormatsArrowKeysWithoutVirtualKeyFallback(uint key)
    {
        var hotkey = new HotkeyDefinition { Key = key };

        Assert.False(hotkey.KeyText.StartsWith("VK", StringComparison.Ordinal));
        Assert.NotEmpty(hotkey.KeyText);
    }
}
