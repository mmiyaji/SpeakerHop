using SpeakerHop.Models;

namespace SpeakerHop.Services;

public sealed class AudioCommandService
{
    private static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromHours(1);
    private readonly IAudioDeviceService _audio;
    private readonly ISettingsService _settings;
    private readonly TimeProvider _timeProvider;

    public event EventHandler<AudioCommandNotification>? StatusChanged;

    public AudioCommandService(IAudioDeviceService audio, ISettingsService settings, TimeProvider? timeProvider = null)
    {
        _audio = audio;
        _settings = settings;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        try
        {
            var devices = _audio.GetRenderDevices();
            var cycleIds = _settings.Current.CycleDeviceIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var now = _timeProvider.GetUtcNow();
            foreach (var device in devices)
            {
                device.IncludeInCycle = cycleIds.Contains(device.Id);
            }

            RememberConnectedDevices(devices, now);
            var activeIds = devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rememberedSelectedDevices = _settings.Current.RememberedAudioDevices
                .Where(device => cycleIds.Contains(device.Id) && !activeIds.Contains(device.Id))
                .OrderByDescending(device => device.LastSeen)
                .Select(device => new AudioDeviceInfo
                {
                    Id = device.Id,
                    Name = device.Name,
                    IsConnected = false,
                    LastSeen = device.LastSeen,
                    IncludeInCycle = true
                });

            return devices.Concat(rememberedSelectedDevices).ToList();
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.getDevicesFailed"), ex.Message));
            return [];
        }
    }

    public void CycleDevice()
    {
        try
        {
            var activeDevices = _audio.GetRenderDevices();
            var activeById = activeDevices
                .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var devices = new List<AudioDeviceInfo>();
            foreach (var id in _settings.Current.CycleDeviceIds)
            {
                if (activeById.TryGetValue(id, out var device) &&
                    !devices.Any(existing => string.Equals(existing.Id, device.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    devices.Add(device);
                }
            }

            if (devices.Count == 0)
            {
                Notify(T("audio.noCycleDevices"));
                return;
            }

            var defaultIndex = devices.FindIndex(d => d.IsDefault);
            var next = devices[(defaultIndex + 1 + devices.Count) % devices.Count];
            SetDevice(next.Id);
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.switchFailed"), ex.Message));
        }
    }

    public void SetDevice(string deviceId)
    {
        try
        {
            var target = _audio.GetRenderDevices().FirstOrDefault(d => d.Id == deviceId);
            if (target is null)
            {
                Notify(T("audio.targetMissing"));
                return;
            }

            _audio.SetDefaultRenderDevice(deviceId);
            Notify(string.Format(T("audio.outputTarget"), target.Name), isDeviceSwitch: true);
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.switchFailed"), ex.Message));
        }
    }

    public void ChangeVolume(int deltaPercent)
    {
        try
        {
            var newValue = _audio.ChangeDefaultVolume(deltaPercent);
            Notify(string.Format(T("audio.volume"), newValue.ToString("P0")));
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.volumeChangeFailed"), ex.Message));
        }
    }

    public void ToggleMute()
    {
        try
        {
            var muted = _audio.ToggleDefaultMute();
            Notify(muted ? T("audio.muted") : T("audio.unmuted"));
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.muteToggleFailed"), ex.Message));
        }
    }

    public void ToggleMicrophoneMute()
    {
        try
        {
            var muted = _audio.ToggleDefaultMicrophoneMute();
            Notify(muted ? T("audio.microphoneMuted") : T("audio.microphoneUnmuted"));
        }
        catch (Exception ex)
        {
            Notify(string.Format(T("audio.microphoneMuteToggleFailed"), ex.Message));
        }
    }

    public void Apply(HotkeyDefinition hotkey)
    {
        switch (hotkey.Action)
        {
            case HotkeyActionType.CycleDevice:
                CycleDevice();
                break;
            case HotkeyActionType.SetDevice when !string.IsNullOrWhiteSpace(hotkey.DeviceId):
                SetDevice(hotkey.DeviceId);
                break;
            case HotkeyActionType.VolumeUp:
                ChangeVolume(Math.Abs(hotkey.VolumeStep));
                break;
            case HotkeyActionType.VolumeDown:
                ChangeVolume(-Math.Abs(hotkey.VolumeStep));
                break;
            case HotkeyActionType.MuteToggle:
                ToggleMute();
                break;
            case HotkeyActionType.MicrophoneMuteToggle:
                ToggleMicrophoneMute();
                break;
        }
    }

    private void Notify(string message, bool isDeviceSwitch = false)
    {
        StatusChanged?.Invoke(this, new AudioCommandNotification(message, isDeviceSwitch));
    }

    private void RememberConnectedDevices(IEnumerable<AudioDeviceInfo> devices, DateTimeOffset lastSeen)
    {
        var remembered = _settings.Current.RememberedAudioDevices
            .ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var device in devices)
        {
            if (remembered.TryGetValue(device.Id, out var existing))
            {
                if (!string.Equals(existing.Name, device.Name, StringComparison.Ordinal) ||
                    lastSeen - existing.LastSeen >= LastSeenWriteInterval)
                {
                    existing.Name = device.Name;
                    existing.LastSeen = lastSeen;
                    changed = true;
                }

                continue;
            }

            _settings.Current.RememberedAudioDevices.Add(new RememberedAudioDevice
            {
                Id = device.Id,
                Name = device.Name,
                LastSeen = lastSeen
            });
            changed = true;
        }

        if (changed)
        {
            TrySaveSettings();
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch
        {
            // Audio enumeration and switching must remain usable when settings persistence fails.
        }
    }

    private string T(string key)
    {
        return LocalizationService.Text(key, _settings.Current.Language);
    }

}

public sealed record AudioCommandNotification(string Message, bool IsDeviceSwitch);
