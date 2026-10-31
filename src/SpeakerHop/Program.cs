using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using SpeakerHop.Services;

namespace SpeakerHop;

public static class Program
{
    private const string SingleInstanceMutexName = @"Local\SpeakerHop.SingleInstance.v2";
    private static App? _app;
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;
    private static SingleInstanceLease? _singleInstanceLease;
    private static ActivationChannel? _activationChannel;
    private static int _showSettingsRequested;

    private static string ActivationPipeName => $"SpeakerHop.Activation.v1.{System.Diagnostics.Process.GetCurrentProcess().SessionId}";

    internal static bool ConsumeShowSettingsRequest() => Interlocked.Exchange(ref _showSettingsRequested, 0) != 0;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject);
            WinRT.ComWrappersSupport.InitializeComWrappers();

            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var ownsMutex);
            if (!ownsMutex)
            {
                if (!IsStartupLaunch(args))
                {
                    // Main has not started a UI dispatcher; this bounded wait cannot block it.
                    ActivationChannel.RequestSettingsAsync(ActivationPipeName, TimeSpan.FromSeconds(2))
                        .GetAwaiter().GetResult();
                }
                return;
            }

            _ownsSingleInstanceMutex = true;

            // The named mutex above is retained to recognize already-running legacy builds.
            // Packaged applications can receive package-private namespaces for named kernel
            // objects, so use a profile-root file lease as the authoritative cross-build gate.
            var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
            var leasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".speakerhop",
                $"instance-{sessionId}.lock");
            _singleInstanceLease = SingleInstanceLease.TryAcquire(leasePath);
            if (_singleInstanceLease is null)
            {
                if (!IsStartupLaunch(args))
                {
                    ActivationChannel.RequestSettingsAsync(ActivationPipeName, TimeSpan.FromSeconds(2))
                        .GetAwaiter().GetResult();
                }
                return;
            }

            _activationChannel = new ActivationChannel(ActivationPipeName, () =>
            {
                Interlocked.Exchange(ref _showSettingsRequested, 1);
                _app?.QueueShowSettings();
            });

            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                _app = new App();
            });
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
        finally
        {
            _activationChannel?.Dispose();
            _singleInstanceLease?.Dispose();
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }

            _singleInstanceMutex?.Dispose();
        }
    }

    private static bool IsStartupLaunch(string[] args)
    {
        if (args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteCrashLog(object exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpeakerHop");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                $"{DateTimeOffset.Now:u}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
