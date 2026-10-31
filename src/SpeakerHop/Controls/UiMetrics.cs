using Microsoft.UI.Xaml;

namespace SpeakerHop.Controls;

internal static class UiMetrics
{
    public const double ButtonMinHeight = 32;
    public const double ButtonMinWidth = 32;
    public const double ControlGap = 8;
    public const double LabelGap = 4;
    public const double SectionGap = 16;
    public const double RowPadding = 8;

    public static Thickness ButtonPadding => new Thickness(8, 4, 8, 4);

    public static Thickness PagePadding(int layoutMode) => layoutMode == 2
        ? new Thickness(12)
        : new Thickness(16);
}
