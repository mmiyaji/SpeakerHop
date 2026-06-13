namespace SpeakerHop.Models;

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }
    public bool IsConnected { get; init; } = true;
    public DateTimeOffset? LastSeen { get; init; }
    public bool IncludeInCycle { get; set; }
}
