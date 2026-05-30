using Microsoft.Extensions.Logging;
using WinAudioRouter.Core.Audio.Events;
using WinAudioRouter.Core.Audio.Models;
using WinAudioRouter.Core.Audio.Services;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

var deviceManager = new AudioDeviceManager(loggerFactory.CreateLogger<AudioDeviceManager>());
var captureService = new AudioCaptureService(loggerFactory.CreateLogger<AudioCaptureService>());

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== WinAudioRouter 测试控制台 ===");
    Console.WriteLine("1. 列出所有播放设备");
    Console.WriteLine("2. 列出所有录音设备");
    Console.WriteLine("3. 获取默认播放设备");
    Console.WriteLine("4. 测试音频捕获（3秒）");
    Console.WriteLine("5. 设备切换测试");
    Console.WriteLine("6. 设备热插拔监听测试（30秒）");
    Console.WriteLine("0. 退出");
    Console.Write("请选择: ");

    var choice = Console.ReadLine();
    Console.WriteLine();

    switch (choice)
    {
        case "1":
            await ListPlaybackDevices();
            break;
        case "2":
            await ListRecordingDevices();
            break;
        case "3":
            await GetDefaultPlaybackDevice();
            break;
        case "4":
            await TestAudioCapture();
            break;
        case "5":
            await TestDeviceSwitch();
            break;
        case "6":
            await TestHotPlug();
            break;
        case "0":
            captureService.Dispose();
            deviceManager.Dispose();
            return;
        default:
            Console.WriteLine("无效选择");
            break;
    }
}

async Task ListPlaybackDevices()
{
    var devices = await deviceManager.GetPlaybackDevicesAsync();
    Console.WriteLine($"找到 {devices.Count} 个播放设备:");
    foreach (var d in devices)
    {
        PrintDevice(d);
    }
}

async Task ListRecordingDevices()
{
    var devices = await deviceManager.GetRecordingDevicesAsync();
    Console.WriteLine($"找到 {devices.Count} 个录音设备:");
    foreach (var d in devices)
    {
        PrintDevice(d);
    }
}

async Task GetDefaultPlaybackDevice()
{
    var device = await deviceManager.GetDefaultPlaybackDeviceAsync();
    if (device is not null)
    {
        PrintDevice(device);
    }
    else
    {
        Console.WriteLine("  未找到默认播放设备");
    }
}

async Task TestAudioCapture()
{
    if (captureService.IsCapturing)
    {
        Console.WriteLine("  音频捕获已在进行中");
        return;
    }

    var captureCount = 0;
    captureService.AudioCaptured += (s, e) =>
    {
        captureCount++;
        if (captureCount <= 5 || captureCount % 10 == 0)
        {
            Console.WriteLine($"  捕获 #{captureCount}: {e.Length} 字节, {e.SamplingRate}Hz, {e.BitDepth}bit");
        }
    };

    await captureService.StartCaptureAsync();
    await Task.Delay(TimeSpan.FromSeconds(3));
    await captureService.StopCaptureAsync();
    Console.WriteLine($"  音频捕获完成，共 {captureCount} 次数据回调");
}

async Task TestDeviceSwitch()
{
    var devices = await deviceManager.GetPlaybackDevicesAsync();
    if (devices.Count == 0)
    {
        Console.WriteLine("没有可用的播放设备");
        return;
    }

    Console.WriteLine("可用播放设备:");
    for (int i = 0; i < devices.Count; i++)
    {
        var defaultTag = devices[i].IsDefault ? " [DEFAULT]" : "";
        Console.WriteLine($"  [{i}] {devices[i].Name}{defaultTag}");
    }

    Console.Write("选择设备编号: ");
    if (!int.TryParse(Console.ReadLine(), out int index) || index < 0 || index >= devices.Count)
    {
        Console.WriteLine("无效选择");
        return;
    }

    var selected = devices[index];
    Console.WriteLine($"正在切换到: {selected.Name}");
    Console.WriteLine($"设备ID: {selected.Id}");

    var success = await deviceManager.SetDefaultPlaybackDeviceAsync(selected.Id);
    if (success)
    {
        Console.WriteLine("切换成功！");
        var newDefault = await deviceManager.GetDefaultPlaybackDeviceAsync();
        if (newDefault is not null)
        {
            Console.WriteLine("当前默认设备:");
            PrintDevice(newDefault);
        }
    }
    else
    {
        Console.WriteLine("切换失败！");
    }
}

async Task TestHotPlug()
{
    Console.WriteLine("开始监听设备热插拔事件...");
    Console.WriteLine("请插拔音频设备（蓝牙、USB等）...");
    Console.WriteLine("监听将持续30秒...");
    Console.WriteLine();

    var eventCount = 0;

    deviceManager.DeviceChanged += (s, e) =>
    {
        eventCount++;
        var action = e.IsAdded ? "新增" : "移除";
        Console.WriteLine($"[设备{action}] {e.DeviceName} (ID: {e.DeviceId})");
    };

    deviceManager.DefaultDeviceChanged += (s, e) =>
    {
        eventCount++;
        Console.WriteLine($"[默认设备变更] {e.OldDeviceId} -> {e.NewDeviceId}");
    };

    await Task.Delay(TimeSpan.FromSeconds(30));

    Console.WriteLine();
    Console.WriteLine($"监听结束，共捕获 {eventCount} 个事件");
}

static void PrintDevice(AudioDevice d)
{
    var defaultTag = d.IsDefault ? " [DEFAULT]" : "";
    Console.WriteLine($"  [{d.DeviceType}] {d.Name}{defaultTag}");
    Console.WriteLine($"    ID: {d.Id}");
    Console.WriteLine($"    状态: {(d.IsActive ? "活跃" : "非活跃")}, 音量: {d.VolumeLevel}%, 采样率: {d.SamplingRate}Hz, 位深: {d.BitDepth}bit");
}
