<p align="center">
  <img src="https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt=audio%20waveform%20visualization%20with%20multiple%20output%20channels%20branching%20out%20from%20a%20single%20source%2C%20dark%20blue%20background%2C%20neon%20cyan%20and%20purple%20glowing%20lines%2C%20minimalist%20tech%20style&image_size=landscape_16_9" alt="WinAudioRouter" width="800">
</p>

<h1 align="center">WinAudioRouter</h1>

<p align="center">
  <strong>Windows 系统级音频路由器 — 将任意音频输出实时分发到多个设备</strong>
</p>

<p align="center">
  <a href="#-功能特性">特性</a> •
  <a href="#-快速开始">快速开始</a> •
  <a href="#-技术架构">架构</a> •
  <a href="#-使用说明">使用说明</a> •
  <a href="#-构建">构建</a>
</p>

---

## ✨ 功能特性

| 特性 | 说明 |
|------|------|
| 🎯 **WASAPI Loopback 捕获** | 实时捕获任意播放设备的系统音频，零延迟 |
| 🔀 **1:N 多目标路由** | 同时将音频分发到最多 5 个输出设备 |
| 🎚️ **独立音量控制** | 每个目标设备独立调节音量（0~100%） |
| ⏱️ **延迟可调** | 每个目标设备独立设置缓冲区延迟（0~2000ms），默认使用硬件最低延迟 |
| 📊 **硬件延迟显示** | 展示每个 WASAPI 设备的真实 DefaultPeriodMs / MinimumPeriodMs |
| 🖥️ **系统托盘** | 最小化到托盘运行，右键菜单控制（H.NotifyIcon WinUI） |
| 💾 **配置持久化** | 自动保存源设备、目标列表、音量、延迟等设置，重启自动恢复 |
| 🔵 **蓝牙支持** | 蓝牙音频设备扫描与管理（预留） |

## 🚀 快速开始

### 环境要求

- Windows 10 19041+ (21H1+)
- .NET 10 SDK
- Visual Studio 2022 17.8+ 或最新版

### 构建

```bash
# 克隆仓库
git clone https://github.com/wenke313/WinAudioRouter.git
cd WinAudioRouter

# 还原依赖并构建
dotnet restore
dotnet build

# 运行
dotnet run --project src/WinAudioRouter.App
```

### 发布单文件

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 🏗️ 技术架构

```
┌─────────────────────────────────────────────────────┐
│                   WinAudioRouter.App                  │
│              MAUI 10 / WinUI 3 (MVVM)                │
│         MainPage ↔ MainViewModel ↔ Services          │
└──────────────────┬──────────────────────────────────┘
                   │ DI Container
┌──────────────────▼──────────────────────────────────┐
│                  WinAudioRouter.Core                  │
│  ┌─────────────┐ ┌──────────────┐ ┌──────────────┐  │
│  │ AudioCapture │ │ AudioRouter  │ │ AudioDevice  │  │
│  │   Service   │ │    Engine    │ │   Manager    │  │
│  └──────┬──────┘ └──────┬───────┘ └──────┬───────┘  │
│         │ Loopback      │ 1:N 分发        │ 设备枚举  │
│  ┌──────▼──────────────▼────────────────▼────────┐  │
│  │           LockFreeRingBuffer                 │  │
│  │        (无锁环形缓冲区, pinned 内存)          │  │
│  └──────────────────────────────────────────────┘  │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│                WinAudioRouter.Native                │
│  NAudio WasapiOut(eventCallback) + COM Interop     │
└─────────────────────────────────────────────────────┘
```

### 核心链路

```
WasapiLoopbackCapture → LockFreeRingBuffer → RingBufferWaveProvider → WasapiOut(eventCallback) × N
```

- 捕获端：`WasapiLoopbackCapture` 以事件驱动模式采集源设备 loopback 音频
- 缓冲：无锁环形缓冲区 (`LockFreeRingBuffer`)，pinned 内存避免 GC 移动
- 输出：每个目标一个 `WasapiOut` 实例，`useEventCallback=true` 事件驱动模式

