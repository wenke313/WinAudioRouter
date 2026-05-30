# Windows 音频路由系统 - 项目实现计划

## 一、GitHub 相关项目调研总结

### 1.1 现有相关开源项目

| 项目名称 | GitHub 地址 | 技术栈 | 主要功能 | 适用度 |
|---------|------------|--------|---------|--------|
| **Audio Router** | https://github.com/audiorouterdev/audio-router | C++ | 按程序路由音频到不同设备 | ⭐⭐⭐ (参考架构) |
| **SoundSwitch** | https://github.com/Belphemur/SoundSwitch | C# | 音频设备快速切换+热键 | ⭐⭐⭐⭐ (参考实现) |
| **EarTrumpet** | https://github.com/File-New-Project/EarTrumpet | C#/WPF | 按应用音量控制+设备路由 | ⭐⭐⭐⭐ (参考架构) |
| **AudioSwitcher API** | https://github.com/xenolightning/AudioSwitcher | C# | 音频设备管理库 | ⭐⭐⭐⭐⭐ (核心依赖) |
| **win-capture-audio** | https://github.com/bozbez/win-capture-audio | C++ | OBS 应用音频捕获插件 | ⭐⭐ (技术参考) |
| **CSCore** | https://github.com/filoe/cscore | C# | 高级音频库,WASAPI支持 | ⭐⭐⭐⭐⭐ (核心依赖) |
| **NAudio** | https://github.com/naudio/NAudio | C# | .NET 音频库,WASAPI Loopback | ⭐⭐⭐⭐⭐ (核心依赖) |
| **VB-Audio Cable** | (非开源,免费) | 驱动 | 虚拟音频线缆 | ⭐⭐⭐ (可选依赖) |
| **Bluetooth.Core** | NuGet 包 | C#/.NET MAUI | 跨平台蓝牙低功耗库 | ⭐⭐⭐⭐⭐ (蓝牙支持) |
| **Salar.BluetoothLE.Maui** | NuGet 包 | C#/.NET MAUI | MAUI 蓝牙库 | ⭐⭐⭐⭐ (蓝牙支持) |

### 1.2 关键发现

1. **无现有 .NET MAUI 项目**：没有找到完整的 .NET MAUI Windows 音频路由项目
2. **音频捕获方案**：
   - NAudio/CSCore: 推荐使用,支持 WASAPI Loopback 捕获系统音频
   - 需要配合虚拟音频线缆(VB-Audio Cable)或 Stereo Mix 使用
3. **设备切换方案**：AudioSwitcher API 和 CSCore 提供了完整的设备枚举和切换功能
4. **蓝牙支持**：Bluetooth.Core 和 Salar.BluetoothLE.Maui 可用于 MAUI 中的蓝牙连接

### 1.3 推荐技术选型

```
┌─────────────────────────────────────────────────────────┐
│                    .NET MAUI UI 层                       │
│                  (跨平台界面 + Windows)                   │
└─────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                   业务逻辑层 (C#)                        │
│  • 音频路由管理    • 设备切换    • 音频流处理            │
└─────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
    ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
    │  NAudio /   │ │  CSCore /   │ │ Bluetooth   │
    │  WASAPI     │ │ AudioSwitch │ │ .Core       │
    │  (捕获)     │ │ (设备切换)  │ │ (蓝牙)      │
    └─────────────┘ └─────────────┘ └─────────────┘
```

## 二、项目架构设计

### 2.1 核心模块

```
WinAudioRouter/
├── WinAudioRouter.sln
│
├── src/
│   ├── WinAudioRouter.App/              # MAUI 主应用
│   │   ├── Platforms/
│   │   │   └── Windows/                # Windows 特定代码
│   │   ├── Views/                      # 页面
│   │   ├── ViewModels/                 # MVVM ViewModel
│   │   └── Services/                   # 平台服务
│   │
│   ├── WinAudioRouter.Core/            # 核心业务逻辑
│   │   ├── Audio/                      # 音频处理
│   │   │   ├── AudioCaptureService.cs  # 音频捕获服务
│   │   │   ├── AudioRouterEngine.cs    # 路由引擎
│   │   │   └── DeviceManager.cs        # 设备管理器
│   │   ├── Bluetooth/                  # 蓝牙功能
│   │   │   └── BluetoothService.cs     # 蓝牙服务
│   │   └── Models/                     # 数据模型
│   │
│   └── WinAudioRouter.Native/          # 原生互操作层
│       ├── AudioSwitcher/              # 设备切换封装
│       └── WasapiWrapper/              # WASAPI 封装
```

