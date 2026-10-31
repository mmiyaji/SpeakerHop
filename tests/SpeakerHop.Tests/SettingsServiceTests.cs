using System.Text.Json;
using SpeakerHop.Models;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Constructor_RejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => new SettingsService(" "));
    }

    [Fact]
    public void Constructor_AllowsPathWithoutDirectory()
    {
        var service = new SettingsService("settings-only.json");

        Assert.NotNull(service.Current);
    }

    [Fact]
    public void Constructor_WhenParentPathIsBlocked_DoesNotThrow()
    {
        using var directory = TemporaryDirectory.Create();
        var blockedParent = Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(blockedParent, "file");

        var service = new SettingsService(Path.Combine(blockedParent, "settings.json"));

        Assert.NotNull(service.Current);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_CreatesDefaultSettingsFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var service = new SettingsService(path);

        service.Load();

        Assert.True(File.Exists(path));
        Assert.Equal("system", service.Current.Language);
        Assert.True(service.Current.NotifyOnDeviceSwitch);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsCurrentSettings()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var saved = new SettingsService(path);
        saved.Current.Language = LocalizationService.English;
        saved.Current.Theme = "dark";
        saved.Current.CycleDeviceIds = ["speaker", "headphones"];

        saved.Save();
        var loaded = new SettingsService(path);
        loaded.Load();

        Assert.Equal(LocalizationService.English, loaded.Current.Language);
        Assert.Equal("dark", loaded.Current.Theme);
        Assert.Equal(["speaker", "headphones"], loaded.Current.CycleDeviceIds);
    }

    [Fact]
    public void Load_PreservesExistingSpeakerMuteValueAndNewMicrophoneMuteValue()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "Hotkeys": [
                { "Id": "00000000-0000-0000-0000-000000000001", "Name": "Speaker", "Action": 4, "Modifiers": 0, "Key": 65, "VolumeStep": 5 },
                { "Id": "00000000-0000-0000-0000-000000000002", "Name": "Microphone", "Action": 5, "Modifiers": 0, "Key": 66, "VolumeStep": 5 }
              ]
            }
            """);
        var service = new SettingsService(path);

        service.Load();

        Assert.Equal([HotkeyActionType.MuteToggle, HotkeyActionType.MicrophoneMuteToggle],
            service.Current.Hotkeys.Select(hotkey => hotkey.Action));
    }

    [Fact]
    public void Load_WhenFileContainsJsonNull_UsesDefaultSettings()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "null");
        var service = new SettingsService(path);

        service.Load();

        Assert.Equal("system", service.Current.Language);
        Assert.NotEmpty(service.Current.Hotkeys);
    }

    [Fact]
    public void Load_WhenJsonIsInvalid_BacksUpInvalidFileAndSavesDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{ invalid json");
        var service = new SettingsService(path);

        service.Load();

        var backup = Assert.Single(Directory.GetFiles(directory.Path, "settings.json.*.invalid"));
        Assert.Equal("{ invalid json", File.ReadAllText(backup));
        Assert.True(File.Exists(path));
        Assert.Equal("system", service.Current.Language);
        Assert.True(JsonDocument.Parse(File.ReadAllText(path)).RootElement.TryGetProperty("Language", out _));
    }

    [Fact]
    public void Load_NormalizesNullDuplicateAndOutOfRangeSettings()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, """
            {
              "Language": "unsupported",
              "Theme": null,
              "CycleDeviceIds": [" speaker ", null, "SPEAKER", ""],
              "RememberedAudioDevices": [
                { "Id": "device", "Name": null, "LastSeen": "2026-01-01T00:00:00+00:00" },
                null
              ],
              "Hotkeys": [
                { "Id": "00000000-0000-0000-0000-000000000000", "Name": null, "Action": 999, "Modifiers": 65535, "Key": 0, "VolumeStep": -2147483648 },
                null
              ]
            }
            """);
        var service = new SettingsService(path);

        service.Load();

        Assert.Equal("system", service.Current.Language);
        Assert.Equal("system", service.Current.Theme);
        Assert.Equal(["speaker"], service.Current.CycleDeviceIds);
        var remembered = Assert.Single(service.Current.RememberedAudioDevices);
        Assert.Equal("device", remembered.Name);
        var hotkey = Assert.Single(service.Current.Hotkeys);
        Assert.NotEqual(Guid.Empty, hotkey.Id);
        Assert.Equal(HotkeyActionType.CycleDevice, hotkey.Action);
        Assert.Equal((uint)0x000F, hotkey.Modifiers);
        Assert.Equal((uint)0x41, hotkey.Key);
        Assert.Equal(100, hotkey.VolumeStep);
    }

    [Fact]
    public void Load_WhenExistingFileIsLocked_FallsBackToDefaults()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{\"Language\":\"en-US\"}");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var service = new SettingsService(path);

        service.Load();

        Assert.Equal("system", service.Current.Language);
        Assert.True(service.IsReadOnlyDueToLoadFailure);
        locked.Dispose();
        var exception = Record.Exception(service.Save);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("{\"Language\":\"en-US\"}", File.ReadAllText(path));
        service.Load();
        Assert.False(service.IsReadOnlyDueToLoadFailure);
        service.Current.Language = LocalizationService.Japanese;
        service.Save();
        Assert.Contains("ja-JP", File.ReadAllText(path));
    }

    [Fact]
    public void Load_WhenReadFails_ResetsPersistedCompletionStatus()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{\"InitialLaunchCompleted\":true}");
        var service = new SettingsService(path);
        service.Load();
        Assert.True(service.InitialLaunchCompletionPersisted);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        service.Load();

        Assert.False(service.InitialLaunchCompletionPersisted);
        Assert.True(service.IsReadOnlyDueToLoadFailure);
    }

    [Fact]
    public void Save_WhenDestinationCannotBeReplaced_PreservesExistingFileAndCleansTemporaryFile()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        const string original = "{\"Language\":\"en-US\"}";
        File.WriteAllText(path, original);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var service = new SettingsService(path);
        service.Current.Language = LocalizationService.Japanese;

        var exception = Record.Exception(service.Save);
        Assert.True(exception is IOException or UnauthorizedAccessException);

        locked.Position = 0;
        using var reader = new StreamReader(locked, leaveOpen: true);
        Assert.Equal(original, reader.ReadToEnd());
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void MarkInitialLaunchCompleted_WhenNotCompleted_SetsFlagAndSaves()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var service = new SettingsService(path);

        service.MarkInitialLaunchCompleted();

        Assert.True(service.Current.InitialLaunchCompleted);
        Assert.Contains("\"InitialLaunchCompleted\": true", File.ReadAllText(path));
    }

    [Fact]
    public void MarkInitialLaunchCompleted_WhenAlreadyCompleted_DoesNotSave()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var service = new SettingsService(path);
        service.Current.InitialLaunchCompleted = true;

        service.MarkInitialLaunchCompleted();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MarkInitialLaunchCompleted_WhenSaveFails_DoesNotClaimCompletionOrThrow()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(path, "{\"Language\":\"en-US\"}");
        var service = new SettingsService(path);
        service.Load();
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        service.MarkInitialLaunchCompleted();

        Assert.False(service.Current.InitialLaunchCompleted);
        Assert.False(service.InitialLaunchCompletionPersisted);
        Assert.False(string.IsNullOrWhiteSpace(service.PersistenceError));
    }

    [Fact]
    public void Load_WhenInvalidFileCannotBeBackedUp_ProtectsOriginalFromSave()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        const string original = "{ invalid json";
        File.WriteAllText(path, original);
        // Allow reading the malformed JSON but deny the delete sharing required
        // for File.Move, so backup failure is deterministic without clock timing.
        using var readLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var service = new SettingsService(path);

        service.Load();
        var exception = Record.Exception(service.Save);

        Assert.True(service.IsReadOnlyDueToLoadFailure);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(original, File.ReadAllText(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SpeakerHop.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
