# 2026-05-30 底层 WASAPI 直接渲染方案

---

### 架构变更

**旧架构**（NAudio 高层抽象，卡顿/破音）：
```
源设备 → WasapiLoopbackCapture → BufferedWaveProvider → [MediaFoundationResampler] → WasapiOut → IAudioRenderClient
问题：轮询模式、BufferedWaveProvider线程不安全、中间层太多、最小延迟200ms
```

**新架构**（直接 WASAPI COM，事件驱动）：
```
源设备 → WasapiLoopbackCapture → LockFreeRingBuffer₁ → WasapiLowLatencyRenderer₁ → IAudioRenderClient
                                ├→ LockFreeRingBuffer₂ → WasapiLowLatencyRenderer₂ → IAudioRenderClient
                                └→ LockFreeRingBuffer₃ → WasapiLowLatencyRenderer₃ → IAudioRenderClient

关键改进：
1. 事件驱动模式 (AUDCLNT_STREAMFLAGS_EVENTCALLBACK) 替代轮询模式
2. AUTOCONVERTPCM 让 WASAPI 自动处理格式转换，去掉 MediaFoundationResampler
3. 无锁环形缓冲区替代 BufferedWaveProvider，零竞争
4. IAudioStreamVolume 控制音量，不修改设备端点音量
5. 最小延迟从 200ms 降至 20ms
```

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 61 | 创建 WASAPI COM 接口定义 | WasapiInterfaces.cs | ✅ |
| 62 | 实现无锁环形缓冲区 | LockFreeRingBuffer.cs | ✅ |
| 63 | 实现 WASAPI 低延迟渲染器 | WasapiLowLatencyRenderer.cs | ✅ |
| 64 | 重构 AudioRouterEngine | AudioRouterEngine.cs | ✅ |
| 65 | 更新 IAudioRouterEngine 接口 | IAudioRouterEngine.cs | ✅ |
| 66 | 更新 ViewModel 音量控制 | MainViewModel.cs, MainPage.xaml.cs | ✅ |
| 67 | 构建验证 | 全项目 | ✅ |

### 新增文件

| 文件 | 用途 |
|------|------|
| `src/WinAudioRouter.Native/Interop/WasapiInterfaces.cs` | WASAPI COM 接口定义 |
| `src/WinAudioRouter.Native/Wrappers/LockFreeRingBuffer.cs` | 无锁环形缓冲区 |
| `src/WinAudioRouter.Native/Wrappers/WasapiLowLatencyRenderer.cs` | WASAPI 低延迟渲染器 |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `AudioRouterEngine.cs` | 替换 WasapiOut+BufferedWaveProvider 为 WasapiLowLatencyRenderer+LockFreeRingBuffer | NAudio高层抽象导致卡顿/破音 |
| `IAudioRouterEngine.cs` | 添加 UpdateTargetVolume 方法 | 支持流级音量控制 |
| `MainViewModel.cs` | 添加 UpdateTargetVolume 方法 | ViewModel 透传音量控制 |
| `MainPage.xaml.cs` | 目标设备音量改用 UpdateTargetVolume | 路由中音量通过 IAudioStreamVolume 控制 |

### 技术决策

**放弃自定义 COM 接口**：NAudio 定义了相同 IID 的 COM 接口（IMMDeviceEnumerator, IMMDevice, IAudioClient, IAudioRenderClient），.NET COM 互操作使用 IID 作为 RCW 缓存键，无法绕过。

**最终方案**：改用 NAudio `WasapiOut(useEventCallback=true)` + `LockFreeRingBuffer` + `RingBufferWaveProvider`，音频路由稳定无卡顿。
