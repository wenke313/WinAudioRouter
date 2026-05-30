using H.NotifyIcon;
using WinWindow = Microsoft.UI.Xaml.Window;

namespace WinAudioRouter.App.WinUI;

public sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly WinWindow _window;
    private bool _disposed;

    public TrayIconManager(WinWindow window)
    {
        _window = window;
        _notifyIcon = new TaskbarIcon();
    }

    public void CreateTrayIcon()
    {
        _notifyIcon.ToolTipText = "WinAudioRouter";

        _notifyIcon.IconSource = new GeneratedIconSource
        {
            Text = "🎧",
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            FontSize = 38,
        };

        var menuFlyout = new Microsoft.UI.Xaml.Controls.MenuFlyout
        {
            AreOpenCloseAnimationsEnabled = false,
        };

        var showItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "显示窗口" };
        showItem.Click += OnShowWindow;
        menuFlyout.Items.Add(showItem);

        menuFlyout.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());

        var exitItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "退出应用" };
        exitItem.Click += OnExitApp;
        menuFlyout.Items.Add(exitItem);

        _notifyIcon.ContextFlyout = menuFlyout;
        _notifyIcon.ContextMenuMode = ContextMenuMode.SecondWindow;
        _notifyIcon.NoLeftClickDelay = true;
        _notifyIcon.LeftClickCommand = new ShowWindowCommand(this);
        _notifyIcon.ForceCreate();
    }

    public void ShowBalloonTip(string title, string text)
    {
        _notifyIcon.ShowNotification(title, text);
    }

    private void OnShowWindow(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void OnExitApp(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ForceExit();
    }

    public static void ForceExit()
    {
        WinAudioRouter.App.App.IsExiting = true;

        new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(50);
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch { }
        })
        {
            IsBackground = true
        }.Start();
    }

    private void ShowWindow()
    {
        _window.DispatcherQueue.TryEnqueue(() =>
        {
            _window.Show();
            _window.Activate();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _notifyIcon.Dispose();
        _disposed = true;
    }

    private sealed class ShowWindowCommand(TrayIconManager manager) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => manager.ShowWindow();
    }
}
