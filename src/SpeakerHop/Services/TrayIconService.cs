using System.Drawing;
using System.Windows.Forms;

namespace SpeakerHop.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly AudioCommandService _commands;
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService(MainWindow window, AudioCommandService commands)
    {
        _window = window;
        _commands = commands;
        _commands.StatusChanged += Commands_StatusChanged;

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "SpeakerHop",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _commands.CycleDevice();
            }
        };
    }

    public void Dispose()
    {
        _commands.StatusChanged -= Commands_StatusChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var cycleItem = menu.Items.Add(T("tray.cycle"), null, (_, _) => _commands.CycleDevice());
        var settingsItem = menu.Items.Add(T("tray.settings"), null, (_, _) => _window.DispatcherQueue.TryEnqueue(_window.ShowSettings));
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = menu.Items.Add(T("tray.exit"), null, (_, _) => _window.DispatcherQueue.TryEnqueue(AppServices.ExitApplication));
        menu.Opening += (_, _) =>
        {
            cycleItem.Text = T("tray.cycle");
            settingsItem.Text = T("tray.settings");
            exitItem.Text = T("tray.exit");
        };
        return menu;
    }

    private void Commands_StatusChanged(object? sender, AudioCommandNotification notification)
    {
        if (notification.IsDeviceSwitch && !AppServices.Settings.Current.NotifyOnDeviceSwitch)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "SpeakerHop";
        _notifyIcon.BalloonTipText = notification.Message;
        _notifyIcon.ShowBalloonTip(1200);
    }

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
    }

    private static string T(string key)
    {
        return LocalizationService.Text(key, AppServices.Settings.Current.Language);
    }
}
