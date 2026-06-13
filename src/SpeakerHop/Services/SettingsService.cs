using System.Text.Json;
using SpeakerHop.Models;

namespace SpeakerHop.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeakerHop");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            BackupInvalidSettings();
            Current = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, _serializerOptions));
    }

    private void BackupInvalidSettings()
    {
        var backupPath = $"{_settingsPath}.{DateTimeOffset.Now:yyyyMMddHHmmss}.invalid";
        File.Move(_settingsPath, backupPath, overwrite: true);
    }

    public void MarkInitialLaunchCompleted()
    {
        if (Current.InitialLaunchCompleted)
        {
            return;
        }

        Current.InitialLaunchCompleted = true;
        Save();
    }
}
