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
