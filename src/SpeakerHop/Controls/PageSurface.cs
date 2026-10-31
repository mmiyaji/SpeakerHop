using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SpeakerHop.Controls;

/// <summary>Owns each page's scrolling surface independently of the application shell.</summary>
internal sealed class PageSurface : UserControl
{
    public StackPanel Body { get; } = new();
    public ScrollViewer Scroller { get; } = new() { Padding = UiMetrics.PagePadding(0) };

    public PageSurface()
    {
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(Scroller, "Page.Scroll");
        Scroller.Content = Body;
        Content = Scroller;
    }
}
