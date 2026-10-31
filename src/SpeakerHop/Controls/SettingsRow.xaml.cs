using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace SpeakerHop.Controls;

public sealed partial class SettingsRow : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty, OnTitleChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(object), typeof(SettingsRow), new PropertyMetadata(null, OnEditorChanged));

    private const double WideLayoutThreshold = 560;

    public SettingsRow()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingsRow)d).TitleText.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingsRow)d).DescriptionText.Text = e.NewValue as string ?? string.Empty;
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var row = (SettingsRow)d;
        row.EditorPresenter.Content = e.NewValue;
        if (e.NewValue is UIElement editor)
        {
            AutomationProperties.SetLabeledBy(editor, row.TitleText);
            if (editor is Control control)
            {
                control.MaxWidth = row.LayoutRoot.ActualWidth < WideLayoutThreshold ? double.PositiveInfinity : 260;
                control.HorizontalAlignment = row.LayoutRoot.ActualWidth < WideLayoutThreshold
                    ? HorizontalAlignment.Stretch
                    : HorizontalAlignment.Right;
            }
            if (editor is RadioButtons radioButtons)
            {
                radioButtons.MaxColumns = row.LayoutRoot.ActualWidth < WideLayoutThreshold ? 1 : 3;
            }
        }
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < WideLayoutThreshold;
        if (narrow)
        {
            TextColumn.Width = new GridLength(1, GridUnitType.Star);
            EditorColumn.Width = new GridLength(0);
            Grid.SetRow(EditorPresenter, 1);
            Grid.SetColumn(EditorPresenter, 0);
            EditorPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
            EditorPresenter.MaxWidth = double.PositiveInfinity;
        }
        else
        {
            TextColumn.Width = new GridLength(1, GridUnitType.Star);
            EditorColumn.Width = GridLength.Auto;
            Grid.SetRow(EditorPresenter, 0);
            Grid.SetColumn(EditorPresenter, 1);
            EditorPresenter.HorizontalAlignment = HorizontalAlignment.Right;
            EditorPresenter.MaxWidth = 260;
        }

        if (Editor is RadioButtons radioButtons)
        {
            radioButtons.MaxColumns = narrow ? 1 : 3;
        }

        if (Editor is Control control)
        {
            control.MaxWidth = narrow ? double.PositiveInfinity : 260;
            control.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        }
    }
}

