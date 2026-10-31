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

        var button = ActionButton(T("about.exitApplication"), () => _ = RequestExitAsync());
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
}
