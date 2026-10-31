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
    private void ShowShortcutsPage()
    {
        _ = ShortcutSession;
        _content.Children.Clear();
        AddHeader(T("shortcuts.title"), T("shortcuts.subtitle"));
        if (_hasUnsavedHotkeyChanges)
        {
            SetStatus(T("shortcuts.unsaved"));
        }
        RestoreStartupHotkeyStatus();
        var saveButton = InlineButton(T("shortcuts.save"), SaveHotkeys, "Shortcuts.Save", T("shortcuts.save"));
        var discardButton = InlineButton(T("shortcuts.discard"), DiscardHotkeys, "Shortcuts.Discard", T("shortcuts.discard"));
        _saveHotkeysButton = saveButton;
        _discardHotkeysButton = discardButton;
        UpdateDraftActions();
        var saveBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6, Margin = new Thickness(0, 0, 0, 6), Children = { discardButton, saveButton } };
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
        if (_hotkeyDraft.Any(hotkey => hotkey.Enabled && hotkey.Key != 0 &&
            hotkey.Action == HotkeyActionType.SetDevice && string.IsNullOrWhiteSpace(hotkey.DeviceId)))
        {
            SetStatus(T("shortcuts.targetRequired"));
            return;
        }
        IReadOnlyList<HotkeyRegistrationFailure> failures;
        try
        {
            failures = AppServices.Hotkeys.SaveAndReload(ShortcutSession.SnapshotForSave());
            AppServices.SetStartupHotkeyFailures(failures);
        }
        catch (Exception ex)
        {
            ShowPersistenceError(ex.Message);
            return;
        }

        ShortcutSession.AcceptSaved(AppServices.Settings.Current.Hotkeys);
        ShowShortcutsPage();
        SetStatus(failures.Count == 0 ? T("shortcuts.saved") :
            string.Format(T("shortcuts.registrationFailed"), failures.Count, string.Join(", ", failures.Select(HotkeyFailureText))),
            failures.Count == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private Button? _saveHotkeysButton;
    private Button? _discardHotkeysButton;

    private void UpdateDraftActions()
    {
        if (_saveHotkeysButton is not null) _saveHotkeysButton.IsEnabled = _hasUnsavedHotkeyChanges;
        if (_discardHotkeysButton is not null) _discardHotkeysButton.IsEnabled = _hasUnsavedHotkeyChanges;
    }

    private void MarkHotkeysDirty()
    {
        UpdateDraftActions();
        SetStatus(_hasUnsavedHotkeyChanges ? T("shortcuts.unsaved") : "");
    }

    private void DiscardHotkeys()
    {
        ShortcutSession.Discard();
        SetStatus("");
        ShowShortcutsPage();
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
}
