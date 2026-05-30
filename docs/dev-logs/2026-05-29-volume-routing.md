# 2026-05-29 音量控制 / 路由卡顿修复 / 中文UI / 延迟调节

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 49 | 实现设备音量实际控制 | IAudioDeviceManager.cs, AudioDeviceManager.cs | ✅ |
| 50 | 修复路由声音卡顿 | AudioRouterEngine.cs | ✅ |
| 51 | UI 全面中文化 | MainPage.xaml, BluetoothPage.xaml, SettingsPage.xaml, AppShell.xaml | ✅ |
| 52 | 添加路由延迟调节 | MainViewModel.cs, MainPage.xaml, IAudioRouterEngine.cs | ✅ |
| 53 | 音量滑块使用 ValueChanged 事件 | MainPage.xaml.cs | ✅ |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `IAudioDeviceManager.cs` | 添加 SetDeviceVolumeAsync | 支持音量实际控制 |
| `AudioDeviceManager.cs` | 实现 SetDeviceVolumeAsync，使用 AudioEndpointVolume | 实际控制设备音量 |
| `IAudioRouterEngine.cs` | StartRoutingAsync 添加 latencyMs 参数 | 支持自定义延迟 |
| `AudioRouterEngine.cs` | 优化缓冲区大小、DiscardOnBufferOverflow、ResamplerQuality | 修复声音卡顿 |
| `MainViewModel.cs` | 添加 SourceVolume/TargetVolume/RoutingLatencyMs | 音量和延迟控制 |
| `MainPage.xaml` | 全面中文化 + 延迟滑块 | 中文UI + 延迟调节 |

### 待解决

- [ ] 音量滑块拖动时频繁调用 SetDeviceVolumeAsync，需要节流
- [ ] 路由延迟调节需要重启路由才能生效
