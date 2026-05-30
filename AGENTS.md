# WinAudioRouter - 开发规则

## 一、项目事实

| 项 | 值 |
|---|---|
| 类型 | .NET MAUI Windows 桌面应用 |
| 框架 | .NET 10 + MAUI 10.0 |
| 语言 | C# 13 |
| 核心 | WASAPI Loopback 系统音频捕获 → 1:N 路由到多设备 |
| 目标 | 最多 5 个输出设备同时路由，延迟 50-2000ms |

---

## 二、技术栈（实际依赖）

### NuGet 包（版本由 Directory.Packages.props 中央管理）

| 包 | 版本 | 用途 | 所在项目 |
|---|---|---|---|
| Microsoft.Maui.Controls | 10.0.70 | UI 框架 | App |
| NAudio | 2.3.0 | WASAPI 音频捕获/播放 | Core |
| CommunityToolkit.Mvvm | 8.3.2 | MVVM 源生成 | App |
| Serilog / Serilog.Extensions.Logging / Serilog.Sinks.File | 4.2.0 / 8.0.0 / 6.0.0 | 结构化日志 | App |
| H.NotifyIcon.WinUI | 2.2.0 | 系统托盘 | App |
| System.Management | 9.0.0 | WMI 设备监听 | Core |
| Microsoft.Extensions.Logging | 10.0.0 | 日志抽象 | Core, TestConsole |

### 禁止

- ❌ `new` 创建服务实例 → 用 DI
- ❌ 同步阻塞调用 → 全部 async
- ❌ `System.IO.FileStream` 处理音频数据
- ❌ 手动 `Thread` → 用 `Task.Run` / `async-await`
- ❌ 自定义 WASAPI COM 接口 → NAudio 已定义相同 IID，RCW 缓存冲突无法绕过
- ❌ `Value="{Binding}"` + `ValueChanged` 同时使用 → 无限循环，用 ViewModel partial method 替代

---

## 三、项目结构（实际目录）

