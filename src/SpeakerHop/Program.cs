using Microsoft.UI.Xaml;

namespace SpeakerHop;

public static class Program
{
    private const string SingleInstanceMutexName = @"Local\SpeakerHop.SingleInstance";
    private static App? _app;
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var ownsMutex);
            if (!ownsMutex)
            {
                return;
            }

            _ownsSingleInstanceMutex = true;
            AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject);
            WinRT.ComWrappersSupport.InitializeComWrappers();
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
            if (_ownsSingleInstanceMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }

            _singleInstanceMutex?.Dispose();
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
