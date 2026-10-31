using SpeakerHop.Models;
using SpeakerHop.Services;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Color = Windows.UI.Color;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;

namespace SpeakerHop;

public sealed partial class MainWindow
{
    private sealed record SelectOption(string Value, string Label);

    private void ShowSettingsPage()
    {
        _content.Children.Clear();
        AddHeader(T("settings.title"), T("settings.subtitle"));
        RestoreStartupHotkeyStatus();
        _content.Children.Add(SettingsOptionCard(
            T("settings.language"),
            T("settings.languageDescription"),
            AppServices.Settings.Current.Language,
            LocalizationService.SupportedLanguages
                .Select(language => new SelectOption(language.Code, T(language.ResourceKey)))
                .ToList(),
            value =>
            {
                var previous = AppServices.Settings.Current.Language;
                try { AppServices.Settings.Current.Language = value; AppServices.Settings.Save(); }
                catch (Exception ex) { AppServices.Settings.Current.Language = previous; ShowPersistenceError(ex.Message); ShowSettingsPage(); return; }
                BuildNavigationItems();
                ApplyTheme();
                SelectPage(SettingsTag);
                SetStatus(T("settings.applied"));
            }));
        _content.Children.Add(SettingsOptionCard(
            T("settings.theme"),
            T("settings.themeDescription"),
            AppServices.Settings.Current.Theme,
            [
                new SelectOption("system", T("theme.system")),
                new SelectOption("light", T("theme.light")),
                new SelectOption("dark", T("theme.dark"))
            ],
            value =>
            {
                var previous = AppServices.Settings.Current.Theme;
                try { AppServices.Settings.Current.Theme = value; AppServices.Settings.Save(); }
                catch (Exception ex) { AppServices.Settings.Current.Theme = previous; ShowPersistenceError(ex.Message); ShowSettingsPage(); return; }
                ApplyTheme();
                ShowSettingsPage();
                SetStatus(T("settings.applied"));
            }));
        _content.Children.Add(SettingsToggleCard(
            T("settings.showMainWindowOnStartup"),
            T("settings.showMainWindowOnStartupDescription"),
            AppServices.Settings.Current.ShowMainWindowOnStartup,
            value =>
            {
                var previous = AppServices.Settings.Current.ShowMainWindowOnStartup;
                try { AppServices.Settings.Current.ShowMainWindowOnStartup = value; AppServices.Settings.Save(); }
                catch { AppServices.Settings.Current.ShowMainWindowOnStartup = previous; throw; }
                SetStatus(T("settings.applied"));
                return Task.FromResult(value);
            }));
        _content.Children.Add(SettingsToggleCard(
            T("settings.startWithWindows"),
            T("settings.startWithWindowsDescription"),
            AppServices.Settings.Current.StartWithWindows,
            SetStartWithWindowsAsync,
            StartupRegistration.GetEnabledAsync));
    }

