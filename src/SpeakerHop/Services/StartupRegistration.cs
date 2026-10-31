using Microsoft.Win32;
using Windows.ApplicationModel;

namespace SpeakerHop.Services;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpeakerHop";
    private const string StartupTaskId = "SpeakerHopStartup";

    public static async Task<StartupRegistrationResult> SetEnabledAsync(bool enabled)
    {
        if (IsPackaged())
        {
            return await SetPackagedStartupAsync(enabled);
        }

        return await Task.Run(() => SetRegistryStartup(enabled));
    }

    public static async Task<bool?> GetEnabledAsync()
    {
        if (IsPackaged())
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(StartupTaskId);
                return startupTask.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }
            catch
            {
                return null;
            }
        }

        return await Task.Run(GetRegistryStartupEnabled);
    }

    private static async Task<StartupRegistrationResult> SetPackagedStartupAsync(bool enabled)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                var state = await startupTask.RequestEnableAsync();
                return state switch
                {
                    StartupTaskState.Enabled => StartupRegistrationResult.Enabled,
                    StartupTaskState.EnabledByPolicy => StartupRegistrationResult.EnabledByPolicy,
                    StartupTaskState.DisabledByPolicy => StartupRegistrationResult.DisabledByPolicy,
                    StartupTaskState.DisabledByUser => StartupRegistrationResult.DisabledByUser,
                    _ => StartupRegistrationResult.Disabled
                };
            }

            startupTask.Disable();
            return StartupRegistrationResult.Disabled;
        }
        catch
        {
            return StartupRegistrationResult.Unsupported;
        }
    }

    private static StartupRegistrationResult SetRegistryStartup(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    return StartupRegistrationResult.Unsupported;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                if (key is null)
                {
                    return StartupRegistrationResult.Unsupported;
                }

                key.SetValue(ValueName, $"\"{path}\"");
                return GetRegistryStartupEnabled() == true
                    ? StartupRegistrationResult.Enabled
                    : StartupRegistrationResult.Unsupported;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return GetRegistryStartupEnabled() == false
                ? StartupRegistrationResult.Disabled
                : StartupRegistrationResult.Unsupported;
        }
        catch
        {
            return StartupRegistrationResult.Unsupported;
        }
    }

    private static bool? GetRegistryStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPackaged()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public enum StartupRegistrationResult
{
    Enabled,
    EnabledByPolicy,
    Disabled,
    DisabledByUser,
    DisabledByPolicy,
    Unsupported
}
