# WinAudioRouter 文档中心

> 所有项目文档的入口和导航

---

## 📂 目录结构

```
docs/
├── README.md                    ← 你在这里
├── dev-logs/                    ← 开发日志（按日期+主题归档）
│   ├── 2026-05-29-phase1.md            项目初始化与核心功能
│   ├── 2026-05-29-phase3-bluetooth.md  蓝牙集成
│   ├── 2026-05-29-ui-fixes.md          UI修复与稳定性
│   ├── 2026-05-29-volume-routing.md    音量控制/路由卡顿/中文UI
│   ├── 2026-05-29-multitarget.md       1:N多目标路由重构
│   ├── 2026-05-30-wasapi-refactor.md   底层WASAPI直接渲染方案
│   ├── 2026-05-30-bugfix-optimization.md  Bug修复+功能优化
│   ├── 2026-05-30-exit-fix-config-persistence.md  退出修复+延迟优化+配置持久化
│   └── 2026-05-30-settings-redesign-icon-lowfreq.md  设置重设计+托盘图标+低频修复
├── architecture/                ← 架构文档
│   └── audio-routing.md                音频路由架构说明
├── plans/                       ← 项目计划
│   └── project-plan.md                 项目实现计划（含GitHub调研）
└── issues/                      ← 问题追踪
    └── known-issues.md                 已知问题列表
```

---

## 📋 开发日志索引

| 日期 | 文件 | 主题 | 关键变更 |
|------|------|------|---------|
| 05-29 | [phase1](dev-logs/2026-05-29-phase1.md) | 项目初始化+核心功能 | MAUI项目、WASAPI设备枚举、路由引擎、COM设备切换、托盘图标 |
| 05-29 | [bluetooth](dev-logs/2026-05-29-phase3-bluetooth.md) | 蓝牙集成 | BluetoothViewModel、BluetoothPage、DI注册 |
| 05-29 | [ui-fixes](dev-logs/2026-05-29-ui-fixes.md) | UI修复与稳定性 | 字体图标→emoji、防抖、TabBar导航 |
| 05-29 | [volume-routing](dev-logs/2026-05-29-volume-routing.md) | 音量/卡顿/中文UI | 音量实际控制、缓冲区优化、全面中文化 |
| 05-29 | [multitarget](dev-logs/2026-05-29-multitarget.md) | 1:N多目标路由 | RoutingTarget模型、多路分发架构 |
| 05-30 | [wasapi-refactor](dev-logs/2026-05-30-wasapi-refactor.md) | WASAPI直接渲染 | LockFreeRingBuffer、WasapiLowLatencyRenderer、事件驱动 |
| 05-30 | [bugfix](dev-logs/2026-05-30-bugfix-optimization.md) | Bug修复+优化 | COM冲突、音量卡死、退出修复、蓝牙扫描优化 |
| 05-30 | [exit-fix](dev-logs/2026-05-30-exit-fix-config-persistence.md) | 退出修复+延迟+持久化 | H.NotifyIcon SecondWindow、延迟0ms、RingBuffer重建、配置JSON持久化、GitHub发布 |
| 05-30 | [settings](dev-logs/2026-05-30-settings-redesign-icon-lowfreq.md) | 设置重设计+图标+低频 | 配置初始化竞态修复、字体/语言/开机自启、favicon.ico托盘图标、低频数据重复填充 |

---

## 🏗️ 架构文档

| 文件 | 说明 |
|------|------|
| [audio-routing.md](architecture/audio-routing.md) | 音频路由架构：WASAPI Loopback → LockFreeRingBuffer → WasapiOut 1:N 分发 |

---

## 🐛 问题追踪

| 文件 | 说明 |
|------|------|
| [known-issues.md](issues/known-issues.md) | 已知问题列表（含已解决和未解决） |

---

## 📝 开发日志编写规范

### 文件命名

```
docs/dev-logs/YYYY-MM-DD-主题关键词.md
```

示例：
- `2026-05-30-bugfix-optimization.md`
- `2026-05-31-eq-equalizer.md`
- `2026-06-01-startup-perf.md`

### 模板

```markdown
# YYYY-MM-DD 主题标题

---

### 完成的任务

| # | 任务 | 涉及文件 | 状态 |
|---|------|---------|------|
| N | 任务描述 | 文件名 | ✅/❌ |

### 新增文件

| 文件 | 用途 |
|------|------|
| 路径 | 说明 |

### 关键修改

| 文件 | 修改 | 原因 |
|------|------|------|
| 路径 | 改了什么 | 为什么改 |

### 问题记录

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 描述 | 根因 | 怎么修的 |
```

### 规则

1. **一天多个主题** → 拆分为多个文件，用 `-关键词` 区分
2. **文件名简洁** → 不超过 3 个单词的 kebab-case
3. **每次代码变更后立即更新** → 无记录视为未完成
4. **新文件创建后同步更新本 README 的索引表**
