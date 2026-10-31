using SpeakerHop.Models;
using SpeakerHop.Services;
using SpeakerHop.Controls;
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

    private static void SetAutomation(FrameworkElement element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id);
        AutomationProperties.SetName(element, name);
    }
}
