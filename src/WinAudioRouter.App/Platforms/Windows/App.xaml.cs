using H.NotifyIcon;
using Microsoft.UI.Xaml;
using WinWindow = Microsoft.UI.Xaml.Window;

namespace WinAudioRouter.App.WinUI;

public partial class App : MauiWinUIApplication
{
    private TrayIconManager? _trayIcon;

    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as WinWindow;

        if (window != null)
        {
            _trayIcon = new TrayIconManager(window);
            _trayIcon.CreateTrayIcon();

            window.Closed += (s, e) =>
            {
                if (WinAudioRouter.App.App.IsExiting)
                {
                    _trayIcon?.Dispose();
                    return;
                }

                e.Handled = true;
                window.Hide();
            };
        }
    }
}
