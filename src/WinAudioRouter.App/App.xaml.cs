namespace WinAudioRouter.App;

public partial class App : Application
{
    public static new App Current => (App)Application.Current!;

    public static bool IsExiting { get; set; }

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    public static void RequestExit()
    {
        if (IsExiting) return;
        IsExiting = true;

        System.Diagnostics.Debug.WriteLine("[WinAudioRouter] RequestExit called");

        try
        {
            global::Microsoft.UI.Xaml.Application.Current?.Exit();
            System.Diagnostics.Debug.WriteLine("[WinAudioRouter] WinUI Application.Exit() completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinAudioRouter] WinUI Application.Exit() failed: {ex.Message}");
        }

        System.Threading.Thread.Sleep(500);

        System.Diagnostics.Debug.WriteLine("[WinAudioRouter] Calling Process.Kill()");
        try
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinAudioRouter] Process.Kill() failed: {ex.Message}");
            try
            {
                Environment.Exit(0);
            }
            catch { }
        }
    }
}
