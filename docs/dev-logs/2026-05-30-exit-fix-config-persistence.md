# 2026-05-30 退出修复+延迟优化+配置持久化

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 76 | 修复应用无法退出（H.NotifyIcon ContextMenuMode） | TrayIconManager.cs, App.xaml.cs, Platforms/Windows/App.xaml.cs | ✅ |
| 77 | 延迟默认值改为0ms，最小值改为0ms | RoutingTarget.cs, AudioRouterEngine.cs, MainViewModel.cs, MainPage.xaml | ✅ |
| 78 | 每个目标设备显示硬件延迟信息（DefaultPeriodMs/MinimumPeriodMs） | RoutingTarget.cs, MainViewModel.cs, MainPage.xaml | ✅ |
| 79 | 双击延迟值可手动输入（TapGestureRecognizer） | MainViewModel.cs, MainPage.xaml | ✅ |
| 80 | 配置持久化（源设备、目标列表、音量、延迟） | AppConfiguration.cs, MainViewModel.cs | ✅ |
| 81 | 修复 UpdateTargetLatencyAsync 不重建 RingBuffer 导致目标无声 | AudioRouterEngine.cs | ✅ |
| 82 | Git 初始化 + 推送到 GitHub | .gitignore, GitHub: wenke313/WinAudioRouter | ✅ |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `TrayIconManager.cs` | ContextMenuMode=SecondWindow + OnExitApp 直接 Process.Kill() | H.NotifyIcon PopupMenu 模式不触发 Click 事件，导致退出无效 |
| `App.xaml.cs` | 移除 Application.Exit 后的 return，确保 Process.Kill() 执行 | 之前 return 导致 Kill 永远不被调用 |
| `Platforms/Windows/App.xaml.cs` | IsExiting 时只 Dispose 托盘图标，不再调用 Process.Kill() | 避免与 RequestExit 中的 Kill 冲突 |
| `RoutingTarget.cs` | 新增 DefaultPeriodMs/MinimumPeriodMs 属性和 DeviceLatencyInfo | 显示每个设备的真实硬件延迟参数 |
| `RoutingTarget.cs` | LatencyMs 默认值 200→0，最小值 50→0 | 用户要求默认使用设备最低延迟 |
| `AudioRouterEngine.cs` | CaptureLatencyMs 默认 200→0，最小值 50→0 | 同上 |
| `AudioRouterEngine.cs` | UpdateTargetLatencyAsync 中重建 RingBuffer 而非 Reset() | 延迟增大时旧 RingBuffer 太小导致数据溢出丢失 → 目标设备无声 |
| `AppConfiguration.cs` | 新增 RoutingConfiguration + TargetDeviceConfig 类 | 支持路由配置的 JSON 序列化持久化 |
| `MainViewModel.cs` | LoadDevicesAsync 加载配置恢复目标设备 | 下次启动自动恢复上次的路由设置 |
| `MainViewModel.cs` | SaveConfigAsync + 各属性变化时触发保存 | 实时保存用户调整 |
| `MainPage.xaml` | Slider Minimum="50"→"0"，延迟标签添加 TapGestureRecognizer 双击编辑 | 支持 0ms 延迟和手动输入 |

### 新增文件

| 文件 | 用途 |
|------|------|
| `.gitignore` | Git 忽略规则（bin/obj/appsettings.json 等） |

### 问题记录

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 应用点击退出无反应 | H.NotifyIcon 默认 PopupMenu 模式模拟菜单，WinUI MenuFlyoutItem.Click 事件不触发 | 改用 ContextMenuMode.SecondWindow 模式，创建真正 WinUI 窗口承载菜单 |
| Process.Kill() 不执行 | Application.Exit() 后有 return 语句，且 Exit() 在 MAUI 下不终止进程 | 移除 return，让代码继续执行到 Process.Kill() |
| ZH3 设备调高延迟后无声 | UpdateTargetLatencyAsync 只 Reset 了旧 RingBuffer，没有重建。延迟从 0ms(19200 bytes) 改到 300ms(115200 bytes) 时缓冲区太小溢出 | 延迟变更时创建新 RingBuffer（大小 = effectiveLatency * 48 * 4 * 2），Dispose 旧的 |
| 配置重启后丢失 | 无持久化机制 | 扩展 AppConfiguration，每次属性变化异步保存到 %APPDATA%/appsettings.json |
