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

public sealed partial class MainWindow : Window
{

    private const string DonationUrl = "https://www.buymeacoffee.com/erumdoor";
    private const string DonationBannerUrl = "https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png";
    private const string DevicesTag = "devices";
    private const string ShortcutsTag = "shortcuts";
    private const string SettingsTag = "settings";
    private const string DonationTag = "donation";
    private const string AboutTag = "about";

    private SolidColorBrush PaletteBrush(string name)
    {
        var theme = new AccessibilitySettings().HighContrast ? "HighContrast" : IsLightPalette() ? "Light" : "Dark";
        var dictionary = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme];
        return (SolidColorBrush)dictionary["SpeakerHop." + name];
    }

    private SolidColorBrush PageBrush => PaletteBrush("PageBackgroundBrush");
    private SolidColorBrush PanelBrush => PaletteBrush("PanelBackgroundBrush");
    private SolidColorBrush StrokeBrush => PaletteBrush("StrokeBrush");
    private SolidColorBrush TextBrush => PaletteBrush("PrimaryTextBrush");
    private SolidColorBrush SecondaryTextBrush => PaletteBrush("SecondaryTextBrush");

    private readonly Grid _root = new();
    private readonly NavigationView _navigation = new();
    private readonly Dictionary<string, Controls.PageSurface> _pages = new();
    private readonly ContentControl _pagePresenter = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private Controls.PageSurface CurrentPage
    {
        get
        {
            if (!_pages.TryGetValue(_currentPageTag, out var page))
                _pages[_currentPageTag] = page = new Controls.PageSurface();
            return page;
        }
    }
    private ScrollViewer _contentScroll => CurrentPage.Scroller;
    private StackPanel _content => CurrentPage.Body;
    private readonly InfoBar _status = new() { IsOpen = false, IsClosable = true };
    private readonly List<AudioDeviceInfo> _devices = [];
    private readonly Dictionary<string, bool> _cycleSelection = new(StringComparer.OrdinalIgnoreCase);
    private Grid? _titleBar;
    private TextBlock? _titleText;
    private string _currentPageTag = DevicesTag;
    private ShortcutDraftSession? _shortcutSession;
    private ShortcutDraftSession ShortcutSession => _shortcutSession ??= new(AppServices.Settings.Current.Hotkeys);
    private bool _hasUnsavedHotkeyChanges => _shortcutSession?.HasChanges ?? false;
    private List<HotkeyDefinition> _hotkeyDraft => ShortcutSession.Rows;
    private bool _exitDialogOpen;
    private string? _appearanceKey;
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
            if (args.WindowActivationState != WindowActivationState.Deactivated) RefreshAppearance();
            if (args.WindowActivationState == WindowActivationState.Deactivated && _captureDialog is not null)
            {
                _captureCancelled = true;
                _captureDialog.Hide();
            }
        };
        _root.SizeChanged += (_, _) => UpdateResponsiveLayout();
        _root.ActualThemeChanged += (_, _) => RefreshAppearance();

    }

    private void RefreshAppearance()
    {
        var key = $"{_root.ActualTheme}:{new AccessibilitySettings().HighContrast}";
        if (_appearanceKey == key) return;
        _appearanceKey = key;
        ApplyTheme();
        if (_content.Children.Count > 0) ShowPage(_currentPageTag);
    }

    public void ShowPersistenceError(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            SetStatus(string.Format(T("settings.persistenceError"), error), InfoBarSeverity.Error);
    }

    public void ShowStartupHotkeyFailures(IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        if (failures.Count > 0)
            SetStatus(string.Format(T("shortcuts.registrationFailureDetails"), failures.Count,
                string.Join(", ", failures.Select(HotkeyFailureText))), InfoBarSeverity.Warning);
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
        var pageHost = new Grid();
        pageHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        pageHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _status.Margin = new Thickness(16, 8, 16, 0);
        pageHost.Children.Add(_status);
        Grid.SetRow(_pagePresenter, 1);
        pageHost.Children.Add(_pagePresenter);
        _navigation.Content = pageHost;
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
        _navigation.FooterMenuItems.Clear();
        _navigation.MenuItems.Add(NavItem(T("nav.devices"), DevicesTag, "\uE995"));
        _navigation.MenuItems.Add(NavItem(T("nav.shortcuts"), ShortcutsTag, "\uE765"));
        _navigation.FooterMenuItems.Add(NavItem(T("nav.settings"), SettingsTag, "\uE713"));
        _navigation.FooterMenuItems.Add(NavItem(T("nav.donation"), DonationTag, "\uE8D7"));
        _navigation.FooterMenuItems.Add(NavItem(T("nav.about"), AboutTag, "\uE946"));
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

        return _root.ActualTheme == ElementTheme.Light;
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
        var item = _navigation.MenuItems.Concat(_navigation.FooterMenuItems)
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
        if (tag != _currentPageTag && _status.Severity is InfoBarSeverity.Informational or InfoBarSeverity.Success)
            SetStatus("");
        _currentPageTag = tag;
        _pagePresenter.Content = CurrentPage;
        _contentScroll.Padding = CurrentLayoutMode() switch
        {
            0 => new Thickness(20, 16, 20, 20),
            1 => new Thickness(16, 14, 16, 16),
            _ => new Thickness(12, 12, 12, 14)
        };
        _automationSequence = 0;
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

    private void SetStatus(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        _status.Message = message;
        _status.Severity = severity;
        _status.IsOpen = !string.IsNullOrWhiteSpace(message);
        AutomationProperties.SetName(_status, string.IsNullOrWhiteSpace(message) ? "Status" : message);
    }

    public async Task RequestExitAsync()
    {
        if (_exitDialogOpen) return;
        if (!_hasUnsavedHotkeyChanges) { AppServices.ExitApplication(); return; }
        _exitDialogOpen = true;
        try
        {
            Activate();
            AppServices.Windowing.BringToFront();
            var dialog = new ContentDialog
            {
                XamlRoot = _root.XamlRoot, RequestedTheme = _root.RequestedTheme,
                Title = T("shortcuts.unsaved"), PrimaryButtonText = T("shortcuts.save"),
                SecondaryButtonText = T("shortcuts.discard"), CloseButtonText = T("common.cancel"),
                DefaultButton = ContentDialogButton.Close
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                SaveHotkeys();
                if (AppServices.StartupHotkeyFailures.Count > 0) return;
            }
            else if (result == ContentDialogResult.Secondary) ShortcutSession.Discard();
            else return;
            if (!_hasUnsavedHotkeyChanges) AppServices.ExitApplication();
        }
        finally { _exitDialogOpen = false; }
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
