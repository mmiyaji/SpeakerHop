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

public sealed class MainWindow : Window
{
    private sealed record SelectOption(string Value, string Label);

    private const string DonationUrl = "https://www.buymeacoffee.com/erumdoor";
    private const string DonationBannerUrl = "https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png";
    private const string DevicesTag = "devices";
    private const string ShortcutsTag = "shortcuts";
    private const string SettingsTag = "settings";
    private const string DonationTag = "donation";
    private const string AboutTag = "about";

    private SolidColorBrush PageBrush => IsLightPalette()
        ? new SolidColorBrush(Color.FromArgb(255, 243, 243, 243))
        : new SolidColorBrush(Color.FromArgb(255, 15, 15, 15));
    private SolidColorBrush PanelBrush => IsLightPalette()
        ? new SolidColorBrush(Colors.White)
        : new SolidColorBrush(Color.FromArgb(255, 31, 31, 31));
    private SolidColorBrush StrokeBrush => IsLightPalette()
        ? new SolidColorBrush(Color.FromArgb(255, 218, 218, 218))
        : new SolidColorBrush(Color.FromArgb(255, 62, 62, 62));
    private SolidColorBrush TextBrush => IsLightPalette()
        ? new SolidColorBrush(Color.FromArgb(255, 28, 28, 28))
        : new SolidColorBrush(Colors.White);
    private SolidColorBrush SecondaryTextBrush => IsLightPalette()
        ? new SolidColorBrush(Color.FromArgb(255, 92, 92, 92))
        : new SolidColorBrush(Color.FromArgb(255, 190, 190, 190));

    private readonly Grid _root = new();
    private readonly NavigationView _navigation = new();
    private readonly ScrollViewer _contentScroll = new();
    private readonly StackPanel _content = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<AudioDeviceInfo> _devices = [];
    private readonly Dictionary<string, bool> _cycleSelection = new(StringComparer.OrdinalIgnoreCase);
    private Grid? _titleBar;
    private TextBlock? _titleText;
    private string _currentPageTag = DevicesTag;
    private bool _hasUnsavedHotkeyChanges;
    private List<HotkeyDefinition>? _hotkeyDraft;
    private int _automationSequence;
    private int _layoutMode = -1;
    private ContentDialog? _captureDialog;
    private bool _captureCancelled;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    public IntPtr Hwnd { get; }

