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

        await Task.Run(() => SetRegistryStartup(enabled));
        return StartupRegistrationResult.Enabled;
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

    private static void SetRegistryStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                key.SetValue(ValueName, $"\"{path}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
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
