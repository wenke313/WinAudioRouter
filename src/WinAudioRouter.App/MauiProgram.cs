using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WinAudioRouter.Core.Audio.Services;
using WinAudioRouter.Core.Bluetooth.Services;
using WinAudioRouter.Native.Wrappers;

namespace WinAudioRouter.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        ConfigureLogging(builder);

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegisterServices(builder.Services);
        RegisterViewModels(builder.Services);

        return builder.Build();
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinAudioRouter", "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IAudioDeviceManager, AudioDeviceManager>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IAudioRouterEngine, AudioRouterEngine>();
        services.AddSingleton<IGlobalHotkeyManager, GlobalHotkeyManager>();
        services.AddSingleton<IBluetoothService, BluetoothService>();
        services.AddSingleton<IBluetoothSettingsService, BluetoothSettingsService>();
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        services.AddTransient<ViewModels.MainViewModel>();
        services.AddTransient<ViewModels.BluetoothViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<Views.BluetoothPage>();
        services.AddTransient<SettingsPage>();
    }
}
