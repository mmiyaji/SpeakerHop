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
        _content.Children.Add(AboutLinks());
        _content.Children.Add(AboutLegal());
        _content.Children.Add(AboutStorage());
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
        AddInfoRow(grid, 3, T("about.copyright"), T("about.copyrightNotice"));
        AddInfoLinkRow(grid, 4, T("about.web"), "https://ruhenheim.org");

        return CompactRow(grid);
    }

    private UIElement AboutLinks()
    {
        var links = new StackPanel { Spacing = 2 };
        links.Children.Add(TextBlock(T("about.linksHeading"), 16, TextBrush));
        foreach (var (key, suffix) in new[] { ("about.releases", "/releases"), ("about.support", "/issues") })
        {
            var link = new HyperlinkButton { Content = T(key), NavigateUri = new Uri("https://github.com/mmiyaji/SpeakerHop" + suffix), Padding = new Thickness(0, 4, 0, 4) };
            SetAutomation(link, key, T(key));
            links.Children.Add(link);
        }
        return CompactRow(links);
    }

    private UIElement AboutLegal()
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(TextBlock(T("about.legalHeading"), 16, TextBrush));
        panel.Children.Add(TextBlock(T("about.dataSummary"), 13, SecondaryTextBrush));
        foreach (var (key, file) in new[] { ("about.copyright", "Copyright.txt"), ("about.terms", "Terms.txt"), ("about.privacyPolicy", "Privacy.txt"), ("about.thirdParty", "ThirdPartyNotices.txt") })
        {
            var button = new Button
            {
                Content = T(key), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 7, 10, 7)
            };
            SetAutomation(button, key, T(key));
            button.Click += async (_, _) => await ShowLegalDocumentAsync(T(key), file);
            panel.Children.Add(button);
        }
        return CompactRow(panel);
    }

    private UIElement AboutStorage()
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(TextBlock(T("about.storageDescription"), 13, SecondaryTextBrush));
        var path = TextBlock(AppServices.Settings.SettingsPath, 12, TextBrush);
        path.IsTextSelectionEnabled = true;
        SetAutomation(path, "About.SettingsPath", $"{T("about.storageHeading")}: {AppServices.Settings.SettingsPath}");
        panel.Children.Add(path);
        panel.Children.Add(TextBlock(T("about.environment") + $" Windows {Environment.OSVersion.Version} \u00B7 {RuntimeInformation.ProcessArchitecture}", 12, SecondaryTextBrush));
        return new Expander { Header = T("about.storageHeading"), Content = panel,
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 4, 0, 8) };
    }

    private sealed record LegalDocument(string Name, string Path);

    private async Task ShowLegalDocumentAsync(string title, string file)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "Legal");
            var documents = new List<LegalDocument> { new(title, LegalDocumentService.GetPath(directory, file, AppServices.Settings.Current.Language)) };
            if (file == "ThirdPartyNotices.txt")
            {
                documents.Add(new(T("about.thirdPartyOriginal"), Path.Combine(directory, file)));
                var notices = Path.Combine(directory, "ThirdParty");
                if (Directory.Exists(notices))
                    documents.AddRange(Directory.GetFiles(notices, "*.txt", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(path => new LegalDocument(Path.GetFileNameWithoutExtension(path), path)));
            }
            var text = new TextBox
            {
                IsReadOnly = true, AcceptsReturn = true,
                Text = await File.ReadAllTextAsync(documents[0].Path),
                TextWrapping = TextWrapping.Wrap, FontSize = 13,
                Height = Math.Max(180, _root.ActualHeight * 0.55),
                Width = Math.Min(640, Math.Max(240, _root.ActualWidth - 112)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ScrollViewer.SetVerticalScrollBarVisibility(text, ScrollBarVisibility.Auto);
            SetAutomation(text, "About.LegalText", title);
            var panel = new StackPanel { Spacing = 8, MinWidth = 0 };
            if (documents.Count > 1)
            {
                var selector = new ComboBox { ItemsSource = documents, DisplayMemberPath = nameof(LegalDocument.Name),
                    SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
                SetAutomation(selector, "About.LegalDocument", T("about.thirdParty"));
                selector.SelectionChanged += async (_, _) =>
                {
                    if (selector.SelectedItem is not LegalDocument selected) return;
                    try
                    {
                        var body = await File.ReadAllTextAsync(selected.Path);
                        if (ReferenceEquals(selector.SelectedItem, selected)) text.Text = body;
                    }
                    catch (Exception ex)
                    {
                        if (ReferenceEquals(selector.SelectedItem, selected))
                            text.Text = T("about.documentError") + "\n" + ex.Message;
                    }
                };
                panel.Children.Add(selector);
            }
            panel.Children.Add(text);
            var dialog = new ContentDialog
            {
                XamlRoot = _root.XamlRoot, RequestedTheme = _root.RequestedTheme,
                Title = title, Content = panel, CloseButtonText = T("common.close"),
                DefaultButton = ContentDialogButton.Close
            };
            dialog.Resources["ContentDialogMaxWidth"] = Math.Min(740, Math.Max(280, _root.ActualWidth - 48));
            Controls.ActionButtonStyles.ApplyDialog(dialog);
            await dialog.ShowAsync();
        }
        catch (Exception ex) { SetStatus(T("about.documentError") + " " + ex.Message, InfoBarSeverity.Error); }
    }

    private UIElement ExitApplicationCard()
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
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
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children =
            {
                new FontIcon { Glyph = "\uE7E8", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = T("about.exitApplication"), VerticalAlignment = VerticalAlignment.Center }
            }
        };
        Controls.ActionButtonStyles.ApplyCritical(button);
        SetAutomation(button, "About.Exit", T("about.exitApplication"));
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(button, 2);
        grid.Children.Add(icon);
        grid.Children.Add(text);
        if (CurrentLayoutMode() == 2)
        {
            Grid.SetRow(button, 1);
            Grid.SetColumn(button, 1);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
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