    public MainWindow()
    {
        Hwnd = WindowNative.GetWindowHandle(this);
        Title = "SpeakerHop";
        BuildShell();
        ConfigureWindow();
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && _captureDialog is not null)
            {
                _captureCancelled = true;
                _captureDialog.Hide();
            }
        };
        _root.SizeChanged += (_, _) => UpdateResponsiveLayout();
    }

    public void ShowPersistenceError(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            SetStatus(string.Format(T("settings.persistenceError"), error));
    }

    public void ShowStartupHotkeyFailures(IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        if (failures.Count > 0)
            SetStatus(string.Format(T("shortcuts.registrationFailureDetails"), failures.Count,
                string.Join(", ", failures.Select(HotkeyFailureText))));
    }

    private void RestoreStartupHotkeyStatus()
    {
        if (AppServices.Settings.PersistenceError is { } error)
            ShowPersistenceError(error);
        else if (AppServices.StartupHotkeyFailures.Count > 0)
            ShowStartupHotkeyFailures(AppServices.StartupHotkeyFailures);
    }

    public void ShowSettings()
    {
        SelectPage(SettingsTag);
        Activate();
        AppServices.Windowing.BringToFront();
    }

    public void ShowHome()
    {
        ShowPage(DevicesTag);
    }

    private void BuildShell()
    {
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();

        _root.Background = PageBrush;
        ApplyTheme();
        AddFallbackWinUiResources(_root.Resources);
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _titleBar = BuildTitleBar();
        _root.Children.Add(_titleBar);
        SetTitleBar(_titleBar);

        _contentScroll.Content = _content;
        _contentScroll.Padding = new Thickness(20, 16, 20, 20);
        AddFallbackWinUiResources(_contentScroll.Resources);

        _navigation.Background = PageBrush;
        AddFallbackWinUiResources(_navigation.Resources);
        _navigation.PaneTitle = "SpeakerHop";
        _navigation.IsSettingsVisible = false;
        _navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
        // Keep the icon rail visible on narrow desktop windows. Minimal mode
        // overlays the pane toggle on this page's own heading.
        _navigation.CompactModeThresholdWidth = 0;
        _navigation.ExpandedModeThresholdWidth = 1000;
        _navigation.OpenPaneLength = 220;
        _navigation.CompactPaneLength = 48;
        BuildNavigationItems();
        _navigation.Content = _contentScroll;
        _navigation.SelectionChanged += Navigation_SelectionChanged;
        SetAutomation(_navigation, "Navigation.Main", "SpeakerHop");
        SetAutomation(_status, "Status.Current", "Status");
        Grid.SetRow(_navigation, 1);
        _root.Children.Add(_navigation);

        Content = _root;
    }

    private void BuildNavigationItems()
    {
        _navigation.MenuItems.Clear();
        _navigation.MenuItems.Add(NavItem(T("nav.devices"), DevicesTag, "\uE995"));
        _navigation.MenuItems.Add(NavItem(T("nav.shortcuts"), ShortcutsTag, "\uE765"));
        _navigation.MenuItems.Add(NavItem(T("nav.settings"), SettingsTag, "\uE713"));
        _navigation.MenuItems.Add(NavItem(T("nav.donation"), DonationTag, "\uE8D7"));
        _navigation.MenuItems.Add(NavItem(T("nav.about"), AboutTag, "\uE946"));
    }

    private Grid BuildTitleBar()
    {
        var titleBar = new Grid
        {
            Background = PageBrush,
            Height = 48,
            Padding = new Thickness(16, 0, 140, 0)
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new Image
        {
            Width = 18,
            Height = 18,
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "App.png"))),
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleText = new TextBlock
        {
            Text = "SpeakerHop",
            Foreground = TextBrush,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        Grid.SetColumn(_titleText, 1);
        titleBar.Children.Add(icon);
        titleBar.Children.Add(_titleText);
        return titleBar;
    }

    private string T(string key)
    {
        return LocalizationService.Text(key, AppServices.Settings.Current.Language);
    }

    private bool IsLightPalette()
    {
        var theme = AppServices.Settings.Current.Theme;
        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Application.Current.RequestedTheme == ApplicationTheme.Light;
    }

    private ElementTheme ConfiguredElementTheme()
    {
        var theme = AppServices.Settings.Current.Theme;
        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            return ElementTheme.Light;
        }

        if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return ElementTheme.Dark;
        }

        return ElementTheme.Default;
    }

    private void ApplyTheme()
    {
        _root.RequestedTheme = ConfiguredElementTheme();
        _root.Background = PageBrush;
        _navigation.Background = PageBrush;
        _status.Foreground = SecondaryTextBrush;

        if (_titleBar is not null)
        {
            _titleBar.Background = PageBrush;
        }

        if (_titleText is not null)
        {
            _titleText.Foreground = TextBrush;
        }

        UpdateTitleBarColors();
    }

    private int CurrentLayoutMode()
    {
        var width = _root.ActualWidth;
        if (width <= 0 || width >= 900)
        {
            return 0;
        }

        return width >= 620 ? 1 : 2;
    }

    private void UpdateResponsiveLayout()
    {
        var mode = CurrentLayoutMode();
        if (mode == _layoutMode)
        {
            return;
        }

        _layoutMode = mode;
        _contentScroll.Padding = mode switch
        {
            0 => new Thickness(20, 16, 20, 20),
            1 => new Thickness(16, 14, 16, 16),
            _ => new Thickness(12, 12, 12, 14)
        };

        if (_content.Children.Count != 0)
        {
            ShowPage(_currentPageTag);
        }
    }

    private static NavigationViewItem NavItem(string text, string tag, string glyph)
    {
        var item = new NavigationViewItem
        {
            Content = text,
            Tag = tag,
            Icon = new FontIcon { Glyph = glyph }
        };
        AddFallbackWinUiResources(item.Resources);
        SetAutomation(item, $"Navigation.{tag}", text);
        return item;
    }

    private static void AddFallbackWinUiResources(ResourceDictionary resources)
    {
        var transparent = new SolidColorBrush(Colors.Transparent);
        var hover = new SolidColorBrush(Color.FromArgb(255, 45, 45, 45));
        var pressed = new SolidColorBrush(Color.FromArgb(255, 62, 62, 62));
        var text = new SolidColorBrush(Colors.White);
        var disabled = new SolidColorBrush(Color.FromArgb(255, 120, 120, 120));

        resources["TabViewScrollButtonBackground"] = transparent;
        resources["TabViewScrollButtonBackgroundPressed"] = pressed;
        resources["TabViewScrollButtonBackgroundPointerOver"] = hover;
        resources["TabViewScrollButtonBackgroundDisabled"] = transparent;
        resources["TabViewScrollButtonForeground"] = text;
        resources["TabViewScrollButtonForegroundPressed"] = text;
        resources["TabViewScrollButtonForegroundPointerOver"] = text;
        resources["TabViewScrollButtonForegroundDisabled"] = disabled;
        resources["TabViewScrollButtonBorderBrush"] = transparent;
        resources["TabViewScrollButtonBorderBrushPressed"] = transparent;
        resources["TabViewScrollButtonBorderBrushPointerOver"] = transparent;
        resources["TabViewScrollButtonBorderBrushDisabled"] = transparent;
        resources["TabViewButtonBackground"] = transparent;
        resources["TabViewButtonBackgroundPressed"] = pressed;
        resources["TabViewButtonBackgroundPointerOver"] = hover;
        resources["TabViewButtonBackgroundDisabled"] = transparent;
        resources["TabViewButtonBackgroundActiveTab"] = transparent;
        resources["ControlCornerRadius"] = new CornerRadius(4);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            ShowPage(tag);
        }
    }

    private void SelectPage(string tag)
    {
        var item = _navigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(entry => string.Equals(entry.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            if (ReferenceEquals(_navigation.SelectedItem, item))
            {
                ShowPage(tag);
                return;
            }
            _navigation.SelectedItem = item;
            return;
        }

        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        _currentPageTag = tag;
        _automationSequence = 0;
        SetStatus("");
        _status.Foreground = SecondaryTextBrush;
        switch (tag)
        {
            case DevicesTag:
                ShowDevicesPage();
                break;
            case ShortcutsTag:
                ShowShortcutsPage();
                break;
            case SettingsTag:
                ShowSettingsPage();
                break;
            case DonationTag:
                ShowDonationPage();
                break;
            case AboutTag:
                ShowAboutPage();
                break;
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
        _content.Children.Add(_status);
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
        var panel = new Grid { ColumnSpacing = 12 };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
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
        var namePanel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
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
                ? device.IsDefault ? T("common.default") : ""
                : T("devices.disconnected"),
            Foreground = SecondaryTextBrush,
            Width = 104,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var switchButton = new Button
        {
            Content = T("devices.switch"),
            IsEnabled = device.IsConnected && !device.IsDefault,
            MinWidth = 64,
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
            Spacing = 8,
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

        var row = new Grid { ColumnSpacing = 10 };
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
            actions.Margin = new Thickness(0, 10, 0, 0);
        }
        row.Children.Add(checkBox);
        row.Children.Add(namePanel);
        row.Children.Add(defaultBadge);
        row.Children.Add(actions);

        checkBox.Checked += (_, _) => UpdateDeviceSelection(device.Id, true);
        checkBox.Unchecked += (_, _) => UpdateDeviceSelection(device.Id, false);

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
        var proposed = AppServices.Settings.Current.Hotkeys.Select(CloneHotkey).ToList();
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

        IReadOnlyList<HotkeyRegistrationFailure> failures;
        try { failures = AppServices.Hotkeys.SaveAndReload(proposed); }
        catch (Exception ex) { ShowPersistenceError(ex.Message); return; }
        AppServices.SetStartupHotkeyFailures(failures);
        if (failures.Count != 0)
        {
            SetStatus(string.Format(T("shortcuts.registrationFailed"), failures.Count,
                string.Join(", ", failures.Select(HotkeyFailureText))));
            return;
        }

        if (!_hasUnsavedHotkeyChanges)
        {
            _hotkeyDraft = ShortcutDraftService.CreateRows(AppServices.Settings.Current.Hotkeys);
            _hasUnsavedHotkeyChanges = false;
        }
        ShowDevicesPage();
        SetStatus(T("shortcuts.saved"));
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

    private void ShowShortcutsPage()
    {
        _hotkeyDraft ??= ShortcutDraftService.CreateRows(AppServices.Settings.Current.Hotkeys);
        _content.Children.Clear();
        AddHeader(T("shortcuts.title"), T("shortcuts.subtitle"));
        if (_hasUnsavedHotkeyChanges)
        {
            SetStatus(T("shortcuts.unsaved"));
        }
        _content.Children.Add(_status);
        RestoreStartupHotkeyStatus();
        var saveButton = InlineButton(T("shortcuts.save"), SaveHotkeys, "Shortcuts.Save", T("shortcuts.save"));
        var saveBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 6), Children = { saveButton } };
        _content.Children.Add(saveBar);

        var header = new Grid { ColumnSpacing = 8, Margin = new Thickness(8, 0, 8, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
        header.Children.Add(TextBlock(T("shortcuts.enabled"), 12, SecondaryTextBrush));
        var actionHeading = TextBlock(T("shortcuts.actionLabel"), 12, SecondaryTextBrush);
        Grid.SetColumn(actionHeading, 1);
        header.Children.Add(actionHeading);
        var keyHeading = TextBlock(T("shortcuts.keyLabel"), 12, SecondaryTextBrush);
        Grid.SetColumn(keyHeading, 2);
        header.Children.Add(keyHeading);
        var settingHeading = TextBlock(T("shortcuts.configuration"), 12, SecondaryTextBrush);
        Grid.SetColumn(settingHeading, 3);
        header.Children.Add(settingHeading);
        if (CurrentLayoutMode() != 2)
            _content.Children.Add(header);

        foreach (var action in Enum.GetValues<HotkeyActionType>())
        {
            var rows = _hotkeyDraft.Where(hotkey => hotkey.Action == action).ToList();
            var group = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var hotkey in rows)
                group.Children.Add(HotkeyRow(hotkey, rows, ReferenceEquals(hotkey, rows[0])));
            _content.Children.Add(group);
        }

    }

    private UIElement HotkeyRow(HotkeyDefinition hotkey, IReadOnlyCollection<HotkeyDefinition> actionRows, bool firstRow)
    {
        var draft = _hotkeyDraft ?? throw new InvalidOperationException("Hotkey draft is not initialized.");
        var narrow = CurrentLayoutMode() == 2;
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 4, MinHeight = 42 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(narrow ? 120 : 150) });
        if (!narrow) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(narrow ? 68 : 68) });
        if (narrow) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var enabled = new CheckBox
        {
            IsChecked = hotkey.Enabled,
            MinWidth = 0,
            Width = 32,
            IsEnabled = hotkey.Key is >= 1 and <= 0xFF && !IsModifierKey(hotkey.Key),
            VerticalAlignment = VerticalAlignment.Center
        };
        SetAutomation(enabled, $"Shortcuts.Enabled.{hotkey.Id}", $"{ActionName(hotkey.Action)} {T("shortcuts.enabled")}");
        enabled.Checked += (_, _) => { if (!hotkey.Enabled) { hotkey.Enabled = true; MarkHotkeysDirty(); } };
        enabled.Unchecked += (_, _) => { if (hotkey.Enabled) { hotkey.Enabled = false; MarkHotkeysDirty(); } };
        var actionLabel = new TextBlock
        {
            Text = ActionName(hotkey.Action),
            Foreground = TextBrush,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        var keyButton = new Button
        {
            Content = hotkey.Key == 0 ? T("shortcuts.unassigned") : hotkey.GestureText,
            MinWidth = narrow ? 98 : 118,
            MaxWidth = narrow ? 142 : 160,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        SetAutomation(keyButton, $"Shortcuts.Key.{hotkey.Id}", $"{ActionName(hotkey.Action)}: {keyButton.Content}");
        ToolTipService.SetToolTip(keyButton, T("shortcuts.key"));
        keyButton.Click += async (_, _) =>
        {
            if (await CaptureGestureAsync(hotkey, draft)) { MarkHotkeysDirty(); ShowShortcutsPage(); }
        };
        FrameworkElement? setting = null;
        if (hotkey.Action == HotkeyActionType.SetDevice)
        {
            var options = AppServices.Commands.GetDevices().Select(device => new SelectOption(device.Id,
                $"{device.Name}{(device.IsConnected ? "" : $" ({T("devices.disconnected")})")}")).ToList();
            if (!string.IsNullOrWhiteSpace(hotkey.DeviceId) && options.All(option => option.Value != hotkey.DeviceId))
                options.Add(new SelectOption(hotkey.DeviceId, $"{DeviceName(hotkey.DeviceId)} ({T("devices.disconnected")})"));
            if (string.IsNullOrWhiteSpace(hotkey.DeviceId))
                options.Insert(0, new SelectOption("", T("shortcuts.chooseDevice")));
            var combo = new ComboBox
            {
                ItemsSource = options,
                DisplayMemberPath = nameof(SelectOption.Label),
                SelectedItem = options.FirstOrDefault(option => option.Value == hotkey.DeviceId) ?? options[0],
                MinWidth = 112,
                MaxWidth = 150,
                IsEnabled = hotkey.Key is >= 1 and <= 0xFF && !IsModifierKey(hotkey.Key),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SetAutomation(combo, $"Shortcuts.Target.{hotkey.Id}", T("shortcuts.target"));
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is SelectOption option && hotkey.DeviceId != option.Value)
                { hotkey.DeviceId = option.Value; MarkHotkeysDirty(); }
            };
            setting = combo;
        }
        else if (hotkey.Action is HotkeyActionType.VolumeUp or HotkeyActionType.VolumeDown)
        {
            var number = new NumberBox { Value = Math.Clamp(hotkey.VolumeStep, 1, 100), Minimum = 1, Maximum = 100,
                SmallChange = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 78,
                IsEnabled = hotkey.Key is >= 1 and <= 0xFF && !IsModifierKey(hotkey.Key) };
            SetAutomation(number, $"Shortcuts.Step.{hotkey.Id}", T("shortcuts.volumeStep"));
            number.ValueChanged += (_, _) =>
            {
                if (double.IsNaN(number.Value)) return;
                var next = (int)Math.Clamp(Math.Round(number.Value), 1, 100);
                if (hotkey.VolumeStep != next)
                {
                    hotkey.VolumeStep = next;
                    if (number.Value != next) number.Value = next;
                    MarkHotkeysDirty();
                }
            };
            setting = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { number, TextBlock("%", 13, SecondaryTextBrush) }
            };
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right };
        if (firstRow)
            actions.Children.Add(InlineButton("\uE710", () => AddShortcut(hotkey.Action),
                $"Shortcuts.Add.{hotkey.Action}", string.Format(T("shortcuts.addBindingFor"), ActionName(hotkey.Action))));
        var remove = InlineButton("\uE74D", () =>
        {
            if (actionRows.Count > 1) draft.Remove(hotkey);
            else { hotkey.Key = 0; hotkey.Modifiers = 0; hotkey.Enabled = false; }
            MarkHotkeysDirty(); ShowShortcutsPage();
        }, $"Shortcuts.Clear.{hotkey.Id}", T("shortcuts.clear"));
        remove.IsEnabled = hotkey.Key != 0 || actionRows.Count > 1;
        actions.Children.Add(remove);
        Grid.SetColumn(enabled, 0); Grid.SetColumn(actionLabel, 1);
        grid.Children.Add(enabled); grid.Children.Add(actionLabel);
        if (!narrow)
        {
            Grid.SetColumn(keyButton, 2); Grid.SetColumn(actions, 4);
            grid.Children.Add(keyButton); grid.Children.Add(actions);
            if (setting is not null) { Grid.SetColumn(setting, 3); grid.Children.Add(setting); }
        }
        else
        {
            Grid.SetColumn(keyButton, 2); Grid.SetColumn(actions, 3);
            grid.Children.Add(keyButton); grid.Children.Add(actions);
            if (setting is not null)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(setting, 1); Grid.SetColumn(setting, 1); Grid.SetColumnSpan(setting, 3); grid.Children.Add(setting);
            }
        }
        return CompactRow(grid);
    }

    private void AddShortcut(HotkeyActionType action)
    {
        _hotkeyDraft ??= ShortcutDraftService.CreateRows(AppServices.Settings.Current.Hotkeys);
        _hotkeyDraft.Add(new HotkeyDefinition
        {
            Name = string.Empty, Action = action, DeviceId = action == HotkeyActionType.SetDevice ? AppServices.Commands.GetDevices().FirstOrDefault()?.Id : null,
            Modifiers = 0, Key = 0, Enabled = false
        });
        MarkHotkeysDirty();
        ShowShortcutsPage();
    }

    private void SaveHotkeys()
    {
        _hotkeyDraft ??= ShortcutDraftService.CreateRows(AppServices.Settings.Current.Hotkeys);
        if (_hotkeyDraft.Any(hotkey => hotkey.Enabled && hotkey.Key != 0 &&
            hotkey.Action == HotkeyActionType.SetDevice && string.IsNullOrWhiteSpace(hotkey.DeviceId)))
        {
            SetStatus(T("shortcuts.targetRequired"));
            return;
        }
        IReadOnlyList<HotkeyRegistrationFailure> failures;
        try
        {
            failures = AppServices.Hotkeys.SaveAndReload(ShortcutDraftService.ToSettings(_hotkeyDraft));
            AppServices.SetStartupHotkeyFailures(failures);
        }
        catch (Exception ex)
        {
            _hasUnsavedHotkeyChanges = true;
            ShowPersistenceError(ex.Message);
            return;
        }

        _hasUnsavedHotkeyChanges = failures.Count != 0;
        if (failures.Count == 0)
        {
            _hotkeyDraft = ShortcutDraftService.CreateRows(AppServices.Settings.Current.Hotkeys);
            ShowShortcutsPage();
            SetStatus(T("shortcuts.saved"));
            return;
        }
        SetStatus(failures.Count == 0
            ? T("shortcuts.saved")
            : string.Format(T("shortcuts.registrationFailed"), failures.Count, string.Join(", ", failures.Select(HotkeyFailureText))));
    }

    private void MarkHotkeysDirty()
    {
        _hasUnsavedHotkeyChanges = true;
        SetStatus(T("shortcuts.unsaved"));
    }

    private async Task<bool> CaptureGestureAsync(
        HotkeyDefinition hotkey,
        IReadOnlyCollection<HotkeyDefinition> hotkeys)
    {
        var gesture = new TextBlock
        {
            Text = hotkey.GestureText,
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 4)
        };
        var hint = new TextBlock
        {
            Text = T("shortcuts.captureHint"),
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.OrangeRed),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var content = new StackPanel
        {
            Spacing = 6,
            MinWidth = 380,
            Children = { hint, gesture, error }
        };
        var dialog = new ContentDialog
        {
            XamlRoot = _root.XamlRoot,
            Title = T("shortcuts.key"),
            Content = content,
            PrimaryButtonText = T("shortcuts.save"),
            CloseButtonText = T("common.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = hotkey.Key is >= 1 and <= 0xFF && !IsModifierKey(hotkey.Key)
        };
        SetAutomation(dialog, "Shortcuts.CaptureDialog", T("shortcuts.key"));
        _captureDialog = dialog;
        _captureCancelled = false;

        var capturedModifiers = hotkey.Modifiers;
        var capturedKey = hotkey.Key;
        void ApplyCapturedKey(uint key, uint modifiers)
        {
            if (IsModifierKey(key))
            {
                gesture.Text = ModifierText(modifiers);
                return;
            }

            capturedModifiers = modifiers;
            capturedKey = key;
            var duplicate = hotkeys.Any(entry =>
                entry.Id != hotkey.Id && entry.Enabled &&
                entry.Modifiers == capturedModifiers && entry.Key == capturedKey);
            var preview = CloneHotkey(hotkey);
            preview.Modifiers = capturedModifiers;
            preview.Key = capturedKey;
            gesture.Text = preview.GestureText;
            error.Text = duplicate
                ? string.Format(T("shortcuts.registrationFailed"), 1, preview.GestureText)
                : "";
            dialog.IsPrimaryButtonEnabled = capturedKey is >= 1 and <= 0xFF && !IsModifierKey(capturedKey) && !duplicate;
        }

        uint pressedModifiers = CurrentModifiers();
        var consumedKeys = new HashSet<uint>();
        LowLevelKeyboardProc hookCallback = (code, wParam, lParam) =>
        {
            const int keyDown = 0x0100;
            const int keyUp = 0x0101;
            const int sysKeyDown = 0x0104;
            const int sysKeyUp = 0x0105;
            var message = wParam.ToInt32();
            if (code < 0 || GetForegroundWindow() != Hwnd)
            {
                return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
            }
            if (code >= 0 && message is keyDown or sysKeyDown)
            {
                var key = unchecked((uint)Marshal.ReadInt32(lParam));
                if (key is 0x09 or 0x0D or 0x1B)
                {
                    return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                }
                var modifier = ModifierFlag(key);
                if (modifier != 0)
                {
                    pressedModifiers |= modifier;
                    if (modifier != 0x0008)
                    {
                        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                    }
                }
                consumedKeys.Add(key);
                var modifiers = pressedModifiers;
                DispatcherQueue.TryEnqueue(() => ApplyCapturedKey(key, modifiers));
                return new IntPtr(1);
            }

            if (code >= 0 && message is keyUp or sysKeyUp)
            {
                var key = unchecked((uint)Marshal.ReadInt32(lParam));
                var modifier = ModifierFlag(key);
                pressedModifiers &= ~modifier;
                if (modifier != 0 && modifier != 0x0008)
                    return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
                return consumedKeys.Remove(key)
                    ? new IntPtr(1)
                    : CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
            }

            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        };
        var hook = SetWindowsHookEx(13, hookCallback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            _captureDialog = null;
            ShowPersistenceError($"Keyboard capture could not start ({Marshal.GetLastWin32Error()}).");
            return false;
        }

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowPersistenceError(ex.Message);
            return false;
        }
        finally
        {
            _captureDialog = null;
            UnhookWindowsHookEx(hook);
            GC.KeepAlive(hookCallback);
        }

        if (_captureCancelled || result != ContentDialogResult.Primary)
        {
            return false;
        }

        hotkey.Modifiers = capturedModifiers;
        hotkey.Key = capturedKey;
        hotkey.Enabled = true;
        return true;
    }

    private static uint CurrentModifiers()
    {
        var keys = System.Windows.Forms.Control.ModifierKeys;
        uint modifiers = 0;
        if ((keys & System.Windows.Forms.Keys.Alt) != 0) modifiers |= 0x0001;
        if ((keys & System.Windows.Forms.Keys.Control) != 0) modifiers |= 0x0002;
        if ((keys & System.Windows.Forms.Keys.Shift) != 0) modifiers |= 0x0004;
        if (GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0) modifiers |= 0x0008;
        return modifiers;
    }

    private static bool IsModifierKey(uint key) => key is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C or
        0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    private static uint ModifierFlag(uint key) => key switch
    {
        0x12 or 0xA4 or 0xA5 => 0x0001,
        0x11 or 0xA2 or 0xA3 => 0x0002,
        0x10 or 0xA0 or 0xA1 => 0x0004,
        0x5B or 0x5C => 0x0008,
        _ => 0
    };

    private static string ModifierText(uint modifiers)
    {
        var preview = new HotkeyDefinition { Modifiers = modifiers, Key = 0 };
        return preview.GestureText.Replace(" + VK 0x00", "", StringComparison.Ordinal);
    }

    private void ShowSettingsPage()
    {
        _content.Children.Clear();
        AddHeader(T("settings.title"), T("settings.subtitle"));
        _content.Children.Add(_status);
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
        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        var narrow = CurrentLayoutMode() == 2;
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(TextBlock(title, 16, TextBrush));
        text.Children.Add(TextBlock(description, 12, SecondaryTextBrush));

        var comboBox = new ComboBox
        {
            ItemsSource = options,
            DisplayMemberPath = nameof(SelectOption.Label),
            SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? options[0],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = narrow ? 160 : 200,
            MaxWidth = 260
        };
        AddFallbackWinUiResources(comboBox.Resources);
        SetAutomation(comboBox, $"Settings.Option.{++_automationSequence}", title);
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is SelectOption option &&
                !string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase))
            {
                changed(option.Value);
            }
        };

        if (narrow)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(comboBox, 1);
            Grid.SetColumnSpan(text, 2);
            Grid.SetColumnSpan(comboBox, 2);
            comboBox.HorizontalAlignment = HorizontalAlignment.Left;
            comboBox.Width = 240;
        }
        Grid.SetColumn(text, 0);
        Grid.SetColumn(comboBox, narrow ? 0 : 1);
        grid.Children.Add(text);
        grid.Children.Add(comboBox);
        return CompactRow(grid);
    }

    private UIElement SettingsToggleCard(
        string title,
        string description,
        bool isOn,
        Func<bool, Task<bool>> changed,
        Func<Task<bool?>>? getActualState = null)
    {
        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var narrow = CurrentLayoutMode() == 2;
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(TextBlock(title, 16, TextBrush));
        text.Children.Add(TextBlock(description, 12, SecondaryTextBrush));

        var toggle = new ToggleSwitch
        {
            IsOn = isOn,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            OnContent = T("common.on"),
            OffContent = T("common.off")
        };
        AddFallbackWinUiResources(toggle.Resources);
        SetAutomation(toggle, $"Settings.Toggle.{++_automationSequence}", title);

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

        if (narrow)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(toggle, 1);
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
        }
        Grid.SetColumn(text, 0);
        Grid.SetColumn(toggle, narrow ? 0 : 1);
        grid.Children.Add(text);
        grid.Children.Add(toggle);
        if (getActualState is not null)
        {
            _ = SyncActualStateAsync();
        }

        return CompactRow(grid);

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

    private void ShowAboutPage()
    {
        _content.Children.Clear();
        AddHeader(T("about.title"), T("about.subtitle"));
        _content.Children.Add(AppInfoCard());
        _content.Children.Add(PrivacyInfoCard());
        _content.Children.Add(ExitApplicationCard());
    }

    private void ShowDonationPage()
    {
        _content.Children.Clear();
        AddHeader(T("donation.title"), T("donation.subtitle"));

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(TextBlock(T("donation.message"), 13, SecondaryTextBrush));

        var fallbackText = new TextBlock
        {
            Text = T("donation.buyMeACoffee"),
            Padding = new Thickness(18, 12, 18, 12),
            Foreground = TextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        var bannerImage = new Image
        {
            Source = new BitmapImage(new Uri(DonationBannerUrl)),
            Width = 217,
            Height = 60,
            Stretch = Stretch.Uniform
        };
        var donationButton = new Button
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = bannerImage
        };
        SetAutomation(donationButton, "Donation.Open", T("donation.openBuyMeACoffee"));
        bannerImage.ImageFailed += (_, _) => donationButton.Content = fallbackText;
        donationButton.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(DonationUrl));
        ToolTipService.SetToolTip(donationButton, DonationUrl);
        panel.Children.Add(donationButton);

        var urlText = TextBlock(DonationUrl, 12, SecondaryTextBrush);
        panel.Children.Add(urlText);

        _content.Children.Add(CompactRow(panel));
    }

    private UIElement AppInfoCard()
    {
        var grid = new Grid
        {
            RowSpacing = 6,
            ColumnSpacing = 20
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddInfoRow(grid, 0, T("about.product"), "SpeakerHop");
        AddInfoRow(grid, 1, T("about.version"), AppVersion());
        AddInfoRow(grid, 2, T("about.author"), "@mmiyaji");
        AddInfoLinkRow(grid, 3, T("about.web"), "https://ruhenheim.org");

        return CompactRow(grid);
    }

    private UIElement PrivacyInfoCard()
    {
        return CompactRow(TextBlock(T("about.privacy"), 13, SecondaryTextBrush));
    }

    private UIElement ExitApplicationCard()
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = "\uE8BB",
            FontSize = 24,
            Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]),
            VerticalAlignment = VerticalAlignment.Center
        };
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(TextBlock(T("about.exitApplication"), 16, TextBrush));
        text.Children.Add(TextBlock(T("about.exitApplicationDescription"), 13, SecondaryTextBrush));

        var button = ActionButton(T("about.exitApplication"), AppServices.ExitApplication);
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(button, 2);
        grid.Children.Add(icon);
        grid.Children.Add(text);
        grid.Children.Add(button);
        return CompactRow(grid);
    }

    private void AddInfoRow(Grid grid, int row, string label, string value)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var labelBlock = TextBlock(label, 14, SecondaryTextBrush);
        var valueBlock = TextBlock(value, 14, TextBrush);
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        Grid.SetRow(valueBlock, row);
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
    }

    private void AddInfoLinkRow(Grid grid, int row, string label, string uri)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var labelBlock = TextBlock(label, 14, SecondaryTextBrush);
        var link = new HyperlinkButton
        {
            Content = uri,
            NavigateUri = new Uri(uri),
            Padding = new Thickness(0),
            MinHeight = 0
        };
        SetAutomation(link, $"About.Link.{row}", uri);
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);
        Grid.SetRow(link, row);
        Grid.SetColumn(link, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(link);
    }

    private static string AppVersion()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString() ?? "Unknown";
    }

    private void AddHeader(string title, string subtitle)
    {
        _content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = TextBrush,
            FontSize = 24,
            Margin = new Thickness(0, 0, 0, 4)
        });
        _content.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
    }

    private StackPanel ActionRow(params (string Label, Action Action)[] actions)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var action in actions)
        {
            var button = ActionButton(action.Label, action.Action);
            row.Children.Add(button);
        }

        return row;
    }

    private Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(8, 4, 8, 4),
            MinWidth = 0,
            MinHeight = 32,
            MaxWidth = 260
        };
        AddFallbackWinUiResources(button.Resources);
        SetAutomation(button, $"{_currentPageTag}.Action.{++_automationSequence}", text);
        button.Click += (_, _) => action();
        return button;
    }

    private UIElement Card(UIElement child, Action? click = null)
    {
        if (click is not null)
        {
            var button = new Button
            {
                Content = child,
                Background = PanelBrush,
                BorderBrush = StrokeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            AddFallbackWinUiResources(button.Resources);
            button.Click += (_, _) => click();
            return button;
        }

        return new Border
        {
            Background = PanelBrush,
            BorderBrush = StrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 6),
            Child = child
        };
    }

    private UIElement CompactRow(UIElement child)
    {
        return new Border
        {
            Background = PanelBrush,
            BorderBrush = StrokeBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 4),
            Child = child
        };
    }

    private Button InlineButton(string glyphOrText, Action action, string automationId, string accessibleName)
    {
        var button = new Button
        {
            Content = glyphOrText.Length == 1 && glyphOrText[0] >= '\uE000'
                ? new FontIcon { Glyph = glyphOrText, FontSize = 12 }
                : glyphOrText,
            Padding = new Thickness(6, 3, 6, 3),
            MinWidth = 30,
            MinHeight = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        AddFallbackWinUiResources(button.Resources);
        SetAutomation(button, automationId, accessibleName);
        ToolTipService.SetToolTip(button, accessibleName);
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock TextBlock(string text, double fontSize, Brush brush)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = brush,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
        AutomationProperties.SetName(_status, string.IsNullOrWhiteSpace(message) ? "Status" : message);
        _status.Margin = string.IsNullOrWhiteSpace(message)
            ? new Thickness(0)
            : new Thickness(0, 0, 0, 14);
    }

    private static HotkeyDefinition CloneHotkey(HotkeyDefinition hotkey) => new()
    {
        Id = hotkey.Id,
        Name = hotkey.Name,
        Action = hotkey.Action,
        DeviceId = hotkey.DeviceId,
        Modifiers = hotkey.Modifiers,
        Key = hotkey.Key,
        VolumeStep = hotkey.VolumeStep,
        Enabled = hotkey.Enabled
    };

    private static void SetAutomation(FrameworkElement element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id);
        AutomationProperties.SetName(element, name);
    }

    private string ActionName(HotkeyActionType action)
    {
        return action switch
        {
            HotkeyActionType.CycleDevice => T("action.cycleDevice"),
            HotkeyActionType.SetDevice => T("action.setDevice"),
            HotkeyActionType.VolumeUp => T("action.volumeUp"),
            HotkeyActionType.VolumeDown => T("action.volumeDown"),
            HotkeyActionType.MuteToggle => T("action.muteToggle"),
            HotkeyActionType.MicrophoneMuteToggle => T("action.microphoneMuteToggle"),
            _ => action.ToString()
        };
    }

    private string HotkeyFailureText(HotkeyRegistrationFailure failure)
    {
        return $"{failure.Hotkey.GestureText} ({T("shortcuts.errorCode")} {failure.ErrorCode})";
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

    private void ConfigureWindow()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(Hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var scale = Math.Max(1.0, GetDpiForWindow(Hwnd) / 96.0);
        appWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)Math.Round(1040 * scale),
            (int)Math.Round(720 * scale)));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico"));
        appWindow.Closing += (_, args) =>
        {
            if (AppServices.IsExiting)
            {
                return;
            }

            args.Cancel = true;
            AppServices.Windowing.Hide();
        };

        UpdateTitleBarColors(appWindow);
    }

    private void UpdateTitleBarColors(AppWindow? appWindow = null)
    {
        try
        {
            appWindow ??= AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Hwnd));
        }
        catch
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        if (new AccessibilitySettings().HighContrast)
        {
            titleBar.ButtonBackgroundColor = null;
            titleBar.ButtonInactiveBackgroundColor = null;
            titleBar.ButtonForegroundColor = null;
            titleBar.ButtonInactiveForegroundColor = null;
            titleBar.ButtonHoverBackgroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            return;
        }

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = IsLightPalette() ? Color.FromArgb(255, 32, 32, 32) : Colors.White;
        titleBar.ButtonInactiveForegroundColor = IsLightPalette()
            ? Color.FromArgb(255, 96, 96, 96)
            : Color.FromArgb(255, 150, 150, 150);
        titleBar.ButtonHoverBackgroundColor = IsLightPalette()
            ? Color.FromArgb(255, 230, 230, 230)
            : Color.FromArgb(255, 45, 45, 45);
        titleBar.ButtonPressedBackgroundColor = IsLightPalette()
            ? Color.FromArgb(255, 212, 212, 212)
            : Color.FromArgb(255, 62, 62, 62);
    }
}
