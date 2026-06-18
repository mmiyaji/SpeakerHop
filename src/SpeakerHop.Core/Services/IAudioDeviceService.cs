using SpeakerHop.Models;

namespace SpeakerHop.Services;

public interface IAudioDeviceService
{
    IReadOnlyList<AudioDeviceInfo> GetRenderDevices();
    void SetDefaultRenderDevice(string deviceId);
    float ChangeDefaultVolume(int deltaPercent);
    bool ToggleDefaultMute();
}