### 2.2 技术栈

- **框架**: .NET 10 + .NET MAUI
- **语言**: C# 13
- **音频库**: NAudio 2.2+ / CSCore
- **蓝牙**: Bluetooth.Core / InTheHand.Bluetooth
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **MVVM**: CommunityToolkit.Mvvm
- **日志**: Microsoft.Extensions.Logging + Serilog

## 三、功能模块规划

### 3.1 核心功能 (Phase 1)

1. **音频设备枚举与切换**
   - 列出所有播放设备(扬声器、耳机、蓝牙等)
   - 列出所有录音设备
   - 一键切换默认播放/录音设备

2. **系统音频捕获**
   - WASAPI Loopback 捕获系统音频流
   - 支持选择捕获源(特定应用或全局)

3. **音频路由**
   - 将捕获的音频流重定向到选定设备
   - 支持多设备同时输出

### 3.2 进阶功能 (Phase 2)

4. **蓝牙设备管理**
   - 蓝牙设备发现与配对
   - 音频路由到蓝牙设备

5. **USB 音频设备支持**
   - 检测 USB 音频设备
   - 优先路由到 USB 设备

6. **热键支持**
   - 全局快捷键切换设备
   - 快速静音/取消静音

### 3.3 高级功能 (Phase 3)

7. **按应用路由**
   - 为不同应用指定不同输出设备
   - 应用音频会话管理

8. **音频处理**
   - 音量调节
   - 音频可视化

## 四、实现步骤

### 步骤 1: 项目初始化
```bash
# 创建 MAUI 项目
dotnet new maui -n WinAudioRouter -f net10.0-windows

# 添加核心 NuGet 包
dotnet add package NAudio --version 2.2.1
dotnet add package CSCore --version 1.2.1.2
dotnet add package CommunityToolkit.Mvvm --version 8.3.2
dotnet add package Microsoft.Extensions.DependencyInjection --version 10.0.0
dotnet add package Serilog.Extensions.Logging --version 8.0.0
```

### 步骤 2: 核心服务实现

#### 2.1 音频设备枚举 (使用 CSCore/AudioSwitcher)
```csharp
// 核心实现要点
public class AudioDeviceManager
{
    // 1. 枚举音频设备
    // 2. 获取设备状态
    // 3. 设置默认设备
    // 4. 监听设备热插拔
}
```

#### 2.2 系统音频捕获 (使用 NAudio WASAPI Loopback)
```csharp
// 核心实现要点
public class SystemAudioCapture
{
    // 1. 创建 WasapiLoopbackCapture
    // 2. 处理 DataAvailable 事件
    // 3. 将音频流写入缓冲区
    // 4. 输出到目标设备
}
```

#### 2.3 音频路由引擎
```csharp
// 核心实现要点
public class AudioRouterEngine
{
    // 1. 音频输入管理
    // 2. 音频输出管理
    // 3. 路由规则配置
    // 4. 实时音频流处理
}
```

### 步骤 3: MAUI UI 实现

- **主页**: 设备列表 + 当前路由状态
- **设备详情页**: 设备信息 + 操作按钮
- **设置页**: 热键配置 + 启动选项

### 步骤 4: 蓝牙功能集成

```csharp
// 使用 Bluetooth.Core 或 InThehand.Bluetooth
public class BluetoothAudioService
{
    // 1. 设备发现
    // 2. 配对管理
    // 3. 音频配置文件(A2DP)支持
    // 4. 连接状态管理
}
```

### 步骤 5: Windows 特定优化

- 系统托盘图标
- 后台运行
- 开机自启
- 全局热键

## 五、关键技术与注意事项

### 5.1 Windows 音频架构