```
WinAudioRouter/
├── src/
│   ├── WinAudioRouter.App/                  # MAUI 主应用 (net10.0-windows10.0.19041.0)
│   │   ├── App.xaml(.cs)                   # 应用入口，RequestExit() 用 Process.Kill()
│   │   ├── AppShell.xaml(.cs)              # TabBar: 音频路由 / 蓝牙 / 设置
│   │   ├── MainPage.xaml(.cs)              # 主路由页面
│   │   ├── SettingsPage.xaml(.cs)          # 设置页面
│   │   ├── MauiProgram.cs                  # DI 容器配置
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs            # 路由控制、设备选择、音量/延迟
│   │   │   ├── BluetoothViewModel.cs       # 蓝牙扫描/配对/连接
│   │   │   └── SettingsViewModel.cs
│   │   ├── Views/
│   │   │   └── BluetoothPage.xaml(.cs)
│   │   ├── Converters/
│   │   │   ├── BoolConverters.cs
│   │   │   └── VolumeToColorConverter.cs
│   │   └── Platforms/Windows/
│   │       ├── App.xaml(.cs)               # WinUI 入口，托盘+窗口生命周期
│   │       ├── TrayIconManager.cs          # 系统托盘图标
│   │       └── appicon.ico
│   │
│   ├── WinAudioRouter.Core/                # 核心业务 (net10.0-windows10.0.19041.0)
│   │   ├── Audio/
│   │   │   ├── Services/                   # 接口+实现
│   │   │   │   ├── IAudioDeviceManager.cs / AudioDeviceManager.cs
│   │   │   │   ├── IAudioCaptureService.cs / AudioCaptureService.cs
│   │   │   │   ├── IAudioRouterEngine.cs / AudioRouterEngine.cs
│   │   │   │   ├── IAudioSessionManager.cs / AudioSessionManager.cs
│   │   │   │   ├── IHotkeyService.cs / HotkeyService.cs
│   │   │   │   └── RingBufferWaveProvider.cs
│   │   │   ├── Models/
│   │   │   │   ├── AudioDevice.cs          # 含 DefaultPeriodMs, MinimumPeriodMs
│   │   │   │   ├── AudioDeviceType.cs
│   │   │   │   ├── AudioSession.cs
│   │   │   │   ├── AudioSessionState.cs
│   │   │   │   ├── AudioLatencyMode.cs
│   │   │   │   ├── AudioSessionRoutingRule.cs
│   │   │   │   ├── AppRoutingRule.cs
│   │   │   │   ├── CaptureStatistics.cs
│   │   │   │   └── RoutingTarget.cs        # DeviceId + DeviceName + LatencyMs + VolumeLevel
│   │   │   └── Events/
│   │   │       ├── AudioEvents.cs
│   │   │       ├── HotkeyEventArgs.cs
│   │   │       └── SessionStateChangedEventArgs.cs
│   │   ├── Bluetooth/
│   │   │   ├── Services/
│   │   │   │   ├── IBluetoothService.cs / BluetoothService.cs
│   │   │   │   └── IBluetoothSettingsService.cs / BluetoothSettingsService.cs
│   │   │   ├── Models/
│   │   │   │   ├── BluetoothAudioDevice.cs
│   │   │   │   └── BluetoothDeviceType.cs
│   │   │   └── Events/
│   │   │       └── BluetoothEvents.cs
│   │   └── Configuration/
│   │       ├── AppConfiguration.cs
│   │       └── BluetoothConfiguration.cs
│   │
│   └── WinAudioRouter.Native/              # 原生互操作 (net10.0-windows, AllowUnsafeBlocks)
│       ├── Interop/
│       │   ├── NativeMethods.cs            # IPolicyConfigVista COM
│       │   ├── WasapiInterfaces.cs          # ⚠️ 未使用，与 NAudio COM IID 冲突
│       │   └── HotkeyInterop.cs
│       └── Wrappers/
│           ├── LockFreeRingBuffer.cs        # 无锁环形缓冲区，pinned 内存
│           ├── WasapiLowLatencyRenderer.cs  # ⚠️ 未使用，COM 冲突
│           ├── DefaultDeviceSwitcher.cs
│           ├── GlobalHotkeyManager.cs
│           └── HotkeyDefinition.cs
│
├── tests/
│   └── WinAudioRouter.TestConsole/         # 交互式控制台测试
│
├── docs/                                    # 📚 项目文档（必读）
│   ├── README.md                           # 文档索引
│   ├── dev-logs/                           # 开发日志
│   ├── architecture/                       # 架构文档
│   ├── plans/                              # 项目计划
│   └── issues/                             # 问题追踪
│
├── ICO/favicon.ico
├── Directory.Packages.props                # 中央包管理
├── .editorconfig
└── WinAudioRouter.slnx
```

### 命名空间

| 层 | 命名空间 |
|---|---|
| App | `WinAudioRouter.App` |
| App WinUI | `WinAudioRouter.App.WinUI` |
| App VM | `WinAudioRouter.App.ViewModels` |
| App Views | `WinAudioRouter.App.Views` |
| App Converters | `WinAudioRouter.App.Converters` |
| Core Audio Services | `WinAudioRouter.Core.Audio.Services` |
| Core Audio Models | `WinAudioRouter.Core.Audio.Models` |
| Core Audio Events | `WinAudioRouter.Core.Audio.Events` |
| Core Bluetooth Services | `WinAudioRouter.Core.Bluetooth.Services` |
| Core Bluetooth Models | `WinAudioRouter.Core.Bluetooth.Models` |
| Core Bluetooth Events | `WinAudioRouter.Core.Bluetooth.Events` |
| Core Config | `WinAudioRouter.Core.Configuration` |
| Native Wrappers | `WinAudioRouter.Native.Wrappers` |
| Native Interop | `WinAudioRouter.Native.Interop` |

---

## 四、核心接口契约

### IAudioDeviceManager

```csharp
namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioDeviceManager
{
    Task<IReadOnlyList<AudioDevice>> GetPlaybackDevicesAsync();
    Task<IReadOnlyList<AudioDevice>> GetRecordingDevicesAsync();
    Task<AudioDevice?> GetDefaultPlaybackDeviceAsync();
    Task<bool> SetDefaultPlaybackDeviceAsync(string deviceId);
    Task<bool> SetDeviceVolumeAsync(string deviceId, float volume);
    event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
    event EventHandler<DefaultDeviceChangedEventArgs>? DefaultDeviceChanged;
}
```

