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

    public int FontSize { get; set; } = 15;

    public string? Language { get; set; } = "简体中文";

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

        var rawJson = await File.ReadAllTextAsync(ConfigFilePath).ConfigureAwait(false);

        AppConfiguration? config = null;
        Exception? deserializeError = null;

        try
        {
            config = JsonSerializer.Deserialize<AppConfiguration>(rawJson, JsonOptions);
        }
        catch (Exception ex)
        {
            deserializeError = ex;
        }

        if (config == null || deserializeError != null)
        {
            var debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WinAudioRouter",
                "config-debug.txt");
            File.WriteAllText(debugLogPath,
                $"=== Config Load Error ===\n" +
                $"Time: {DateTime.Now:O}\n" +
                $"Error: {deserializeError?.Message ?? "config is null"}\n" +
                $"Stack: {deserializeError?.StackTrace}\n" +
                $"JSON length: {rawJson.Length}\n" +
                $"JSON preview: {(rawJson.Length > 500 ? rawJson[..500] + "..." : rawJson)}\n");
            return Default;
        }

        if (config.Routing?.Targets != null && config.Routing.Targets.Count > 0)
        {
            Console.Error.WriteLine($"[Config] Loaded {config.Routing.Targets.Count} targets");
        }
        else
        {
            Console.Error.WriteLine($"[Config] Loaded but Targets is null/empty (count={config.Routing?.Targets?.Count ?? -1})");
        }

        return config;
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
