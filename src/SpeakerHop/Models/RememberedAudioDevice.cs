namespace SpeakerHop.Models;

public sealed class RememberedAudioDevice
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
