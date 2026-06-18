using SpeakerHop.Models;

namespace SpeakerHop.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
}
