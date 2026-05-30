# 已知问题追踪

> 最后更新: 2026-06-01

---

## 未解决

| # | 问题 | 严重度 | 涉及模块 | 备注 |
|---|------|--------|---------|------|
| 1 | DefaultDeviceSwitcher COM 互操作失败 | 中 | Native/Interop | IPolicyConfigVista.SetDefaultEndpoint 返回错误 HRESULT |
| 2 | 蓝牙 BLE 广播名称为空 | 低 | Core/Bluetooth | 部分设备 FromIdAsync 返回 Name 为空，需二次解析 |
| 3 | 软件启动速度慢 | 低 | App | MAUI 框架初始化开销大 |
| 4 | FX/EQ 均衡器未实现 | 功能 | Core/Audio | 需要研究开源音频处理库 |

## 已解决

| # | 问题 | 解决方案 | 解决日期 |
|---|------|---------|---------|
| 5 | COM 类型冲突闪退 | 放弃自定义 COM 接口，使用 NAudio WasapiOut(eventCallback=true) | 2026-05-30 |
| 6 | 音量调节卡死 | 移除 ValueChanged 事件，改用 ViewModel partial method | 2026-05-30 |
| 7 | 软件无法正常退出（v1） | Process.Kill() 替代 TerminateProcess P/Invoke | 2026-05-30 |
| 8 | 路由声音卡顿/破音 | WasapiOut(eventCallback=true) + LockFreeRingBuffer | 2026-05-30 |
| 9 | 蓝牙扫描慢 | FromIdAsync 并行化 + 3秒超时 + Cached 模式 | 2026-05-30 |
| 10 | 应用退出无反应（H.NotifyIcon Click 不触发） | ContextMenuMode=SecondWindow + 直接 Process.Kill() | 2026-05-30 |
| 11 | 调高延迟后目标设备无声 | UpdateTargetLatencyAsync 重建 RingBuffer 而非 Reset() | 2026-05-30 |
| 12 | 配置重启后丢失 | AppConfiguration 扩展 RoutingConfiguration，JSON 持久化到 %APPDATA% | 2026-05-30 |
| 13 | 配置初始化竞态：SaveConfig 覆盖空 Targets | _isInitializing 标志，初始化期间跳过 SaveConfig | 2026-05-30 |
| 14 | 托盘图标显示 emoji 不专业 | System.Drawing.Icon 加载 trayicon.ico，保留 fallback | 2026-05-30 |
| 15 | 路由音频低频变少 | RingBufferWaveProvider 数据重复循环代替静音填充 | 2026-05-30 |
