using Microsoft.UI.Xaml;
using SpeakerHop.Services;

namespace SpeakerHop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) => WriteCrashLog(e.Exception);
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AppServices.Settings.Load();
        _window = new MainWindow();
        AppServices.Initialize(_window, loadSettings: false);
        _window.ShowHome();

        if (!AppServices.Settings.Current.InitialLaunchCompleted ||
            AppServices.Settings.Current.ShowMainWindowOnStartup)
        {
            ShowMainWindow();
        }

        AppServices.Settings.MarkInitialLaunchCompleted();
    }

    private void ShowMainWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.ShowHome();
        _window.Activate();
        AppServices.Windowing.BringToFront();
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeakerHop");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                $"{DateTimeOffset.Now:u}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
