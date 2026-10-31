using SpeakerHop.Models;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class ShortcutDraftServiceTests
{
    [Fact]
    public void CreateRows_IncludesAllActionsAndAddsUnassignedPlaceholders()
    {
        var rows = ShortcutDraftService.CreateRows([]);

        Assert.Equal(Enum.GetValues<HotkeyActionType>(), rows.Select(row => row.Action));
        Assert.All(rows, row =>
        {
            Assert.Equal(0u, row.Key);
            Assert.Equal(0u, row.Modifiers);
            Assert.False(row.Enabled);
            Assert.Equal(5, row.VolumeStep);
        });
    }

    [Fact]
    public void CreateRows_PreservesEveryBindingAndDoesNotMutateInputs()
    {
        var idOne = Guid.NewGuid();
        var idTwo = Guid.NewGuid();
        var first = new HotkeyDefinition
        {
            Id = idOne, Name = "first", Action = HotkeyActionType.MuteToggle,
            DeviceId = "device-a", Modifiers = 3, Key = 0x42, VolumeStep = 12, Enabled = false
        };
        var second = new HotkeyDefinition
        {
            Id = idTwo, Name = "second", Action = HotkeyActionType.MuteToggle,
            DeviceId = "device-b", Modifiers = 8, Key = 0x43, VolumeStep = 27, Enabled = true
        };
        var input = new[] { first, second };

        var rows = ShortcutDraftService.CreateRows(input);
        var muteRows = rows.Where(row => row.Action == HotkeyActionType.MuteToggle).ToArray();

        Assert.Equal(new[] { idOne, idTwo }, muteRows.Select(row => row.Id));
        Assert.Equal("first", muteRows[0].Name);
        Assert.Equal("device-a", muteRows[0].DeviceId);
        Assert.Equal(3u, muteRows[0].Modifiers);
        Assert.Equal(0x42u, muteRows[0].Key);
        Assert.Equal(12, muteRows[0].VolumeStep);
        Assert.False(muteRows[0].Enabled);
        Assert.Equal("second", muteRows[1].Name);
        Assert.Equal("device-b", muteRows[1].DeviceId);
        Assert.Equal(8u, muteRows[1].Modifiers);
        Assert.Equal(0x43u, muteRows[1].Key);
        Assert.Equal(27, muteRows[1].VolumeStep);
        Assert.True(muteRows[1].Enabled);
        Assert.NotSame(first, muteRows[0]);
        Assert.NotSame(second, muteRows[1]);
        Assert.Equal(new[] { first, second }, input);

        // Explicit values 4 and 5 remain their assigned actions.
        Assert.Equal(HotkeyActionType.MuteToggle, muteRows[0].Action);
        Assert.Equal(HotkeyActionType.MicrophoneMuteToggle,
            rows.Single(row => row.Action == HotkeyActionType.MicrophoneMuteToggle).Action);
    }

    [Fact]
    public void ToSettings_SkipsUnassignedAndModifierKeysButKeepsDisabledRealBindings()
    {
        var disabledId = Guid.NewGuid();
        var enabledId = Guid.NewGuid();
        var input = new[]
        {
            new HotkeyDefinition { Action = HotkeyActionType.VolumeUp, Key = 0, Enabled = false },
            new HotkeyDefinition { Action = HotkeyActionType.VolumeDown, Key = 0x11, Enabled = true },
            new HotkeyDefinition { Id = disabledId, Action = HotkeyActionType.MuteToggle, Key = 0x41, Enabled = false },
            new HotkeyDefinition { Id = enabledId, Action = HotkeyActionType.MicrophoneMuteToggle, Key = 0x42, Enabled = true }
        };

        var settings = ShortcutDraftService.ToSettings(input);

        Assert.Equal(new[] { disabledId, enabledId }, settings.Select(row => row.Id));
        Assert.False(settings[0].Enabled);
        Assert.True(settings[1].Enabled);
        Assert.NotSame(input[2], settings[0]);
        Assert.Equal(0u, input[0].Key);
        Assert.Equal(0x11u, input[1].Key);
    }

    [Fact]
    public void DraftRoundTrip_PreservesBindingsOrderAndIsolatesEditsAndClears()
    {
        var persisted = new[]
        {
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Volume custom", Action = HotkeyActionType.VolumeUp, Key = 0x51, Modifiers = 1, VolumeStep = 17, Enabled = false },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Device alpha", Action = HotkeyActionType.SetDevice, DeviceId = "alpha", Key = 0x41, Modifiers = 2, VolumeStep = 9, Enabled = false },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Cycle custom", Action = HotkeyActionType.CycleDevice, Key = 0x43, Modifiers = 4, VolumeStep = 11, Enabled = true },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Mute custom", Action = HotkeyActionType.MuteToggle, Key = 0x4D, Modifiers = 8, VolumeStep = 13, Enabled = false },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Device beta", Action = HotkeyActionType.SetDevice, DeviceId = "beta", Key = 0x42, Modifiers = 3, VolumeStep = 10, Enabled = true },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Volume down custom", Action = HotkeyActionType.VolumeDown, Key = 0x52, Modifiers = 5, VolumeStep = 19, Enabled = false },
            new HotkeyDefinition { Id = Guid.NewGuid(), Name = "Mic custom", Action = HotkeyActionType.MicrophoneMuteToggle, Key = 0x4E, Modifiers = 6, VolumeStep = 15, Enabled = true }
        };

        var draft = ShortcutDraftService.CreateRows(persisted);
        var expectedStableOrder = persisted
            .OrderBy(binding => binding.Action)
            .Select(binding => binding.Id);
        Assert.Equal(expectedStableOrder, draft.Select(binding => binding.Id));
        Assert.Equal(persisted.Length, draft.Count); // Every action exists, so no placeholders are added.

        var editedDraft = draft.Single(binding => binding.Id == persisted[0].Id);
        editedDraft.Name = "Edited in draft";
        editedDraft.Key = 0x53;
        Assert.Equal("Volume custom", persisted[0].Name);
        Assert.Equal(0x51u, persisted[0].Key);

        var saved = ShortcutDraftService.ToSettings(draft);
        Assert.Equal(draft.Select(binding => binding.Id), saved.Select(binding => binding.Id));
        Assert.Equal(draft.Select(Fields), saved.Select(Fields));

        // Clearing one of two SetDevice bindings removes only that binding.
        draft.Single(binding => binding.DeviceId == "alpha").Key = 0;
        var afterOneClear = ShortcutDraftService.ToSettings(draft);
        Assert.DoesNotContain(afterOneClear, binding => binding.DeviceId == "alpha");
        Assert.Contains(afterOneClear, binding => binding.DeviceId == "beta");

        // Once the final binding for an action is cleared, CreateRows supplies its placeholder again.
        draft.Single(binding => binding.DeviceId == "beta").Key = 0;
        var withSetDevicePlaceholders = ShortcutDraftService.CreateRows(
            ShortcutDraftService.ToSettings(draft));
        var setDeviceRows = withSetDevicePlaceholders
            .Where(binding => binding.Action == HotkeyActionType.SetDevice)
            .ToArray();
        var placeholder = Assert.Single(setDeviceRows);
        Assert.Equal(0u, placeholder.Key);
        Assert.False(placeholder.Enabled);
        Assert.Equal(5, placeholder.VolumeStep);
    }

    private static (Guid Id, string Name, HotkeyActionType Action, string? DeviceId,
        uint Modifiers, uint Key, int VolumeStep, bool Enabled) Fields(HotkeyDefinition binding) =>
        (binding.Id, binding.Name, binding.Action, binding.DeviceId, binding.Modifiers,
            binding.Key, binding.VolumeStep, binding.Enabled);
}