```
┌──────────────────────────────────────────────────────────┐
│                    Application Layer                     │
├──────────────────────────────────────────────────────────┤
│                 User Mode (Applications)                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │   App A    │  │   App B     │  │   App C     │     │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘     │
│         │                 │                 │            │
│  ┌──────▼─────────────────▼─────────────────▼──────┐   │
│  │           Audio Session Manager (WASAPI)          │   │
│  └────────────────────────┬─────────────────────────┘   │
├───────────────────────────┼─────────────────────────────┤
│                    Kernel Mode                           │
│  ┌────────────────────────▼─────────────────────────┐   │
│  │              Audio Engine (AUDIOSES.DLL)           │   │
│  └────────────────────────┬─────────────────────────┘   │
│         ┌──────────────────┼──────────────────┐          │
│  ┌──────▼──────┐    ┌──────▼──────┐    ┌──────▼──────┐ │
│  │  Speakers   │    │  Bluetooth  │    │    USB      │ │
│  │  (内置)     │    │   Headset   │    │    DAC      │ │
│  └─────────────┘    └─────────────┘    └─────────────┘ │
└──────────────────────────────────────────────────────────┘
```

### 5.2 技术挑战与解决方案

| 挑战 | 解决方案 |
|-----|---------|
| 系统音频捕获需要虚拟设备 | 使用 VB-Audio Cable 或 WASAPI Loopback |
| 音频延迟 | 使用低延迟 WASAPI 共享模式 |
| 管理员权限需求 | 提示用户或使用 UAC bypass |
| 蓝牙音频路由 | 使用 A2DP 配置文件 |
| 按应用路由 | 使用 IAudioSessionControl API |

### 5.3 权限要求

- **Windows 10/11**: 普通用户权限即可(使用 WASAPI 共享模式)
- **设备切换**: 可能需要管理员权限
- **蓝牙**: 需要蓝牙权限

## 六、参考资源

### 6.1 核心文档

1. **Windows Audio Sessions API (WASAPI)**
   - https://docs.microsoft.com/en-us/windows/win32/coreaudio/capturing-a-stream

2. **NAudio 文档**
   - https://github.com/naudio/NAudio
   - WasapiLoopbackCapture 用于系统音频捕获

3. **CSCore 文档**
   - https://github.com/filoe/cscore
   - WASAPI 和设备管理功能

4. **AudioSwitcher API**
   - https://github.com/xenolightning/AudioSwitcher
   - 设备切换参考实现

### 6.2 社区项目参考

1. **SoundSwitch 源码分析**
   - https://github.com/Belphemur/SoundSwitch
   - 学习如何切换默认音频设备

2. **EarTrumpet 架构**
   - https://github.com/File-New-Project/EarTrumpet
   - 学习按应用路由实现

## 七、风险评估

| 风险 | 影响 | 缓解措施 |
|-----|-----|---------|
| 音频延迟高 | 用户体验差 | 使用低延迟 WASAPI 设置 |
| 设备切换不稳定 | 功能失效 | 使用官方 AudioSwitcher API |
| 蓝牙兼容性 | 部分设备无法使用 | 降级到传统蓝牙 RFCOMM |
| .NET MAUI Windows 支持不完善 | 开发困难 | 必要时使用 WPF 混合 |
| 需要管理员权限 | 用户体验差 | 尽量使用共享模式 |

## 八、总结

### 项目可行性评估

✅ **可行性高**:
- 已有成熟的 .NET 音频库(NAudio, CSCore)
- AudioSwitcher 提供了完整的设备切换参考
- .NET MAUI 支持 Windows 桌面应用
- Bluetooth.Core/Salar.BluetoothLE.Maui 支持蓝牙

⚠️ **需要解决**:
- 系统音频捕获可能需要虚拟音频线缆
- 按应用路由需要较复杂的 WASAPI 编程
- Windows 音频架构的复杂性

### 建议开发顺序

1. **Phase 1**: 设备枚举与切换 (2周)
   - NAudio/CSCore 集成
   - 设备列表 UI
   - 一键切换功能

2. **Phase 2**: 音频捕获与路由 (3周)
   - WASAPI Loopback 捕获
   - 音频流重定向
   - 实时播放测试

3. **Phase 3**: 蓝牙集成 (2周)
   - 蓝牙设备发现
   - 音频路由到蓝牙

4. **Phase 4**: 高级功能 (持续迭代)
   - 热键支持
   - 按应用路由
   - 系统托盘

---

*计划编制时间: 2026-05-29*
*基于 GitHub 项目调研和技术可行性分析*
