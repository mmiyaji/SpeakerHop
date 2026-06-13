using SpeakerHop.Services;
using Microsoft.UI.Xaml;

namespace SpeakerHop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) =>
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeakerHop");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                $"{DateTimeOffset.Now:u}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        AppServices.Settings.Load();
        _window = new MainWindow();
        AppServices.Initialize(_window, loadSettings: false);

        var shouldShowMainWindow =
            !AppServices.Settings.Current.InitialLaunchCompleted ||
            AppServices.Settings.Current.ShowMainWindowOnStartup;
        _window.ShowHome();
        if (shouldShowMainWindow)
        {
            _window.Activate();
            AppServices.Settings.MarkInitialLaunchCompleted();
        }
    }
}