### IAudioRouterEngine

```csharp
namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioRouterEngine
{
    Task StartRoutingAsync(string sourceDeviceId, IReadOnlyList<RoutingTarget> targets, int latencyMs = 200);
    Task StopRoutingAsync();
    bool IsRouting { get; }
    bool UpdateTargetVolume(string targetDeviceId, float volume);
    event EventHandler<RoutingStateChangedEventArgs>? RoutingStateChanged;
}
```

### IAudioCaptureService

```csharp
namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioCaptureService
{
    Task StartCaptureAsync(string deviceId);
    Task StopCaptureAsync();
    bool IsCapturing { get; }
    MMDevice? GetDeviceById(string deviceId);
    WaveFormat? GetDeviceWaveFormat(string deviceId);
    CaptureStatistics GetStatistics();
    event EventHandler<AudioCapturedEventArgs>? DataAvailable;
    event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;
}
```

### IAudioSessionManager

```csharp
namespace WinAudioRouter.Core.Audio.Services;

public interface IAudioSessionManager
{
    Task<IReadOnlyList<AudioSession>> GetSessionsAsync();
    Task<float> GetSessionVolumeAsync(string sessionId);
    event EventHandler<SessionStateChangedEventArgs>? SessionCreated;
    event EventHandler<SessionStateChangedEventArgs>? SessionRemoved;
}
```

### IBluetoothService

```csharp
namespace WinAudioRouter.Core.Bluetooth.Services;

public interface IBluetoothService
{
    Task<IReadOnlyList<BluetoothAudioDevice>> GetPairedDevicesAsync();
    Task<bool> ConnectAsync(string deviceId);
    Task DisconnectAsync(string deviceId);
    bool IsConnected(string deviceId);
    event EventHandler<BluetoothDeviceEventArgs>? DeviceStateChanged;
}
```

### IHotkeyService

```csharp
namespace WinAudioRouter.Core.Audio.Services;

public interface IHotkeyService
{
    Task RegisterAsync(HotkeyDefinition hotkey);
    Task UnregisterAsync(HotkeyDefinition hotkey);
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;
}
```

---

## 五、架构规则

### 依赖方向（严格）

```
App → Core → Native
```
- App 可引用 Core、Native
- Core 可引用 Native
- Native 不引用任何项目
- ❌ 禁止反向依赖

### MVVM 规则

- ViewModel 继承 `ObservableObject`，用 `[ObservableProperty]` + `[RelayCommand]`
- 属性变化监听用 `partial void OnXxxChanged()` — 不用 XAML `ValueChanged` 事件
- COM/WASAPI 操作必须 `Task.Run` 到后台线程，不阻塞 UI
- UI 更新用 `MainThread.BeginInvokeOnMainThread()`

### DI 注册（MauiProgram.cs）

| 生命周期 | 适用 | 示例 |
|---|---|---|
| Singleton | 有状态服务 | AudioDeviceManager, AudioRouterEngine, BluetoothService |
| Transient | 无状态/轻量 | ViewModel, Page |

### 事件规则

- 事件参数类继承 `EventArgs`，用 `required` + `init` 属性
- 事件触发用 `Event?.Invoke(this, e)` 模式
- 订阅后注意取消订阅，防止内存泄漏

### 音频路由架构

详见 `docs/architecture/audio-routing.md`

核心链路：
```
WasapiLoopbackCapture → LockFreeRingBuffer → RingBufferWaveProvider → WasapiOut(eventCallback=true)
```

关键约束：
- WasapiOut 必须用 `useEventCallback=true`（事件驱动，非轮询）
- 不使用自定义 WASAPI COM 接口（与 NAudio IID 冲突）
- 不使用 `BufferedWaveProvider`（线程不安全），用 `LockFreeRingBuffer`

---

## 六、代码风格

### 命名