    private UIElement SettingsOptionCard(
        string title,
        string description,
        string selectedValue,
        IReadOnlyList<SelectOption> options,
        Action<string> changed)
    {
        var comboBox = new ComboBox
        {
            ItemsSource = options,
            DisplayMemberPath = nameof(SelectOption.Label),
            SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? options[0],
            MinWidth = 160,
            MaxWidth = 260
        };
        AddFallbackWinUiResources(comboBox.Resources);
        var editorAutomationId = $"Settings.Option.{++_automationSequence}";
        SetAutomation(comboBox, editorAutomationId, title);
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is SelectOption option &&
                !string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase))
            {
                changed(option.Value);
            }
        };

        var row = new Controls.SettingsRow
        {
            Title = title,
            Description = description,
            Editor = comboBox,
            Padding = new Thickness(0),
            Margin = new Thickness(0)
        };
        SetAutomation(row, $"{editorAutomationId}.Row", title);
        return CompactRow(row);
    }

    private UIElement SettingsToggleCard(
        string title,
        string description,
        bool isOn,
        Func<bool, Task<bool>> changed,
        Func<Task<bool?>>? getActualState = null)
    {
        var toggle = new ToggleSwitch
        {
            IsOn = isOn,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            OnContent = T("common.on"),
            OffContent = T("common.off")
        };
        AddFallbackWinUiResources(toggle.Resources);
        var editorAutomationId = $"Settings.Toggle.{++_automationSequence}";
        SetAutomation(toggle, editorAutomationId, title);

        var suppressChange = false;
        var changeVersion = 0;
        toggle.Toggled += async (_, _) =>
        {
            if (suppressChange)
            {
                return;
            }

            var requested = toggle.IsOn;
            changeVersion++;
            toggle.IsEnabled = false;
            try
            {
                var actual = await changed(requested);
                suppressChange = true;
                toggle.IsOn = actual;
            }
            catch (Exception ex)
            {
                suppressChange = true;
                toggle.IsOn = !requested;
                ShowPersistenceError(ex.Message);
            }
            finally
            {
                suppressChange = false;
                toggle.IsEnabled = true;
            }
        };

        var row = new Controls.SettingsRow
        {
            Title = title,
            Description = description,
            Editor = toggle,
            Padding = new Thickness(0),
            Margin = new Thickness(0)
        };
        SetAutomation(row, $"{editorAutomationId}.Row", title);
        if (getActualState is not null)
        {
            _ = SyncActualStateAsync();
        }

        return CompactRow(row);

        async Task SyncActualStateAsync()
        {
            try
            {
                var version = changeVersion;
                var actual = await getActualState();
                if (actual is null || version != changeVersion)
                {
                    return;
                }

                suppressChange = true;
                toggle.IsOn = actual.Value;
                suppressChange = false;
                if (AppServices.Settings.Current.StartWithWindows != actual.Value)
                {
                    var previous = AppServices.Settings.Current.StartWithWindows;
                    AppServices.Settings.Current.StartWithWindows = actual.Value;
                    try { AppServices.Settings.Save(); }
                    catch (Exception ex) { AppServices.Settings.Current.StartWithWindows = previous; ShowPersistenceError(ex.Message); }
                }
            }
            catch (Exception ex)
            {
                suppressChange = false;
                ShowPersistenceError(ex.Message);
            }
        }
    }

    private async Task<bool> SetStartWithWindowsAsync(bool enabled)
    {
        if (AppServices.Settings.IsReadOnlyDueToLoadFailure)
        {
            throw new InvalidOperationException(AppServices.Settings.PersistenceError);
        }
        var previous = AppServices.Settings.Current.StartWithWindows;
        AppServices.Settings.Current.StartWithWindows = enabled;

        var result = await StartupRegistration.SetEnabledAsync(enabled);
        var actual = enabled;
        if (enabled && result is (StartupRegistrationResult.Disabled or
            StartupRegistrationResult.DisabledByPolicy or
            StartupRegistrationResult.DisabledByUser or
            StartupRegistrationResult.Unsupported))
        {
            actual = false;
        }
        else if (!enabled && result is StartupRegistrationResult.Unsupported)
        {
            actual = previous;
        }

        AppServices.Settings.Current.StartWithWindows = actual;
        try { AppServices.Settings.Save(); }
        catch
        {
            // Settings persistence and the external Windows registration must
            // remain consistent if saving fails after changing the OS state.
            await StartupRegistration.SetEnabledAsync(previous);
            AppServices.Settings.Current.StartWithWindows = previous;
            throw;
        }
        SetStatus(StartupRegistrationStatusText(enabled, result, actual));
        return actual;
    }

    private string StartupRegistrationStatusText(bool requested, StartupRegistrationResult result, bool actual)
    {
        return result switch
        {
            StartupRegistrationResult.Enabled or StartupRegistrationResult.EnabledByPolicy => T("settings.startWithWindowsEnabled"),
            StartupRegistrationResult.Disabled when !requested => T("settings.startWithWindowsDisabled"),
            StartupRegistrationResult.DisabledByUser => T("settings.startWithWindowsDisabledByUser"),
            StartupRegistrationResult.DisabledByPolicy => T("settings.startWithWindowsDisabledByPolicy"),
            StartupRegistrationResult.Unsupported => T("settings.startWithWindowsUnsupported"),
            _ when actual => T("settings.startWithWindowsEnabled"),
            _ => T("settings.startWithWindowsDisabled")
        };
    }
}
