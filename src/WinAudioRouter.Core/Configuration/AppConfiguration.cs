using System.Text.Json;

namespace WinAudioRouter.Core.Configuration;

public class AppConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BluetoothConfiguration Bluetooth { get; set; } = new();

    public string LatencyMode { get; set; } = "Normal";

    public RoutingConfiguration Routing { get; set; } = new();

    public static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinAudioRouter",
        "appsettings.json");

    public static AppConfiguration Default => new();

    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, json).ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
    }

    public static async Task<AppConfiguration> LoadAsync()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return Default;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ConfigFilePath).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            return config ?? Default;
        }
        catch
        {
            return Default;
        }
    }
}

public class RoutingConfiguration
{
    public string? SourceDeviceId { get; set; }
    public int CaptureLatencyMs { get; set; }
    public List<TargetDeviceConfig> Targets { get; set; } = [];
}

public class TargetDeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int VolumeLevel { get; set; } = 100;
    public int LatencyMs { get; set; }
}
