using System.Runtime.InteropServices;

namespace SpeakerHop.Services;

public sealed class WindowingService
{
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private readonly MainWindow _window;

    public WindowingService(MainWindow window)
    {
        _window = window;
    }

    public void BringToFront()
    {
        ShowWindow(_window.Hwnd, SwRestore);
        SetForegroundWindow(_window.Hwnd);
    }

    public void Hide()
    {
        ShowWindow(_window.Hwnd, SwHide);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
