namespace SpeakerHop.Services;

public static class AppServices
{
    public static bool IsExiting { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static AudioDeviceService Audio { get; } = new();
    public static WindowingService Windowing { get; private set; } = null!;
    public static TrayIconService Tray { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;
    public static AudioCommandService Commands { get; private set; } = null!;

    public static void Initialize(MainWindow window, bool loadSettings = true)
    {
        if (loadSettings)
        {
            Settings.Load();
        }

        Windowing = new WindowingService(window);
        Commands = new AudioCommandService(Audio, Settings);
        Tray = new TrayIconService(window, Commands);
        Hotkeys = new HotkeyService(Commands, Settings);
        Hotkeys.RegisterConfiguredHotkeys();
    }

    public static void ExitApplication()
    {
        IsExiting = true;
        Hotkeys?.Dispose();
        Tray?.Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
