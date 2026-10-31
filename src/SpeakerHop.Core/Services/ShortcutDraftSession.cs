using SpeakerHop.Models;

namespace SpeakerHop.Services;

/// <summary>Tracks editable shortcut rows against the last persisted settings.</summary>
public sealed class ShortcutDraftSession
{
    private List<HotkeyDefinition> _baseline;

    public ShortcutDraftSession(IEnumerable<HotkeyDefinition> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        _baseline = Snapshot(saved);
        Rows = ShortcutDraftService.CreateRows(_baseline);
    }

    public List<HotkeyDefinition> Rows { get; private set; }

    public bool HasChanges => !SameBindings(_baseline, ShortcutDraftService.ToSettings(Rows));

    /// <summary>Accepts the settings actually persisted after a successful save.</summary>
    public void AcceptSaved(IEnumerable<HotkeyDefinition> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        _baseline = Snapshot(saved);
        Rows = ShortcutDraftService.CreateRows(_baseline);
    }

    /// <summary>Restores editable rows from the last accepted settings.</summary>
    public void Discard() => Rows = ShortcutDraftService.CreateRows(_baseline);

    /// <summary>Returns cloned, persistable bindings in row order.</summary>
    public List<HotkeyDefinition> SnapshotForSave() => ShortcutDraftService.ToSettings(Rows);

    private static List<HotkeyDefinition> Snapshot(IEnumerable<HotkeyDefinition> bindings) =>
        ShortcutDraftService.ToSettings(ShortcutDraftService.CreateRows(bindings));

    private static bool SameBindings(IReadOnlyList<HotkeyDefinition> left, IReadOnlyList<HotkeyDefinition> right)
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            var a = left[i];
            var b = right[i];
            if (a.Id != b.Id || a.Name != b.Name || a.Action != b.Action ||
                !string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase) ||
                a.Modifiers != b.Modifiers || a.Key != b.Key ||
                a.VolumeStep != b.VolumeStep || a.Enabled != b.Enabled)
                return false;
        }

        return true;
    }
}
