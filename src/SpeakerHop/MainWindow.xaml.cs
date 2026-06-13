using SpeakerHop.Models;
using SpeakerHop.Services;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

    public IntPtr Hwnd { get; }

    public MainWindow()
    {
        Hwnd = WindowNative.GetWindowHandle(this);
        Title = "SpeakerHop";
        BuildShell();
        ConfigureWindow();
    }

    public void ShowSettings()
    {
        SelectPage(SettingsTag);
        Activate();
        AppServices.Windowing.BringToFront();
    }

    public void ShowHome()
    {
        SelectPage(DevicesTag);
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
        _contentScroll.Padding = new Thickness(32, 28, 32, 32);
        AddFallbackWinUiResources(_contentScroll.Resources);

        _navigation.Background = PageBrush;
        AddFallbackWinUiResources(_navigation.Resources);
        _navigation.PaneTitle = "SpeakerHop";
        _navigation.IsSettingsVisible = false;
        _navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
        _navigation.OpenPaneLength = 220;
        _navigation.CompactPaneLength = 48;
        BuildNavigationItems();
        _navigation.Content = _contentScroll;
        _navigation.SelectionChanged += Navigation_SelectionChanged;
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

    private static NavigationViewItem NavItem(string text, string tag, string glyph)
    {
        var item = new NavigationViewItem
        {
            Content = text,
            Tag = tag,
            Icon = new FontIcon { Glyph = glyph }
        };
        AddFallbackWinUiResources(item.Resources);
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
            _navigation.SelectedItem = item;
        }

        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        _currentPageTag = tag;
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
        _content.Children.Add(NotificationOptionCard());

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

    private UIElement NotificationOptionCard()
    {
        var panel = new Grid { ColumnSpacing = 12 };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(TextBlock(T("devices.notification"), 16, TextBrush));
        text.Children.Add(TextBlock(T("devices.notificationDescription"), 13, SecondaryTextBrush));

        var toggle = new ToggleSwitch
        {
            IsOn = AppServices.Settings.Current.NotifyOnDeviceSwitch,
            OnContent = T("common.on"),
            OffContent = T("common.off"),
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.Toggled += (_, _) =>
        {
            AppServices.Settings.Current.NotifyOnDeviceSwitch = toggle.IsOn;
            AppServices.Settings.Save();
            SetStatus(AppServices.Settings.Current.NotifyOnDeviceSwitch
                ? T("devices.notificationsEnabled")
                : T("devices.notificationsDisabled"));
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(toggle, 1);
        panel.Children.Add(text);
        panel.Children.Add(toggle);
        return Card(panel);
    }

    private void LoadDeviceSelections()
    {
        _devices.Clear();
        _cycleSelection.Clear();
        foreach (var device in AppServices.Commands.GetDevices())
        {
            _devices.Add(device);
            _cycleSelection[device.Id] = device.IncludeInCycle;
        }
    }

    private UIElement DeviceRow(AudioDeviceInfo device)
    {
        var selected = _cycleSelection.GetValueOrDefault(device.Id);
        var checkBox = new CheckBox
        {
            IsChecked = selected,
            VerticalAlignment = VerticalAlignment.Center
        };
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

        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(checkBox, 0);
        Grid.SetColumn(namePanel, 1);
        Grid.SetColumn(defaultBadge, 2);
        row.Children.Add(checkBox);
        row.Children.Add(namePanel);
        row.Children.Add(defaultBadge);

        checkBox.Checked += (_, _) => UpdateDeviceSelection(device.Id, true);
        checkBox.Unchecked += (_, _) => UpdateDeviceSelection(device.Id, false);

        return Card(row);
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
        var currentDeviceIds = _devices
            .Select(device => device.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rememberedMissingSelections = AppServices.Settings.Current.CycleDeviceIds
            .Where(id => !currentDeviceIds.Contains(id));
        var currentSelections = _cycleSelection
            .Where(pair => pair.Value)
            .Select(pair => pair.Key);

        AppServices.Settings.Current.CycleDeviceIds = rememberedMissingSelections
            .Concat(currentSelections)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppServices.Settings.Save();
        SetStatus(T("devices.saved"));
    }

    private void ShowShortcutsPage()
    {
        _content.Children.Clear();
        AddHeader(T("shortcuts.title"), T("shortcuts.subtitle"));
        if (_hasUnsavedHotkeyChanges)
        {
            SetStatus(T("shortcuts.unsaved"));
        }
        _content.Children.Add(_status);

        foreach (var hotkey in AppServices.Settings.Current.Hotkeys)
        {
            _content.Children.Add(HotkeyRow(hotkey));
        }

        _content.Children.Add(ActionRow(
            (T("shortcuts.add"), AddShortcut),
            (T("shortcuts.save"), SaveHotkeys)));
    }

    private UIElement HotkeyRow(HotkeyDefinition hotkey)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = ActionName(hotkey.Action),
            Foreground = TextBrush,
            FontSize = 18
        });
        panel.Children.Add(new TextBlock
        {
            Text = hotkey.Enabled ? T("shortcuts.enabled") : T("shortcuts.disabled"),
            Foreground = SecondaryTextBrush
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{T("shortcuts.keyLabel")}: {hotkey.GestureText}",
            Foreground = SecondaryTextBrush
        });
        panel.Children.Add(new TextBlock
        {
            Text = hotkey.Action == HotkeyActionType.SetDevice
                ? $"{T("shortcuts.target")}: {DeviceName(hotkey.DeviceId)}"
                : $"{T("shortcuts.volumeStep")}: {hotkey.VolumeStep}%",
            Foreground = SecondaryTextBrush
        });

        panel.Children.Add(ActionRow(
            (hotkey.Enabled ? T("shortcuts.disable") : T("shortcuts.enable"), () =>
            {
                hotkey.Enabled = !hotkey.Enabled;
                MarkHotkeysDirty();
                ShowShortcutsPage();
            }),
            (T("shortcuts.action"), () =>
            {
                hotkey.Action = NextAction(hotkey.Action);
                if (hotkey.Action == HotkeyActionType.SetDevice && string.IsNullOrWhiteSpace(hotkey.DeviceId))
                {
                    hotkey.DeviceId = AppServices.Commands.GetDevices().FirstOrDefault()?.Id;
                }
                MarkHotkeysDirty();
                ShowShortcutsPage();
            }),
            (T("shortcuts.key"), () =>
            {
                hotkey.Key = NextKey(hotkey.Key);
                MarkHotkeysDirty();
                ShowShortcutsPage();
            }),
            (T("shortcuts.device"), () =>
            {
                hotkey.DeviceId = NextDeviceId(hotkey.DeviceId);
                hotkey.Action = HotkeyActionType.SetDevice;
                MarkHotkeysDirty();
                ShowShortcutsPage();
            }),
            (T("shortcuts.step"), () =>
            {
                hotkey.VolumeStep = hotkey.VolumeStep >= 25 ? 1 : hotkey.VolumeStep + 1;
                MarkHotkeysDirty();
                ShowShortcutsPage();
            }),
            (T("shortcuts.delete"), () =>
            {
                AppServices.Settings.Current.Hotkeys.Remove(hotkey);
                MarkHotkeysDirty();
                ShowShortcutsPage();
            })));

        return Card(panel);
    }

    private void AddShortcut()
    {
        AppServices.Settings.Current.Hotkeys.Add(new HotkeyDefinition
        {
            Name = string.Format(T("shortcuts.newName"), AppServices.Settings.Current.Hotkeys.Count + 1),
            Action = HotkeyActionType.CycleDevice,
            Modifiers = 0x0002 | 0x0004,
            Key = 0x41
        });
        MarkHotkeysDirty();
        ShowShortcutsPage();
    }

    private void SaveHotkeys()
    {
        var failures = AppServices.Hotkeys.SaveAndReload();
        _hasUnsavedHotkeyChanges = false;
        SetStatus(failures.Count == 0
            ? T("shortcuts.saved")
            : string.Format(T("shortcuts.registrationFailed"), failures.Count, string.Join(", ", failures.Select(HotkeyFailureText))));
    }

    private void MarkHotkeysDirty()
    {
        _hasUnsavedHotkeyChanges = true;
        SetStatus(T("shortcuts.unsaved"));
    }

    private void ShowSettingsPage()
    {
        _content.Children.Clear();
        AddHeader(T("settings.title"), T("settings.subtitle"));
        _content.Children.Add(_status);
        _content.Children.Add(SettingsOptionCard(
            T("settings.language"),
            T("settings.languageDescription"),
            AppServices.Settings.Current.Language,
            LocalizationService.SupportedLanguages
                .Select(language => new SelectOption(language.Code, T(language.ResourceKey)))
                .ToList(),
            value =>
            {
                AppServices.Settings.Current.Language = value;
                AppServices.Settings.Save();
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
                AppServices.Settings.Current.Theme = value;
                AppServices.Settings.Save();
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
                AppServices.Settings.Current.ShowMainWindowOnStartup = value;
                AppServices.Settings.Save();
                SetStatus(T("settings.applied"));
                return Task.FromResult(value);
            }));
        _content.Children.Add(SettingsToggleCard(
            T("settings.startWithWindows"),
            T("settings.startWithWindowsDescription"),
            AppServices.Settings.Current.StartWithWindows,
            SetStartWithWindowsAsync));
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

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(TextBlock(title, 16, TextBrush));
        text.Children.Add(TextBlock(description, 14, SecondaryTextBrush));

        var comboBox = new ComboBox
        {
            ItemsSource = options,
            DisplayMemberPath = nameof(SelectOption.Label),
            SelectedItem = options.FirstOrDefault(option => string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? options[0],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 220
        };
        AddFallbackWinUiResources(comboBox.Resources);
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is SelectOption option &&
                !string.Equals(option.Value, selectedValue, StringComparison.OrdinalIgnoreCase))
            {
                changed(option.Value);
            }
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(comboBox, 1);
        grid.Children.Add(text);
        grid.Children.Add(comboBox);
        return Card(grid);
    }

    private UIElement SettingsToggleCard(
        string title,
        string description,
        bool isOn,
        Func<bool, Task<bool>> changed)
    {
        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(TextBlock(title, 16, TextBrush));
        text.Children.Add(TextBlock(description, 14, SecondaryTextBrush));

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

        var suppressChange = false;
        toggle.Toggled += async (_, _) =>
        {
            if (suppressChange)
            {
                return;
            }

            var requested = toggle.IsOn;
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
                SetStatus(ex.Message);
            }
            finally
            {
                suppressChange = false;
                toggle.IsEnabled = true;
            }
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(text);
        grid.Children.Add(toggle);
        return Card(grid);
    }

    private async Task<bool> SetStartWithWindowsAsync(bool enabled)
    {
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
        AppServices.Settings.Save();
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
        panel.Children.Add(TextBlock(T("donation.message"), 14, SecondaryTextBrush));

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
        bannerImage.ImageFailed += (_, _) => donationButton.Content = fallbackText;
        donationButton.Click += (_, _) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri(DonationUrl));
        ToolTipService.SetToolTip(donationButton, DonationUrl);
        panel.Children.Add(donationButton);

        var link = new HyperlinkButton
        {
            Content = T("donation.openBuyMeACoffee"),
            NavigateUri = new Uri(DonationUrl),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ToolTipService.SetToolTip(link, DonationUrl);
        var urlText = TextBlock(DonationUrl, 12, SecondaryTextBrush);

        panel.Children.Add(new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(1, -6, 0, 0),
            Children =
            {
                link,
                urlText
            }
        });

        _content.Children.Add(Card(panel));
    }

    private UIElement AppInfoCard()
    {
        var grid = new Grid
        {
            RowSpacing = 12,
            ColumnSpacing = 28
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddInfoRow(grid, 0, T("about.product"), "SpeakerHop");
        AddInfoRow(grid, 1, T("about.version"), AppVersion());
        AddInfoRow(grid, 2, T("about.author"), "@mmiyaji");
        AddInfoLinkRow(grid, 3, T("about.web"), "https://ruhenheim.org");

        return Card(grid);
    }

    private UIElement PrivacyInfoCard()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(TextBlock(T("about.privacy"), 15, TextBrush));
        panel.Children.Add(ActionRow(
            (T("about.openAuthorSite"), () => _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://ruhenheim.org")))));
        return Card(panel);
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
        return Card(grid);
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
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "Unknown";
    }

    private void AddHeader(string title, string subtitle)
    {
        _content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = TextBrush,
            FontSize = 30,
            Margin = new Thickness(0, 0, 0, 6)
        });
        _content.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18)
        });
    }

    private StackPanel ActionRow(params (string Label, Action Action)[] actions)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        foreach (var action in actions)
        {
            row.Children.Add(ActionButton(action.Label, action.Action));
        }

        return row;
    }

    private Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(12, 8, 12, 8),
            MinWidth = 84
        };
        AddFallbackWinUiResources(button.Resources);
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
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10),
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
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 10),
            Child = child
        };
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
        _status.Margin = string.IsNullOrWhiteSpace(message)
            ? new Thickness(0)
            : new Thickness(0, 0, 0, 14);
    }

    private static HotkeyActionType NextAction(HotkeyActionType current)
    {
        var values = Enum.GetValues<HotkeyActionType>();
        var index = Array.IndexOf(values, current);
        return values[(index + 1) % values.Length];
    }

    private static uint NextKey(uint current)
    {
        uint[] keys = [0x41, 0x53, 0x44, 0x46, 0x26, 0x28, 0x70, 0x71, 0x72];
        var index = Array.IndexOf(keys, current);
        return keys[(index + 1 + keys.Length) % keys.Length];
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

    private string? NextDeviceId(string? current)
    {
        var devices = AppServices.Commands.GetDevices();
        if (devices.Count == 0)
        {
            return null;
        }

        var index = devices.ToList().FindIndex(device => device.Id == current);
        return devices[(index + 1 + devices.Count) % devices.Count].Id;
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
        appWindow.Resize(new Windows.Graphics.SizeInt32(1040, 720));
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
