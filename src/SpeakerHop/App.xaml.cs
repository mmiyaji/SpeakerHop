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

        if (Program.ConsumeShowSettingsRequest())
        {
            _window.ShowSettings();
        }
        else if (!AppServices.Settings.Current.InitialLaunchCompleted ||
            AppServices.Settings.Current.ShowMainWindowOnStartup)
        {
            ShowMainWindow();
        }

        AppServices.Settings.MarkInitialLaunchCompleted();
        if (AppServices.Settings.PersistenceError is { } persistenceError)
        {
            _window.ShowPersistenceError(persistenceError);
            AppServices.Tray.ShowPersistenceError(persistenceError);
        }
    }

    internal void QueueShowSettings()
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (Program.ConsumeShowSettingsRequest() && !AppServices.IsExiting)
            {
                _window.ShowSettings();
            }
        });
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
