# 音频路由架构

> 最后更新: 2026-05-30

---

## 当前架构

```
WASAPI Loopback Capture (系统音频)
        │
        ▼
  LockFreeRingBuffer (无锁环形缓冲区，pinned 内存)
        │
        ├──▶ RingBufferWaveProvider₁ → WasapiOut(eventCallback=true)₁ → 目标设备₁
        ├──▶ RingBufferWaveProvider₂ → WasapiOut(eventCallback=true)₂ → 目标设备₂
        └──▶ RingBufferWaveProvider₃ → WasapiOut(eventCallback=true)₃ → 目标设备₃
```

## 核心组件

### 1. WasapiLoopbackCapture (NAudio)
- 捕获系统默认输出设备的音频流
- 事件: `DataAvailable` → 写入所有 RingBuffer
- 事件: `RecordingStopped` → 自动重连（最多3次）

### 2. LockFreeRingBuffer
- 位置: `src/WinAudioRouter.Native/Wrappers/LockFreeRingBuffer.cs`
- 单写多读，无锁设计
- `GC.AllocateArray<byte>(capacity, pinned: true)` 固定内存
- `Volatile.Read/Write` 保证可见性
- 环形回绕通过 `Span.CopyTo` 处理

### 3. RingBufferWaveProvider
- 位置: `src/WinAudioRouter.Core/Audio/Services/RingBufferWaveProvider.cs`
- 实现 `IWaveProvider` 接口
- 桥接 LockFreeRingBuffer → WasapiOut
- `Read()` 时数据不足则补零静音

### 4. WasapiOut (NAudio, eventCallback=true)
- 事件驱动模式，WASAPI 在需要数据时触发回调
- 比 `useEventCallback=false`（轮询模式）延迟更低更稳定
- 音量通过 `WasapiOut.Volume` 控制

## 关键技术决策

### 为什么不用自定义 WASAPI COM 接口？

NAudio 定义了与我们相同 IID 的 COM 接口（IMMDeviceEnumerator, IMMDevice, IAudioClient, IAudioRenderClient），.NET COM 互操作使用 IID 作为 RCW 缓存键，无法绕过。尝试了 `CoCreateInstance` P/Invoke 和 `Marshal.GetUniqueObjectForIUnknown` 均失败。

### 为什么用 LockFreeRingBuffer 替代 BufferedWaveProvider？

- `BufferedWaveProvider` 不是线程安全的
- 多消费者场景下数据竞争导致声音重复
- `AddWaveProvider` 内部有锁，增加延迟

## 延迟参数

| 参数 | 默认值 | 范围 | 说明 |
|------|--------|------|------|
| RingBuffer 容量 | latencyMs * waveFormat.AverageBytesPerSecond / 1000 * 2 | - | 每个目标设备独立 |
| WasapiOut Latency | 用户设定 (50-2000ms) | 50-2000ms | 控制播放缓冲区 |
| 系统捕获延迟 | 设备 DefaultPeriodMs | 10ms (典型) | WASAPI 引擎周期 |

## 限制

- 最多 5 个目标设备同时路由
- 音量调节通过 `WasapiOut.Volume`（0.0-1.0），非流级 IAudioStreamVolume
- 格式转换由 WASAPI AUTOCONVERTPCM 自动处理
