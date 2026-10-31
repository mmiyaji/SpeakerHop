using System.Text.Json;
using SpeakerHop.Models;

namespace SpeakerHop.Services;

public sealed class SettingsService
    : ISettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private bool _protectExistingFile;

    public SettingsService()
        : this(DefaultSettingsPath())
    {
    }

    public SettingsService(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException("Settings path must not be empty.", nameof(settingsPath));
        }

        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public AppSettings Current { get; private set; } = new();

    /// <summary>True when a read/backup failure means the existing settings file must be preserved.</summary>
    public bool IsReadOnlyDueToLoadFailure => _protectExistingFile;

    /// <summary>Most recent persistence failure, if any.</summary>
    public string? PersistenceError { get; private set; }

    /// <summary>Whether initial launch completion was successfully written to disk.</summary>
    public bool InitialLaunchCompletionPersisted { get; private set; }

    public void Load()
    {
        try
        {
            var json = File.ReadAllText(_settingsPath);
            Current = Normalize(JsonSerializer.Deserialize<AppSettings>(json));
            _protectExistingFile = false;
            PersistenceError = null;
            InitialLaunchCompletionPersisted = Current.InitialLaunchCompleted;
        }
        catch (JsonException)
        {
            if (!BackupInvalidSettings())
            {
                _protectExistingFile = true;
                PersistenceError ??= "Invalid settings could not be backed up; the existing file is protected.";
                Current = new AppSettings();
                InitialLaunchCompletionPersisted = false;
                return;
            }
            _protectExistingFile = false;
            Current = new AppSettings();
            InitialLaunchCompletionPersisted = false;
            TrySave();
        }
        catch (FileNotFoundException)
        {
            _protectExistingFile = false;
            PersistenceError = null;
            InitialLaunchCompletionPersisted = false;
            Current = new AppSettings();
            TrySave();
        }
        catch (DirectoryNotFoundException)
        {
            _protectExistingFile = false;
            PersistenceError = null;
            InitialLaunchCompletionPersisted = false;
            Current = new AppSettings();
            TrySave();
        }
        catch (IOException)
        {
            Current = new AppSettings();
            InitialLaunchCompletionPersisted = false;
            _protectExistingFile = true;
            PersistenceError = "Settings could not be read; saving is disabled until settings are loaded successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            Current = new AppSettings();
            InitialLaunchCompletionPersisted = false;
            _protectExistingFile = true;
            PersistenceError = "Settings could not be read; saving is disabled until settings are loaded successfully.";
        }
    }

    public void Save()
    {
        if (_protectExistingFile)
        {
            throw new InvalidOperationException("Settings could not be read or backed up. Load settings successfully before saving.");
        }
        Current = Normalize(Current);
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, Current, _serializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
            PersistenceError = null;
            InitialLaunchCompletionPersisted = Current.InitialLaunchCompleted;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private bool BackupInvalidSettings()
    {
        var backupPath = $"{_settingsPath}.{DateTimeOffset.Now:yyyyMMddHHmmss}.invalid";
        try
        {
            File.Move(_settingsPath, backupPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void TrySave()
    {
        try
        {
            Save();
        }
        catch (IOException)
        {
            PersistenceError = "Settings could not be saved.";
        }
        catch (UnauthorizedAccessException)
        {
            PersistenceError = "Settings could not be saved.";
        }
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();

        var supportedLanguage = LocalizationService.SupportedLanguages.FirstOrDefault(option =>
            string.Equals(option.Code, settings.Language, StringComparison.OrdinalIgnoreCase));
        settings.Language = supportedLanguage?.Code ?? LocalizationService.SystemLanguage;
        settings.Theme = settings.Theme?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "system"
        };

        settings.CycleDeviceIds = (settings.CycleDeviceIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.RememberedAudioDevices = (settings.RememberedAudioDevices ?? [])
            .OfType<RememberedAudioDevice>()
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .Select(device =>
            {
                device.Id = device.Id.Trim();
                device.Name = string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name.Trim();
                return device;
            })
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(device => device.LastSeen).First())
            .ToList();

        var defaultHotkeys = new AppSettings().Hotkeys;
        settings.Hotkeys = settings.Hotkeys is null
            ? defaultHotkeys
            : settings.Hotkeys.OfType<HotkeyDefinition>().ToList();
        var usedHotkeyIds = new HashSet<Guid>();
        foreach (var hotkey in settings.Hotkeys)
        {
            if (hotkey.Id == Guid.Empty || !usedHotkeyIds.Add(hotkey.Id))
            {
                hotkey.Id = Guid.NewGuid();
                usedHotkeyIds.Add(hotkey.Id);
            }

            hotkey.Name = string.IsNullOrWhiteSpace(hotkey.Name) ? "Shortcut" : hotkey.Name.Trim();
            hotkey.Action = Enum.IsDefined(hotkey.Action) ? hotkey.Action : HotkeyActionType.CycleDevice;
            hotkey.DeviceId = string.IsNullOrWhiteSpace(hotkey.DeviceId) ? null : hotkey.DeviceId.Trim();
            hotkey.Modifiers &= 0x000F;
            hotkey.Key = hotkey.Key is >= 1 and <= 0xFF ? hotkey.Key : 0x41;
            hotkey.VolumeStep = (int)Math.Clamp(Math.Abs((long)hotkey.VolumeStep), 1, 100);
        }

        return settings;
    }

    public void MarkInitialLaunchCompleted()
    {
        if (Current.InitialLaunchCompleted)
        {
            return;
        }

        Current.InitialLaunchCompleted = true;
        try
        {
            Save();
        }
        catch (IOException exception)
        {
            Current.InitialLaunchCompleted = false;
            InitialLaunchCompletionPersisted = false;
            PersistenceError = exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            Current.InitialLaunchCompleted = false;
            InitialLaunchCompletionPersisted = false;
            PersistenceError = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            Current.InitialLaunchCompleted = false;
            InitialLaunchCompletionPersisted = false;
            PersistenceError = exception.Message;
        }
    }

    private static string DefaultSettingsPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpeakerHop");
        return Path.Combine(directory, "settings.json");
    }
}
