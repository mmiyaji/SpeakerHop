using SpeakerHop.Models;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class ShortcutDraftSessionTests
{
    [Fact]
    public void EditAndRevert_TracksCurrentValues()
    {
        var saved = Binding("original", HotkeyActionType.MuteToggle, 0x41);
        var session = new ShortcutDraftSession([saved]);
        var row = session.Rows.Single(binding => binding.Id == saved.Id);

        Assert.False(session.HasChanges);
        row.Key = 0x42;
        Assert.True(session.HasChanges);
        row.Key = 0x41;
        Assert.False(session.HasChanges);
    }

    [Fact]
    public void AcceptSavedAndDiscard_ResetRowsAndBaseline()
    {
        var original = Binding("original", HotkeyActionType.MuteToggle, 0x41);
        var session = new ShortcutDraftSession([original]);
        session.Rows.Single(binding => binding.Id == original.Id).Name = "draft";
        session.Discard();
        Assert.False(session.HasChanges);
        Assert.Equal("original", session.Rows.Single(binding => binding.Id == original.Id).Name);

        session.Rows.Single(binding => binding.Id == original.Id).Name = "saved edit";
        var persisted = session.SnapshotForSave();
        session.AcceptSaved(persisted);
        Assert.False(session.HasChanges);
        session.Rows.Single(binding => binding.Id == original.Id).Name = "later draft";
        session.Discard();
        Assert.Equal("saved edit", session.Rows.Single(binding => binding.Id == original.Id).Name);
        Assert.False(session.HasChanges);
    }

    [Fact]
    public void DisabledBindingsAndDuplicates_AreComparedInStablePerActionOrder()
    {
        var first = Binding("first", HotkeyActionType.SetDevice, 0x41, enabled: false, deviceId: "device-a");
        var second = Binding("second", HotkeyActionType.SetDevice, 0x42, enabled: true, deviceId: "device-b");
        var session = new ShortcutDraftSession([first, second]);

        Assert.False(session.HasChanges);
        Assert.Equal(new[] { first.Id, second.Id }, session.SnapshotForSave().Select(binding => binding.Id));
        session.Rows.Single(binding => binding.Id == first.Id).DeviceId = "DEVICE-A";
        Assert.False(session.HasChanges); // Device identifiers are case-insensitive.
        session.Rows.Single(binding => binding.Id == first.Id).Enabled = true;
        Assert.True(session.HasChanges);
        session.Discard();
        Assert.False(session.Rows.Single(binding => binding.Id == first.Id).Enabled);
        Assert.Equal(new[] { first.Id, second.Id }, session.SnapshotForSave().Select(binding => binding.Id));
    }

    [Fact]
    public void PlaceholdersDoNotCreateChanges_AndSnapshotsDoNotMutateInputsOrRows()
    {
        var original = Binding("saved", HotkeyActionType.MuteToggle, 0x41);
        var session = new ShortcutDraftSession([original]);

        Assert.False(session.HasChanges);
        Assert.Contains(session.Rows, row => row.Key == 0 && !row.Enabled);
        var snapshot = session.SnapshotForSave();
        Assert.DoesNotContain(snapshot, row => row.Key == 0);
        Assert.NotSame(original, session.Rows.Single(row => row.Id == original.Id));
        Assert.NotSame(session.Rows.Single(row => row.Id == original.Id), snapshot.Single(row => row.Id == original.Id));

        snapshot[0].Name = "external mutation";
        Assert.Equal("saved", session.Rows.Single(row => row.Id == original.Id).Name);
        Assert.Equal("saved", original.Name);
        Assert.False(session.HasChanges);
    }

    private static HotkeyDefinition Binding(string name, HotkeyActionType action, uint key,
        bool enabled = true, string? deviceId = null) => new()
    {
        Id = Guid.NewGuid(), Name = name, Action = action, Key = key,
        DeviceId = deviceId, Modifiers = 2, VolumeStep = 9, Enabled = enabled
    };
}
