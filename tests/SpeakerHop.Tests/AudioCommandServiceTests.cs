using SpeakerHop.Models;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class AudioCommandServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 18, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void GetDevices_MarksSelectedDevicesAndAppendsRememberedMissingDevices()
    {
        var settings = new FakeSettingsService();
        settings.Current.Language = LocalizationService.English;
        settings.Current.CycleDeviceIds = ["SPEAKER", "missing"];
        settings.Current.RememberedAudioDevices.Add(new RememberedAudioDevice
        {
            Id = "missing",
            Name = "Missing HDMI",
            LastSeen = FixedNow.AddDays(-2)
        });
        var audio = new FakeAudioDeviceService
        {
            Devices =
            [
                Device("speaker", "Speakers", isDefault: true),
                Device("headphones", "Headphones")
            ]
        };
        var service = CreateService(audio, settings);

        var devices = service.GetDevices();

        Assert.Equal(["speaker", "headphones", "missing"], devices.Select(device => device.Id));
        Assert.True(devices[0].IncludeInCycle);
        Assert.False(devices[1].IncludeInCycle);
        Assert.False(devices[2].IsConnected);
        Assert.True(devices[2].IncludeInCycle);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void GetDevices_UpdatesRememberedDevicesOnlyWhenChanged()
    {
        var settings = new FakeSettingsService();
        settings.Current.Language = LocalizationService.English;
        settings.Current.RememberedAudioDevices.Add(new RememberedAudioDevice
        {
            Id = "speaker",
            Name = "Old Speakers",
            LastSeen = FixedNow.AddMinutes(-1)
        });
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("speaker", "Speakers")]
        };
        var service = CreateService(audio, settings);

        _ = service.GetDevices();
        _ = service.GetDevices();

        var remembered = Assert.Single(settings.Current.RememberedAudioDevices);
        Assert.Equal("Speakers", remembered.Name);
        Assert.Equal(FixedNow, remembered.LastSeen);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void GetDevices_WhenAudioThrows_NotifiesAndReturnsEmpty()
    {
        var audio = new FakeAudioDeviceService
        {
            GetRenderDevicesException = new InvalidOperationException("device failure")
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        var devices = service.GetDevices();

        Assert.Empty(devices);
        var notification = Assert.Single(notifications);
        Assert.Equal("Could not get audio devices: device failure", notification.Message);
        Assert.False(notification.IsDeviceSwitch);
    }

    [Fact]
    public void CycleDevice_WhenNoCycleDevices_NotifiesAndDoesNotSetDevice()
    {
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("speaker", "Speakers", isDefault: true)]
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.CycleDevice();

        Assert.Empty(audio.SetDefaultRenderDeviceCalls);
        Assert.Equal("No cycle devices are selected", Assert.Single(notifications).Message);
    }

    [Fact]
    public void CycleDevice_WhenNoDefaultDeviceUsesFirstSelectedDevice()
    {
        var settings = EnglishSettings();
        settings.Current.CycleDeviceIds = ["a", "b"];
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("a", "A"), Device("b", "B")]
        };
        var service = CreateService(audio, settings);
        var notifications = CaptureNotifications(service);

        service.CycleDevice();

        Assert.Equal(["a"], audio.SetDefaultRenderDeviceCalls);
        Assert.Equal("Output: A", Assert.Single(notifications).Message);
        Assert.True(notifications[0].IsDeviceSwitch);
    }

    [Fact]
    public void CycleDevice_SwitchesToDeviceAfterCurrentDefault()
    {
        var settings = EnglishSettings();
        settings.Current.CycleDeviceIds = ["a", "b", "c"];
        var audio = new FakeAudioDeviceService
        {
            Devices =
            [
                Device("a", "A", isDefault: true),
                Device("b", "B"),
                Device("c", "C")
            ]
        };
        var service = CreateService(audio, settings);

        service.CycleDevice();

        Assert.Equal(["b"], audio.SetDefaultRenderDeviceCalls);
    }

    [Fact]
    public void SetDevice_WhenTargetMissing_Notifies()
    {
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("speaker", "Speakers")]
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.SetDevice("missing");

        Assert.Empty(audio.SetDefaultRenderDeviceCalls);
        Assert.Equal("The specified device could not be found", Assert.Single(notifications).Message);
    }

    [Fact]
    public void SetDevice_WhenSetFails_NotifiesSwitchFailure()
    {
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("speaker", "Speakers")],
            SetDefaultRenderDeviceException = new InvalidOperationException("denied")
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.SetDevice("speaker");

        Assert.Equal("Could not switch output device: denied", Assert.Single(notifications).Message);
    }

    [Fact]
    public void SetDevice_WhenGetFails_NotifiesSwitchFailure()
    {
        var audio = new FakeAudioDeviceService
        {
            GetRenderDevicesException = new InvalidOperationException("enumeration failed")
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.SetDevice("speaker");

        Assert.Equal("Could not switch output device: enumeration failed", Assert.Single(notifications).Message);
    }

    [Fact]
    public void SetDevice_WithoutSubscribers_DoesNotThrow()
    {
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("speaker", "Speakers")]
        };
        var service = new AudioCommandService(audio, EnglishSettings());

        service.SetDevice("speaker");

        Assert.Equal(["speaker"], audio.SetDefaultRenderDeviceCalls);
    }

    [Fact]
    public void ChangeVolume_WhenSuccessful_NotifiesFormattedPercent()
    {
        var audio = new FakeAudioDeviceService { VolumeResult = 0.42f };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.ChangeVolume(15);

        Assert.Equal([15], audio.ChangeDefaultVolumeCalls);
        Assert.Contains("42", Assert.Single(notifications).Message);
    }

    [Fact]
    public void ChangeVolume_WhenAudioThrows_NotifiesFailure()
    {
        var audio = new FakeAudioDeviceService
        {
            ChangeDefaultVolumeException = new InvalidOperationException("muted endpoint")
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.ChangeVolume(-10);

        Assert.Equal("Could not change volume: muted endpoint", Assert.Single(notifications).Message);
    }

    [Fact]
    public void ToggleMute_ReportsMutedAndUnmutedStates()
    {
        var audio = new FakeAudioDeviceService { ToggleDefaultMuteResults = new Queue<bool>([true, false]) };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.ToggleMute();
        service.ToggleMute();

        Assert.Equal(["Muted", "Unmuted"], notifications.Select(notification => notification.Message));
    }

    [Fact]
    public void ToggleMute_WhenAudioThrows_NotifiesFailure()
    {
        var audio = new FakeAudioDeviceService
        {
            ToggleDefaultMuteException = new InvalidOperationException("no endpoint")
        };
        var service = CreateService(audio, EnglishSettings());
        var notifications = CaptureNotifications(service);

        service.ToggleMute();

        Assert.Equal("Could not toggle mute: no endpoint", Assert.Single(notifications).Message);
    }

    [Fact]
    public void Apply_DispatchesEverySupportedActionAndIgnoresIncompleteSetDevice()
    {
        var settings = EnglishSettings();
        settings.Current.CycleDeviceIds = ["a"];
        var audio = new FakeAudioDeviceService
        {
            Devices = [Device("a", "A"), Device("b", "B")]
        };
        var service = CreateService(audio, settings);

        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.CycleDevice });
        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.SetDevice, DeviceId = "b" });
        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.SetDevice, DeviceId = " " });
        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.VolumeUp, VolumeStep = -7 });
        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.VolumeDown, VolumeStep = -8 });
        service.Apply(new HotkeyDefinition { Action = HotkeyActionType.MuteToggle });
        service.Apply(new HotkeyDefinition { Action = (HotkeyActionType)999 });

        Assert.Equal(["a", "b"], audio.SetDefaultRenderDeviceCalls);
        Assert.Equal([7, -8], audio.ChangeDefaultVolumeCalls);
        Assert.Equal(1, audio.ToggleDefaultMuteCallCount);
    }

    private static AudioCommandService CreateService(
        FakeAudioDeviceService audio,
        FakeSettingsService settings)
    {
        return new AudioCommandService(audio, settings, new FixedTimeProvider(FixedNow));
    }

    private static FakeSettingsService EnglishSettings()
    {
        var settings = new FakeSettingsService();
        settings.Current.Language = LocalizationService.English;
        return settings;
    }

    private static List<AudioCommandNotification> CaptureNotifications(AudioCommandService service)
    {
        var notifications = new List<AudioCommandNotification>();
        service.StatusChanged += (_, notification) => notifications.Add(notification);
        return notifications;
    }

    private static AudioDeviceInfo Device(string id, string name, bool isDefault = false)
    {
        return new AudioDeviceInfo
        {
            Id = id,
            Name = name,
            IsDefault = isDefault
        };
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public int SaveCount { get; private set; }

        public void Save()
        {
            SaveCount++;
        }
    }

    private sealed class FakeAudioDeviceService : IAudioDeviceService
    {
        public IReadOnlyList<AudioDeviceInfo> Devices { get; init; } = [];
        public Exception? GetRenderDevicesException { get; init; }
        public Exception? SetDefaultRenderDeviceException { get; init; }
        public Exception? ChangeDefaultVolumeException { get; init; }
        public Exception? ToggleDefaultMuteException { get; init; }
        public float VolumeResult { get; init; } = 0.5f;
        public Queue<bool> ToggleDefaultMuteResults { get; init; } = new([true]);
        public List<string> SetDefaultRenderDeviceCalls { get; } = [];
        public List<int> ChangeDefaultVolumeCalls { get; } = [];
        public int ToggleDefaultMuteCallCount { get; private set; }

        public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
        {
            if (GetRenderDevicesException is not null)
            {
                throw GetRenderDevicesException;
            }

            return Devices;
        }

        public void SetDefaultRenderDevice(string deviceId)
        {
            if (SetDefaultRenderDeviceException is not null)
            {
                throw SetDefaultRenderDeviceException;
            }

            SetDefaultRenderDeviceCalls.Add(deviceId);
        }

        public float ChangeDefaultVolume(int deltaPercent)
        {
            if (ChangeDefaultVolumeException is not null)
            {
                throw ChangeDefaultVolumeException;
            }

            ChangeDefaultVolumeCalls.Add(deltaPercent);
            return VolumeResult;
        }

        public bool ToggleDefaultMute()
        {
            if (ToggleDefaultMuteException is not null)
            {
                throw ToggleDefaultMuteException;
            }

            ToggleDefaultMuteCallCount++;
            return ToggleDefaultMuteResults.Count == 0 || ToggleDefaultMuteResults.Dequeue();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
