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
    private readonly DispatcherTimer _currentOutputTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private void RefreshCurrentOutput()
    {
        if (_currentPageTag != DevicesTag || AppServices.IsExiting) return;
        try
        {
            var current = AppServices.Audio.GetRenderDevices().FirstOrDefault(device => device.IsDefault);
            var shown = _devices.FirstOrDefault(device => device.IsConnected && device.IsDefault);
            if (current?.Id != shown?.Id || current?.Name != shown?.Name) ShowDevicesPage();
        }
        catch (Exception)
        {
            // Clear an obsolete current output if the last endpoint disappeared.
            if (_devices.Any(device => device.IsConnected && device.IsDefault)) ShowDevicesPage();
        }
    }

    private void ShowDevicesPage()
    {
        LoadDeviceSelections();
        RenderDevicesPage();
    }

    private void RenderDevicesPage()
    {
        _content.Children.Clear();
        AddHeader(T("devices.title"), T("devices.subtitle"));


        RestoreStartupHotkeyStatus();
        _content.Children.Add(NotificationOptionRow());

        foreach (var device in _devices)
        {
            _content.Children.Add(DeviceRow(device));
        }

        _content.Children.Add(ActionRow(
            (T("devices.refresh"), ShowDevicesPage),
            (T("devices.cycleNow"), () =>
            {
                AppServices.Commands.CycleDevice();
                ShowDevicesPage();
            })));
    }

    private UIElement NotificationOptionRow()
    {
        var panel = new Grid { ColumnSpacing = Controls.UiMetrics.ControlGap };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = Controls.UiMetrics.LabelGap };
        text.Children.Add(TextBlock(T("devices.notification"), 16, TextBrush));
        text.Children.Add(TextBlock(T("devices.notificationDescription"), 12, SecondaryTextBrush));

        var toggle = new ToggleSwitch
        {
            IsOn = AppServices.Settings.Current.NotifyOnDeviceSwitch,
            OnContent = T("common.on"),
            OffContent = T("common.off"),
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddFallbackWinUiResources(toggle.Resources);
        SetAutomation(toggle, "Devices.Notifications", T("devices.notification"));
        var suppressChange = false;
        toggle.Toggled += (_, _) =>
        {
            if (suppressChange) return;
            var previous = AppServices.Settings.Current.NotifyOnDeviceSwitch;
            try
            {
                AppServices.Settings.Current.NotifyOnDeviceSwitch = toggle.IsOn;
                AppServices.Settings.Save();
                SetStatus(toggle.IsOn ? T("devices.notificationsEnabled") : T("devices.notificationsDisabled"));
            }
            catch (Exception ex)
            {
                AppServices.Settings.Current.NotifyOnDeviceSwitch = previous;
                suppressChange = true;
                toggle.IsOn = previous;
                suppressChange = false;
                ShowPersistenceError(ex.Message);
            }
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(toggle, 1);
        panel.Children.Add(text);
        panel.Children.Add(toggle);
        return CompactRow(panel);
    }

    private void LoadDeviceSelections()
    {
        _devices.Clear();
        _cycleSelection.Clear();
        try
        {
            foreach (var device in AppServices.Commands.GetDevices())
            {
                _devices.Add(device);
                _cycleSelection[device.Id] = device.IncludeInCycle;
            }

            if (_devices.Count == 0)
            {
                SetStatus(T("common.none"));
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private UIElement DeviceRow(AudioDeviceInfo device)
    {
        var selected = _cycleSelection.GetValueOrDefault(device.Id);
        var checkBox = new CheckBox
        {
            IsChecked = selected,
            MinWidth = 0,
            Width = 32,
            VerticalAlignment = VerticalAlignment.Center
        };
        SetAutomation(checkBox, $"Devices.Include.{device.Id}",
            $"{device.Name}. {T("devices.subtitle")}");
        var (primaryName, secondaryName) = SplitDeviceName(device.Name);
        var namePanel = new StackPanel { Spacing = Controls.UiMetrics.LabelGap, VerticalAlignment = VerticalAlignment.Center };
        namePanel.Children.Add(new TextBlock
        {
            Text = primaryName,
            Foreground = device.IsConnected ? TextBrush : SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(secondaryName))
        {
            namePanel.Children.Add(new TextBlock
            {
                Text = secondaryName,
                Foreground = SecondaryTextBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (!device.IsConnected && device.LastSeen is not null)
        {
            namePanel.Children.Add(new TextBlock
            {
                Text = string.Format(T("devices.lastSeen"), FormatLastSeen(device.LastSeen.Value)),
                Foreground = SecondaryTextBrush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var defaultBadge = new TextBlock
        {
            Text = device.IsConnected
                ? device.IsDefault ? T("devices.current") : ""
                : T("devices.disconnected"),
            Foreground = device.IsConnected && device.IsDefault
                ? TextBrush
                : SecondaryTextBrush,
            FontWeight = device.IsConnected && device.IsDefault
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var switchButton = new Button
        {
            Content = T("devices.switch"),
            IsEnabled = device.IsConnected && !device.IsDefault,
            MinWidth = 64,
            MinHeight = Controls.UiMetrics.ButtonMinHeight,
            Padding = Controls.UiMetrics.ButtonPadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddFallbackWinUiResources(switchButton.Resources);
        SetAutomation(switchButton, $"Devices.Switch.{device.Id}",
            $"{T("devices.switch")}: {device.Name}");
        switchButton.Click += (_, _) => SwitchToDevice(device);

        var assignedHotkey = AppServices.Settings.Current.Hotkeys.FirstOrDefault(hotkey =>
            hotkey.Action == HotkeyActionType.SetDevice &&
            string.Equals(hotkey.DeviceId, device.Id, StringComparison.OrdinalIgnoreCase));
        var shortcutButton = new Button
        {
            Content = assignedHotkey?.GestureText ?? T("shortcuts.key"),
            MinWidth = 112,
            MinHeight = Controls.UiMetrics.ButtonMinHeight,
            Padding = Controls.UiMetrics.ButtonPadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddFallbackWinUiResources(shortcutButton.Resources);
        SetAutomation(shortcutButton, $"Devices.Shortcut.{device.Id}",
            $"{T("shortcuts.key")}: {device.Name}");
        ToolTipService.SetToolTip(shortcutButton,
            assignedHotkey is null ? T("action.setDevice") : assignedHotkey.GestureText);
        shortcutButton.Click += async (_, _) => await ConfigureDeviceShortcutAsync(device);

        var layoutMode = CurrentLayoutMode();
        var actions = new StackPanel
        {
            Orientation = layoutMode == 2 ? Orientation.Vertical : Orientation.Horizontal,
            Spacing = Controls.UiMetrics.ControlGap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = layoutMode == 2 ? HorizontalAlignment.Stretch : HorizontalAlignment.Right
        };
        if (layoutMode == 2)
        {
            shortcutButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            switchButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        actions.Children.Add(shortcutButton);
        actions.Children.Add(switchButton);

        var row = new Grid { ColumnSpacing = Controls.UiMetrics.ControlGap };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(checkBox, 0);
        Grid.SetColumn(namePanel, 1);
        Grid.SetColumn(defaultBadge, 2);
        if (layoutMode == 0)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(actions, 3);
        }
        else
        {
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(actions, 1);
            Grid.SetColumn(actions, 1);
            Grid.SetColumnSpan(actions, 2);
            actions.Margin = new Thickness(0, 8, 0, 0);
        }
        row.Children.Add(checkBox);
        row.Children.Add(namePanel);
        row.Children.Add(defaultBadge);
        row.Children.Add(actions);

        checkBox.Checked += (_, _) => UpdateDeviceSelection(device.Id, true);
        checkBox.Unchecked += (_, _) => UpdateDeviceSelection(device.Id, false);

        if (device.IsConnected && device.IsDefault)
        {
            var accent = new AccessibilitySettings().HighContrast ? TextBrush : new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
            var highlighted = (Border)CompactRow(row);
            highlighted.BorderBrush = accent;
            highlighted.BorderThickness = new Thickness(2);
            return highlighted;
        }

        return CompactRow(row);
    }

    private void SwitchToDevice(AudioDeviceInfo device)
    {
        string? status = null;
        void CaptureStatus(object? sender, AudioCommandNotification notification) => status = notification.Message;

        AppServices.Commands.StatusChanged += CaptureStatus;
        try
        {
            AppServices.Commands.SetDevice(device.Id);
        }
        finally
        {
            AppServices.Commands.StatusChanged -= CaptureStatus;
        }

        ShowDevicesPage();
        if (!string.IsNullOrWhiteSpace(status))
        {
            SetStatus(status);
        }
    }

    private async Task ConfigureDeviceShortcutAsync(AudioDeviceInfo device)
    {
        var proposed = _hotkeyDraft.Select(CloneHotkey).ToList();
        var hotkey = proposed.FirstOrDefault(entry =>
            entry.Action == HotkeyActionType.SetDevice &&
            string.Equals(entry.DeviceId, device.Id, StringComparison.OrdinalIgnoreCase));
        if (hotkey is null)
        {
            hotkey = new HotkeyDefinition
            {
                Name = device.Name,
                Action = HotkeyActionType.SetDevice,
                DeviceId = device.Id,
                Enabled = true
            };
            proposed.Add(hotkey);
        }

        if (!await CaptureGestureAsync(hotkey, proposed))
        {
            return;
        }

        _hotkeyDraft.Clear();
        _hotkeyDraft.AddRange(proposed);
        SelectPage(ShortcutsTag);
        MarkHotkeysDirty();
    }
    private UIElement DeviceList()
    {
        var list = new StackPanel { Spacing = 0 };
        foreach (var device in _devices)
        {
            list.Children.Add(DeviceRow(device));
        }

        return list;
    }

    private void UpdateDeviceSelection(string deviceId, bool selected)
    {
        _cycleSelection[deviceId] = selected;
        SaveDeviceSelections();
    }

    private void SaveDeviceSelections()
    {
        var previous = AppServices.Settings.Current.CycleDeviceIds.ToList();
        var currentDeviceIds = _devices
            .Select(device => device.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedCurrentIds = _cycleSelection
            .Where(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var preservedOrder = AppServices.Settings.Current.CycleDeviceIds
            .Where(id => !currentDeviceIds.Contains(id) || selectedCurrentIds.Contains(id));
        var newlySelected = _devices
            .Select(device => device.Id)
            .Where(id => selectedCurrentIds.Contains(id))
            .Where(id => !AppServices.Settings.Current.CycleDeviceIds.Contains(id, StringComparer.OrdinalIgnoreCase));

        try
        {
            AppServices.Settings.Current.CycleDeviceIds = preservedOrder
                .Concat(newlySelected)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            AppServices.Settings.Save();
            SetStatus(T("devices.saved"));
        }
        catch (Exception ex)
        {
            AppServices.Settings.Current.CycleDeviceIds = previous;
            foreach (var device in _devices)
                _cycleSelection[device.Id] = previous.Contains(device.Id, StringComparer.OrdinalIgnoreCase);
            ShowDevicesPage();
            ShowPersistenceError(ex.Message);
        }
    }

    private static string FormatLastSeen(DateTimeOffset lastSeen)
    {
        return lastSeen.LocalDateTime.ToString("g");
    }

    private static (string Primary, string? Secondary) SplitDeviceName(string name)
    {
        var separatorIndex = name.IndexOf(" (", StringComparison.Ordinal);
        if (separatorIndex <= 0 || !name.EndsWith(')'))
        {
            return (name, null);
        }

        var primary = name[..separatorIndex].Trim();
        var secondary = name[(separatorIndex + 2)..^1].Trim();
        if (string.IsNullOrWhiteSpace(primary) || string.IsNullOrWhiteSpace(secondary))
        {
            return (name, null);
        }

        return (primary, secondary);
    }

    private string DeviceName(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return T("common.none");
        }

        return AppServices.Commands.GetDevices().FirstOrDefault(device => device.Id == deviceId)?.Name ?? T("device.missing");
    }
}

