using System.Runtime.InteropServices;
using SpeakerHop.Models;
using NAudio.CoreAudioApi;

namespace SpeakerHop.Services;

public sealed class AudioDeviceService
{
    public List<AudioDeviceInfo> GetRenderDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var defaultId = defaultDevice.ID;
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active)
            .Select(device => new AudioDeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                IsDefault = string.Equals(defaultId, device.ID, StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.Name)
            .ToList();
    }

    public void SetDefaultRenderDevice(string deviceId)
    {
        var policyConfig = (IPolicyConfig)(object)new PolicyConfigClient();
        policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
        policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
        policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
    }

    public float ChangeDefaultVolume(int deltaPercent)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var endpoint = device.AudioEndpointVolume;
        var next = Math.Clamp(endpoint.MasterVolumeLevelScalar + (deltaPercent / 100f), 0f, 1f);
        endpoint.MasterVolumeLevelScalar = next;
        if (next > 0)
        {
            endpoint.Mute = false;
        }

        return next;
    }

    public bool ToggleDefaultMute()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var endpoint = device.AudioEndpointVolume;
        endpoint.Mute = !endpoint.Mute;
        return endpoint.Mute;
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private sealed class PolicyConfigClient;

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat();
        void GetDeviceFormat();
        void ResetDeviceFormat();
        void SetDeviceFormat();
        void GetProcessingPeriod();
        void SetProcessingPeriod();
        void GetShareMode();
        void SetShareMode();
        void GetPropertyValue();
        void SetPropertyValue();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        void SetEndpointVisibility();
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }
}
