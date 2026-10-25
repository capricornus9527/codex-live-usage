# Codex 常驻用量伴随插件设计

**日期：** 2026-10-25

**状态：** 已实现并验证

## 目标

为 Windows 官方 Codex 桌面应用提供一个本地常驻透明单行用量状态。状态固定在输入框外框正下方，不随回复正文高度移动；跟随 Codex 窗口出现、移动和最小化，不需要 Codex++、CDP、DOM 注入或模型调用。

用户只需要完成一次插件安装。之后 Windows 登录时启动隐藏监督进程，检测到 Codex 主窗口和输入框后显示透明单行用量。

## 非目标

- 不把状态文字插入 Codex 对话 DOM 或每条回复内部。
- 不修改 Codex、`app.asar`、登录信息、模型配置或会话文件。
- 不读取 API Key、账号凭据或对话正文。
- 不访问网络，不计算账单金额，不声称本地统计等于服务端账单。
- 首版不支持 Codex CLI、macOS 或 Linux。
- 首版不实现 Codex++ 的网络拦截、WebSocket 监听或 API 响应解析。

## GitHub 调研

| 项目 | 版本/许可 | 可借鉴内容 | 采用方式 |
| --- | --- | --- | --- |
| [gkfriend/codex-usage-companion](https://github.com/gkfriend/codex-usage-companion/tree/7aa4643faeecda977d2bd59fca1a96fa3e85dd7b) | `7aa4643`，MIT | C# 无窗口伴随进程、Win32 窗口定位、Codex 窗口生命周期 | 借鉴架构与测试拆分，不直接复制整套应用 |
| [soleillevant0125/codex-token-overlay](https://github.com/soleillevant0125/codex-token-overlay/tree/b3a38d727fb2e0cf8e8c92ffff3f65da9a592dc5) | `b3a38d7`，MIT | WinForms 胶囊窗口、Codex 窗口附着、token 指标口径 | 借鉴窗口跟随和指标口径，代码重写并保留来源说明 |
| [Javis603/token-monitor](https://github.com/Javis603/token-monitor/tree/e6562f2dfcdc8de9eb4244b327a048774c4419d9) | `e6562f2`，MIT | 增量读取 rollout、缓存统计、本地优先数据边界 | 借鉴增量解析和错误降级思路 |
| [frankfoxy/codex-session-bar](https://github.com/frankfoxy/codex-session-bar/tree/83cbcb6bddb085c1fda6826064571ba40dfc8317) | `83cbcb6`，未声明仓库级许可 | Rust 原生 Windows 窗口发现、UI Automation 锚点 | 只参考思路，不复制代码 |

现有 API 余额小鲸鱼继续作为启动和跟随机制的本地参考。新插件不修改小鲸鱼，也不依赖其文件或运行时。

## 技术选择

- **语言/UI：** C#、WPF、`net9.0-windows`。
- **原因：** 当前机器已有 .NET 9 SDK 和 Windows Desktop Runtime；WPF 能直接绘制完全透明的外层窗口和抗锯齿文字，不需要复制约 380 MB 的 Electron 运行时。
- **进程：** 单个 WinExe 伴随进程，隐藏启动，不显示控制台、任务栏按钮或 Alt+Tab 项。
- **启动：** 安装脚本注册当前用户级计划任务 `Codex Live Usage Companion`，登录时启动；不要求管理员权限。
- **窗口跟随：** Win32 枚举 Codex 主窗口，UI Automation 定位输入框外层容器，使用 owner window 关系并轮询窗口位置、输入框位置、可见性和最小化状态。
- **当前会话：** 通过 `\\.\pipe\codex-ipc` 的 `thread-stream-following-changed` 消息跟踪当前选中的 conversation ID；IPC 不可用时才回退到最新 rollout。
- **数据：** 只读取 `$CODEX_HOME/sessions` 或 `~/.codex/sessions` 中与当前 thread 对应的 `rollout-*.jsonl` 数值字段。

计划任务不是 Codex 官方插件自动启动能力，而是与 API 余额小鲸鱼相同的本地伴随程序机制。插件包用于分发、安装说明和可控安装/卸载，不依赖 MCP 自动渲染。

## 用户体验

### 默认布局

- 只有一行状态文字，文案沿用 Codex++ Token Usage 的命名：

  `上下文：110.9K/1.0M (10.6%)        缓存命中率：92%        输入：108K        输出：146        输出速度：33.5 tok/s`

- 默认居中位于输入框外框正下方。回复内容和滚动位置变化不能改变纵向位置。
- 窗口和文字背景完全透明，没有卡片、底色、边框、圆角或阴影。
- 指标之间使用六个普通空格分隔，不使用中点、竖线或其他可见分隔符。
- 不接收鼠标点击和键盘焦点，不打断 Codex 输入。
- Codex 最小化、隐藏或退出时状态行同步隐藏，不覆盖其他应用。
- 找不到输入框、底部空间不足或没有有效 token 数据时隐藏状态行，不显示 0、等待提示或虚构值。
- 宽度不足时按优先级依次省略输出速度、输入输出、缓存命中率；上下文占用及其上限始终保留。

### 文案规范

Codex++ Token Usage 原先使用以下名称：

- `本轮调用合计`
- `输入`
- `输出`
- `缓存读`
- `缓存命中率`
- `上下文 已用/上限 (百分比)`
- `调用`
- `耗时`

单行版本保留核心名称，并明确上下文上限：

- 上下文：`上下文 {已用}/{上限} ({百分比}%)`
- 缓存：`缓存命中率：{百分比}%`
- 本轮输入：`输入：{tokens}`
- 本轮输出：`输出：{tokens}`
- 输出速度：`输出速度：{数值} tok/s`

首版不显示会话累计，以便五项核心指标完整放入输入框宽度。

不使用含糊的“缓存”“上下文占用”或单独的“上限”标签。数字使用紧凑单位，例如 `110.9K`、`1.0M`。

### 视觉约束

- 外层窗口和文字背景完全透明，不能出现任何底色或窗口边缘。
- 字号 12 px，行高约 14 px，文字不透明度为 100%，浅色主题使用取样自 Codex 思考状态的 `RGB(167, 168, 169)`。
- 中文标签使用 `Microsoft YaHei UI`。
- 数字、括号、百分比和拉丁单位使用 `Times New Roman`。
- 输入框下方高度不足时，按 12 → 11 → 10 → 9 px 逐级压缩并减少间距，而不是直接隐藏。
- 浅色主题使用黑色文字；深色主题使用 Codex 对应的浅色正文，避免不可读。
- 不使用渐变、阴影、发光或额外装饰。
- 使用稳定的单行布局和等宽数字特性，数字变化不能让文字左右抖动。
- 指标之间使用八个空格；文字过长时按优先级省略，不换行、不覆盖输入框。

## 架构

```text
Codex Live Usage.exe
  ├─ SingleInstanceGuard
  ├─ CodexWindowTracker
  │    ├─ 发现 ChatGPT.exe / OpenAI Codex 主窗口
  │    ├─ 跟踪 bounds、foreground、minimized、DPI
  │    └─ 管理 owner window 关系和显示/隐藏
  ├─ ComposerLocator
  │    ├─ UI Automation 查找 Codex 输入框外层容器
  │    └─ 计算输入框下方的固定文字位置
  ├─ CodexIpcActiveThreadMonitor
  │    ├─ 只读连接 Codex 本机命名管道
  │    ├─ 跟踪当前窗口的 conversation ID
  │    └─ IPC 不可用时回退到最新 rollout
  ├─ UsageLine
  │    ├─ 完全透明的单行文字
  │    ├─ 八个空格分隔指标
  │    ├─ 主题前景色
  │    └─ 数值和错误状态
  ├─ RolloutMetricsMonitor
  │    ├─ 查找最新 rollout
  │    ├─ 增量读取新增行
  │    ├─ 解析 token_count
  │    └─ 计算上下文、缓存和速度
  └─ SettingsStore
       └─ 安装状态（由安装脚本维护）
```

### 进程生命周期

1. 登录任务启动 `CodexLiveUsage.exe --background`。
2. 使用当前用户命名 Mutex 保证单实例。
3. 未发现 Codex 时保持低频率窗口探测，不创建可见窗口。
4. 发现 Codex 主窗口、输入框外框和有效指标后创建固定位置的透明状态行。
5. Codex 关闭、输入框不可见或找不到输入框时隐藏状态行并继续等待下一次更新。
6. 用户运行卸载脚本时停止进程、注销任务并删除安装文件。

首版进程在用户注销前常驻；Codex 关闭时只隐藏窗口，避免每轮重新启动带来的延迟。

### 数据规则

- `last_token_usage` 表示最近一轮。
- `total_token_usage` 表示当前 rollout 的累计量。
- 上下文占用使用 `last_token_usage.total_tokens / model_context_window`。
- 缓存命中率使用 `cached_input_tokens / input_tokens`，限制在 0% 到 100%。
- 输出速度使用最近两个有效累计快照的 output token 增量和时间差。
- 文件截断、轮转或回退时重置读取偏移并重新解析。
- 不完整 JSON 行保留到下一轮；损坏行跳过。
- 所有数据先归一化为非负数字，未知值保持 `null`。
- 只保留数值快照和最近更新时间，不保存事件正文。

### 当前会话规则

Codex IPC 报告选中对话后，程序立即切换到该 thread 对应的 rollout。切换后的第一次显示将该对话速度初始化为 `0 tok/s`，避免把上一个对话或历史生成速度带入新对话。即使目标 rollout 尚未产生 token_count，也显示当前 thread 的完整零值行。

IPC 只用于读取 conversation ID 和 following 状态，不读取消息正文、凭据或账号数据。

## 插件包

建议插件标识：`codex-live-usage@personal`。

```text
codex-plugins/codex-live-usage/
  .codex-plugin/plugin.json
  skills/codex-live-usage/SKILL.md
  src/CodexLiveUsage/
  tests/CodexLiveUsage.Tests/
  scripts/install.ps1
  scripts/uninstall.ps1
  scripts/build.ps1
  THIRD_PARTY_NOTICES.md
  README.md
```

插件不包含 MCP server。安装、启动、停止和卸载由脚本负责，避免把“按需 MCP 工具”误当成常驻 UI。

## 安装与卸载

### 安装

1. 检查 .NET 9 桌面运行时、Windows x64 和 Codex 可执行文件。
2. 构建或将构建产物发布到插件目录。
3. 注册当前用户登录计划任务。
4. 启动伴随进程并等待 Codex 窗口。
5. 输出任务状态、进程状态和配置路径。

安装脚本不修改 Codex 配置、插件市场或其他插件。个人市场注册由单独的插件安装步骤完成。

### 卸载

1. 停止已验证的伴随进程。
2. 注销计划任务。
3. 清理解析缓存和运行日志。
4. 是否删除整个插件目录由卸载参数控制。
5. 不删除 Codex sessions、账号文件或小鲸鱼数据。

## 故障处理

- **Codex 未启动：** 保持等待，不记录错误弹窗。
- **窗口不可见/最小化：** 隐藏状态行，不关闭监控。
- **无 rollout 或字段缺失：** 当前 thread 显示完整的 0-token 行。
- **找不到输入框：** 隐藏状态行；输入框恢复后自动显示。
- **底部空间不足：** 逐级压缩字号和间距；极端情况下才隐藏。
- **窗口路径变化：** 先按进程可执行文件路径识别，再以窗口标题和 owner 状态辅助。
- **读文件失败：** 保留最近成功值并标记“本地数据暂不可用”。
- **计划任务未启动：** 安装脚本失败退出并报告任务计划程序错误。

## 验证

### 自动测试

- 解析有效 token_count、缺失字段、非法值、损坏行和不完整行。
- 验证上下文比例、缓存命中率、速度和时间窗口。
- 验证 rollout 轮转、增量 offset 和重复事件不会重复累计。
- 验证当前 conversation 路由、空闲切换、0 tok/s 初始化和零值行。
- 验证 12/11/10/9 px 自适应字号和仅剩 23 px 时的显示。
- 验证输入框外框锚点、固定纵向位置、DPI 缩放、多显示器和最小化隐藏。
- 运行 `dotnet build` 和自定义无第三方依赖测试运行器。

### 安装与运行验证

- `Get-ScheduledTask` 显示任务已注册并运行。
- 只存在一个伴随进程。
- Codex 启动后单行透明状态自动出现，不抢占输入焦点。
- 移动、缩放、滚动、最小化和恢复 Codex 时，状态行固定于输入框下方且可见性正确。
- 切换有新增 token 的对话后，数值在一个刷新周期内更新。
- 在另一个空闲对话与当前对话之间切换，确认指标来自各自对话且速度在切换后先显示 0。
- 截图确认背景完全透明，只有一行文字，指标之间为空格分隔。
- 卸载后任务、进程和缓存均消失，Codex 会话文件未改变。

## 许可与来源

新实现不得直接复制未声明仓库级许可的代码。参考 MIT 项目的代码片段时，保留版权声明并在 `THIRD_PARTY_NOTICES.md` 中记录来源、提交和用途。
