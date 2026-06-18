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

    [Theory]
    [InlineData(0x30u, "0")]
    [InlineData(0x5Au, "Z")]
    [InlineData(0x70u, "F1")]
    [InlineData(0x87u, "F24")]
    [InlineData(0x2Fu, "VK 0x2F")]
    public void KeyText_FormatsKnownRangesAndFallback(uint key, string expected)
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
