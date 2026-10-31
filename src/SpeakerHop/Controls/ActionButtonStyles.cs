using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SpeakerHop.Controls;

/// <summary>
/// Applies semantic button colors while keeping WinUI's built-in Button template,
/// including its focus, disabled, and pointer states.
/// </summary>
internal static class ActionButtonStyles
{
    public static void ApplyPrimary(Button button)
    {
        button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
    }

    public static void ApplyDialog(ContentDialog dialog, bool criticalPrimary = false, bool criticalSecondary = false)
    {
        dialog.CloseButtonStyle = NamedStyle("Cancel");
        dialog.PrimaryButtonStyle = criticalPrimary ? NamedStyle("Critical") :
            (Style)Application.Current.Resources["AccentButtonStyle"];
        if (criticalSecondary) dialog.SecondaryButtonStyle = NamedStyle("Critical");
        dialog.Opened += (_, _) =>
        {
            foreach (var button in Buttons(dialog))
            {
                if (button.Name == "CloseButton") ApplyCancel(button);
                else if (button.Name == "PrimaryButton")
                {
                    if (criticalPrimary) ApplyCritical(button);
                    else ApplyPrimary(button);
                }
                else if (button.Name == "SecondaryButton" && criticalSecondary) ApplyCritical(button);
            }
        };
    }

    private static Style NamedStyle(string kind) =>
        (Style)Application.Current.Resources[$"SpeakerHop.{kind}ActionStyle"];

    private static IEnumerable<Button> Buttons(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button button) yield return button;
            foreach (var descendant in Buttons(child)) yield return descendant;
        }
    }

    public static void ApplyCritical(Button button) => Apply(button, ActionKind.Critical);

    public static void ApplyCancel(Button button) => Apply(button, ActionKind.Cancel);

    private static void Apply(Button button, ActionKind kind)
    {
        button.Style = NamedStyle(kind == ActionKind.Critical ? "Critical" : "Cancel");
        var themes = new ResourceDictionary();
        themes.ThemeDictionaries.Add("Light", Palette(kind, dark: false));
        themes.ThemeDictionaries.Add("Dark", Palette(kind, dark: true));
        // Leave system brushes and templates in control for contrast themes.
        themes.ThemeDictionaries.Add("HighContrast", new ResourceDictionary());
        button.Resources.MergedDictionaries.Add(themes);
    }

    private static ResourceDictionary Palette(ActionKind kind, bool dark)
    {
        var resources = new ResourceDictionary();
        Color background, hover, pressed, border, hoverBorder, foreground;

        if (kind == ActionKind.Critical)
        {
            // CAB's ImpactActionResources uses a restrained red tint to mark
            // consequential actions without replacing the platform template.
            background = Hex(dark ? "14FFB4B8" : "0CA4262C");
            hover = Hex(dark ? "22FFB4B8" : "18A4262C");
            pressed = Hex(dark ? "30FFB4B8" : "24A4262C");
            border = Hex(dark ? "48FFB4B8" : "409B3038");
            hoverBorder = Hex(dark ? "68FFB4B8" : "609B3038");
            foreground = Hex(dark ? "FFFFB4B8" : "FF9B3038");
        }
        else
        {
            // Cancel is intentionally a quiet, outlined action that remains
            // visible beside a filled primary action.
            background = Colors.Transparent;
            hover = Hex(dark ? "1FFFFFFF" : "0F000000");
            pressed = Hex(dark ? "2EFFFFFF" : "1F000000");
            border = Hex(dark ? "66FFFFFF" : "66000000");
            hoverBorder = Hex(dark ? "99FFFFFF" : "99000000");
            foreground = Hex(dark ? "FFFFFFFF" : "FF1A1A1A");
        }

        resources["ButtonBackground"] = new SolidColorBrush(background);
        resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(hover);
        resources["ButtonBackgroundPressed"] = new SolidColorBrush(pressed);
        resources["ButtonBorderBrush"] = new SolidColorBrush(border);
        resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(hoverBorder);
        resources["ButtonBorderBrushPressed"] = new SolidColorBrush(hoverBorder);
        resources["ButtonForeground"] = new SolidColorBrush(foreground);
        resources["ButtonForegroundPointerOver"] = new SolidColorBrush(foreground);
        resources["ButtonForegroundPressed"] = new SolidColorBrush(foreground);
        return resources;
    }

    private static Color Hex(string value) => Color.FromArgb(
        Convert.ToByte(value[..2], 16), Convert.ToByte(value[2..4], 16),
        Convert.ToByte(value[4..6], 16), Convert.ToByte(value[6..8], 16));

    private enum ActionKind { Critical, Cancel }
}