### 技术栈

| 层 | 技术 |
|---|---|
| UI 框架 | .NET MAUI 10 / WinUI 3 |
| 语言 | C# 13 / .NET 10 |
| 音频引擎 | NAudio 2.3.0 (WASAPI) |
| MVVM | CommunityToolkit.Mvvm 8.3.2 |
| 日志 | Serilog 4.2.0 (File Sink) |
| 托盘图标 | H.NotifyIcon.WinUI 2.2.0 |
| 设备监听 | System.Management (WMI) |

## 📖 使用说明

### 基本流程

1. **选择源设备** — 从下拉框选择要捕获的音频输出设备（通常选当前默认设备）
2. **添加目标设备** — 从下方设备列表点击添加（最多 5 个）
3. **调节参数** — 拖动滑块调整每个目标的音量和延迟，或双击延迟值手动输入
4. **启动路由** — 点击「开始路由」，音频开始分发到所有目标设备
5. **最小化到托盘** — 关闭窗口会隐藏到系统托盘，右键托盘图标可退出

### 延迟说明

- **0ms（默认）** = 使用设备原生最低延迟，最流畅但可能因设备而异卡顿
- **50~500ms** = 推荐范围，平衡流畅度和延迟感
- **1000ms+** = 高缓冲，非常稳定但有明显延迟
- 双击延迟标签可弹出输入框精确输入

### 配置文件

位置：`%APPDATA%\WinAudioRouter\appsettings.json`

```json
{
  "routing": {
    "sourceDeviceId": "{...}",
    "captureLatencyMs": 0,
    "targets": [
      {
        "deviceId": "{...}",
        "deviceName": "扬声器 (Fosi Audio ZH3)",
        "volumeLevel": 65,
        "latencyMs": 280
      }
    ]
  }
}
```

## 📁 项目结构

```
WinAudioRouter/
├── src/
│   ├── WinAudioRouter.App/          # MAUI 主应用 (UI + ViewModel)
│   │   ├── ViewModels/              # MVVM 视图模型
│   │   ├── Platforms/Windows/       # WinUI 平台代码（托盘、窗口生命周期）
│   │   └── Converters/              # XAML 值转换器
│   ├── WinAudioRouter.Core/         # 核心业务逻辑
│   │   ├── Audio/Services/          # 音频服务（捕获、路由、设备管理）
│   │   ├── Audio/Models/            # 数据模型（设备、目标、会话）
│   │   ├── Bluetooth/               # 蓝牙模块
│   │   └── Configuration/           # 配置持久化
│   └── WinAudioRouter.Native/       # 原生互操作（COM、P/Invoke）
│       ├── Wrappers/               # 封装层（RingBuffer、热键等）
│       └── Interop/                # P/Invoke 声明
├── tests/
│   └── WinAudioRouter.TestConsole/  # 控制台测试工具
├── docs/                           # 项目文档
├── AGENTS.md                       # 开发规则
└── Directory.Packages.props         # NuGet 版本中央管理
```

## 🐛 已知问题 & 限制

| 问题 | 状态 |
|------|------|
| `DefaultDeviceSwitcher` COM 切换默认设备偶尔失败 | 未解决 |
| 蓝牙 BLE 部分设备名称为空 | 未解决 |
| 仅支持 Windows 平台 | 设计如此 |
| 最大 5 个目标设备 | 设计限制 |

详细追踪见 [docs/issues/known-issues.md](docs/issues/known-issues.md)

## 📄 License

MIT

## 🙏 致谢

- [NAudio](https://github.com/naudio/NAudio) — .NET 音频库
- [CommunityToolkit.Mvvm](https://github.com/communitytoolkit/dotnet) — MVVM 工具包
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) — 跨平台托盘图标库
