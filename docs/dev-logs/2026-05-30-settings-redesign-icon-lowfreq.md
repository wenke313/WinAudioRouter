# 2026-05-30 设置页面重设计+托盘图标+低频修复

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| 83 | 修复配置持久化：初始化期间 SaveConfig 覆盖 Targets | MainViewModel.cs | ✅ |
| 84 | 设置页面重设计：移除延迟模式，新增字体/语言/开机自启/关于 | SettingsPage.xaml, SettingsViewModel.cs | ✅ |
| 85 | 托盘图标改用 favicon.ico 替代 emoji GeneratedIconSource | TrayIconManager.cs, WinAudioRouter.App.csproj | ✅ |
| 86 | 低频变少修复：RingBufferWaveProvider 数据重复代替静音填充 | RingBufferWaveProvider.cs | ✅ |
| 87 | AppConfiguration 新增 FontSize/Language 字段 | AppConfiguration.cs | ✅ |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| `MainViewModel.cs` | 新增 `_isInitializing` 标志，初始化期间跳过 SaveConfig | 加载配置时设置 SourceDevice 触发 OnSourceDeviceChanged → SaveConfig → 此时 RoutingTargets 为空 → 覆盖文件中的 targets 为 [] |
| `SettingsPage.xaml` | 移除延迟模式区块、已保存设备列表；新增界面设置（字体大小）、语言选择、启动选项（开机自启）、关于信息 | 延迟已在主页面按设备独立调节；设置应管 UI 和程序行为 |
| `SettingsViewModel.cs` | 新增 FontSize/SelectedLanguage/IsAutoStartEnabled 属性；实现 Windows 注册表开机自启读写 | 支持新 UI 控件 |
| `AppConfiguration.cs` | 新增 FontSize(int) 和 Language(string?) 属性 | 持久化 UI 设置 |
| `TrayIconManager.cs` | CreateTrayIcon() 优先加载 trayicon.ico (System.Drawing.Icon)，fallback 到 emoji | 用户要求使用自定义 ICO 文件 |
| `WinAudioRouter.App.csproj` | 添加 trayicon.ico 作为 None 内容项 CopyToOutputDirectory | 确保 ICO 随构建输出到运行目录 |
| `RingBufferWaveProvider.cs` | Read() 方法：数据不足时循环重复已有数据替代 Array.Clear(静音) | 静音填充产生音频间隙 → 低频波形不连续 → 听感低频减弱 |

### 问题记录

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 配置重启后 targets 全部丢失 | LoadDevicesAsync 中先设 SourceDevice 触发 SaveConfig，此时 RoutingTargets 还在恢复循环之前为空 | 添加 _isInitializing 标志，初始化期间所有 SaveConfig 直接 return |
| 托盘图标显示 emoji 🎧 不专业 | 使用 H.NotifyIcon 的 GeneratedIconSource 渲染文字图标 | 改用 System.Drawing.Icon 加载 trayicon.ico，保留 fallback |
| 路由音频低频感觉变少 | RingBufferWaveProvider.Read() 在 bytesRead < count 时执行 Array.Clear 填充静音，产生间隙。低频波长长（50Hz=20ms），对间隙极度敏感 | 改为将已有数据循环重复填满 buffer，保持低频波形连续性 |

### 配置文件格式变更

```json
{
  "latencyMode": "Normal",
  "fontSize": 15,
  "language": "简体中文",
  "routing": { ... },
  "bluetooth": { ... }
}
```

新增字段：`fontSize`（默认 15）、`language`（默认 "简体中文"）

### 开机自启实现方式

Windows 注册表 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- 键名：`WinAudioRouter`
- 值：`"exe完整路径"`
- 启用时写入，关闭时删除
