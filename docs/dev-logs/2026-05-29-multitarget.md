# 2026-05-29 1:N 多目标路由架构重构

---

### 架构变更

**旧架构**（1:1 路由）：
```
源设备 → WasapiLoopbackCapture → BufferedWaveProvider → WasapiOut → 目标设备
```

**新架构**（1:N 路由）：
```
源设备 → WasapiLoopbackCapture ─┬→ BufferedWaveProvider₁ → [Resampler] → WasapiOut₁ → 目标设备₁
                                ├→ BufferedWaveProvider₂ → [Resampler] → WasapiOut₂ → 目标设备₂
                                └→ BufferedWaveProvider₃ → [Resampler] → WasapiOut₃ → 目标设备₃
```

核心：**只捕获一次系统声音，同时分发到所有目标设备**。

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 54 | 创建 RoutingTarget 模型 | RoutingTarget.cs | ✅ |
| 55 | 重写 IAudioRouterEngine 支持 1:N | IAudioRouterEngine.cs | ✅ |
| 56 | 重写 AudioRouterEngine 支持多目标 | AudioRouterEngine.cs | ✅ |
| 57 | 重写 MainViewModel 多目标支持 | MainViewModel.cs | ✅ |
| 58 | 重写 MainPage.xaml 多目标 UI | MainPage.xaml | ✅ |
| 59 | 修复 BluetoothViewModel 接口变更 | BluetoothViewModel.cs | ✅ |
| 60 | 更新 AudioEvents 移除 TargetDeviceId | AudioEvents.cs | ✅ |

### 新增文件

| 文件 | 用途 |
|------|------|
| `src/WinAudioRouter.Core/Audio/Models/RoutingTarget.cs` | 路由目标模型（设备ID+名称+延迟+音量） |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `IAudioRouterEngine.cs` | 接口改为多目标 | 支持 1:N 路由 |
| `AudioRouterEngine.cs` | 完全重写为多目标架构 | 一次捕获多路分发 |
| `AudioEvents.cs` | 移除 TargetDeviceId | 多目标模式 |
| `MainViewModel.cs` | 多目标+每设备延迟 | 新架构 UI 绑定 |
| `MainPage.xaml` | 多目标路由 UI | 目标设备列表+每设备延迟滑块 |
| `BluetoothViewModel.cs` | 适配新接口 | StartRoutingAsync 签名变更 |
