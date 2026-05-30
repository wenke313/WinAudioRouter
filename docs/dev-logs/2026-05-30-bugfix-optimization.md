# 2026-05-30 Bug修复 + 功能优化

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 68 | 修复COM类型冲突闪退 | WasapiLowLatencyRenderer.cs, WasapiInterfaces.cs | ✅ |
| 69 | 改用NAudio WasapiOut(eventCallback=true)+LockFreeRingBuffer | AudioRouterEngine.cs, RingBufferWaveProvider.cs | ✅ |
| 70 | 修复音量调节卡死 | MainPage.xaml, MainPage.xaml.cs, MainViewModel.cs | ✅ |
| 71 | 修复软件关闭/退出问题 | App.xaml.cs, Platforms/Windows/App.xaml.cs | ✅ |
| 72 | 添加应用图标 | WinAudioRouter.App.csproj, appicon.ico | ✅ |
| 73 | 优化蓝牙扫描速度 | BluetoothService.cs | ✅ |
| 74 | 捕获延迟显示系统最低延迟 | AudioDevice.cs, AudioDeviceManager.cs, MainViewModel.cs, MainPage.xaml | ✅ |
| 75 | 修复软件无法正常退出 | App.xaml.cs, Platforms/Windows/App.xaml.cs | ✅ |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `AudioRouterEngine.cs` | 使用 WasapiOut(eventCallback=true)+LockFreeRingBuffer | 绕过COM冲突，音频路由稳定 |
| `RingBufferWaveProvider.cs` | 新增，桥接 LockFreeRingBuffer 和 IWaveProvider | 替代 BufferedWaveProvider |
| `MainPage.xaml` | 移除 ValueChanged 事件 | 与双向绑定冲突导致无限循环 |
| `MainViewModel.cs` | 改用 partial method 监听属性变化 | 替代 ValueChanged 事件 |
| `App.xaml.cs` | Process.Kill() 替代 TerminateProcess P/Invoke | P/Invoke 伪句柄无法终止进程 |
| `Platforms/Windows/App.xaml.cs` | window.Closed 中 IsExiting 时也调用 Process.Kill() | 双重保障退出 |
| `BluetoothService.cs` | FromIdAsync 并行化+3秒超时+Cached模式 | 蓝牙扫描速度优化 |
| `AudioDevice.cs` | 添加 DefaultPeriodMs/MinimumPeriodMs | 显示系统捕获延迟 |
| `AudioDeviceManager.cs` | 获取设备延迟信息 | 支持延迟显示 |
| `WinAudioRouter.App.csproj` | 添加 ApplicationIcon 配置 | 应用图标 |

### 新增文件

| 文件 | 用途 |
|------|------|
| `src/WinAudioRouter.Core/Audio/Services/RingBufferWaveProvider.cs` | 桥接 LockFreeRingBuffer 和 NAudio IWaveProvider |

### 问题记录

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| COM 类型冲突闪退 | NAudio 定义了相同 IID 的 COM 接口 | 放弃自定义 COM，使用 NAudio WasapiOut(eventCallback=true) |
| 音量调节卡死 | ValueChanged 与双向绑定冲突导致无限循环 | 移除 ValueChanged，改用 ViewModel partial method |
| 软件无法退出 | TerminateProcess 伪句柄无效 + Environment.Exit 被 MAUI 拦截 | Process.Kill() 替代 |