| 类型 | 规则 | 示例 |
|---|---|---|
| 类/接口 | PascalCase | `AudioDeviceManager`, `IAudioDeviceManager` |
| 方法 | PascalCase | `GetPlaybackDevicesAsync` |
| 属性 | PascalCase | `DeviceName` |
| 私有字段 | _camelCase | `_audioDevice` |
| 常量 | PascalCase | `MaxSampleRate` |
| 事件 | PascalCase | `DeviceChanged` |
| 枚举值 | PascalCase | `DeviceState.Connected` |
| XAML 控件 | Type_Purpose | `Btn_Switch`, `Sld_Volume` |
| XAML 资源键 | Category_Name | `PrimaryButton`, `AccentColor` |

### 文件

- 一个类/接口一个文件，文件名 = 类型名
- 目录结构反映命名空间
- 接口文件 `I` 前缀

### 格式

- 4 空格缩进，行宽 120
- using 排序：系统 → 第三方 → 项目
- 库代码 `ConfigureAwait(false)`

### 异步

- 方法名 `Async` 后缀，返回 `Task` / `Task<T>`
- ❌ 禁止 `.Wait()` / `.Result`（死锁风险）
- ❌ 禁止 `async void`（仅事件处理允许）

### 退出

- 应用退出必须用 `System.Diagnostics.Process.GetCurrentProcess().Kill()`
- ❌ `Environment.Exit(0)` — MAUI WinUI 会拦截
- ❌ `TerminateProcess` P/Invoke — 伪句柄无效

---

## 七、日志

| 级别 | 场景 |
|---|---|
| Information | 设备切换、路由启停 |
| Warning | 设备未找到、使用默认 |
| Error | 捕获失败、路由异常 |
| Critical | 音频引擎崩溃 |

- 结构化日志：`_logger.LogInformation("Device {DeviceId} switched", id)`
- 生产日志路径：`%APPDATA%\WinAudioRouter\logs\`
- 保留 7 天，格式 `log-{Date}.json`

---

## 八、测试

- 当前：`tests/WinAudioRouter.TestConsole/` 交互式控制台
- 命名：`[Method]_[Scenario]_[ExpectedResult]`
- 构建验证：`dotnet build` 必须零错误

---

## 九、构建命令

```powershell
dotnet restore                                          # 恢复依赖
dotnet build                                            # 构建
dotnet build -c Debug -f net10.0-windows10.0.19041.0   # 构建 (指定框架)
dotnet run --project src/WinAudioRouter.App             # 运行
dotnet test                                             # 测试
dotnet format                                           # 格式化
```

发布：
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 十、文档管理（强制）

### 体系

```
docs/
├── README.md              # 文档索引（入口）
├── dev-logs/              # 开发日志 YYYY-MM-DD-关键词.md
├── architecture/          # 架构文档
├── plans/                 # 项目计划
└── issues/                # 问题追踪 known-issues.md
```

### 强制规则

1. **代码变更必须记录** → `docs/dev-logs/` 下创建当日日志
2. **索引必须同步** → 新增文档后更新 `docs/README.md`
3. **问题必须追踪** → 发现/解决问题更新 `docs/issues/known-issues.md`
4. **架构变更必须记录** → 写入 `docs/architecture/`
5. **单文件不超 200 行** → 超过则按主题拆分
6. **日志命名** → `YYYY-MM-DD-关键词.md`（kebab-case）

### 检查清单

- [ ] `docs/dev-logs/` 有当日日志
- [ ] `docs/README.md` 索引已更新
- [ ] 新问题 → `docs/issues/known-issues.md` 已更新
- [ ] 架构变更 → `docs/architecture/` 已更新

---

## 十一、已知技术约束

| 约束 | 原因 | 应对 |
|---|---|---|
| 不用自定义 WASAPI COM | NAudio 定义了相同 IID，.NET RCW 缓存冲突 | 用 NAudio WasapiOut(eventCallback=true) |
| 不用 ValueChanged + 双向绑定 | 无限循环 | 用 ViewModel partial method |
| 不用 Environment.Exit | MAUI WinUI 拦截 | Process.Kill() |
| DefaultDeviceSwitcher COM 失败 | IPolicyConfigVista 返回错误 HRESULT | 待修复 |
| WasapiLowLatencyRenderer 未使用 | COM 冲突 | 保留但不引用 |

---

*文档版本 3.0.0 | 2026-05-30*
