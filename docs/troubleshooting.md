# 故障排查

## 切换对话后数据没有更新

Codex Live Usage 使用本机命名管道：

```text
\\.\pipe\codex-ipc
```

监听 `thread-stream-following-changed` 广播，并根据以下事件确定当前对话：

```text
旧 conversationId, following = false
新 conversationId, following = true
```

发生真实对话切换时，程序会立即切换 rollout，并将新对话的输出速度初始化为
`0 tok/s`。只有收到后续 token 事件后才更新速度。

如果界面已经切换但指标没有变化：

1. 运行 `CodexLiveUsage.exe --diagnostics .\diagnostics.json`。
2. 检查 `ipc.IsConnected`、`ipc.ThreadId` 和 `ipc.RecentEvents`。
3. 正常连接应同时出现旧对话 `following=False` 和新对话 `following=True`。
4. 如果 IPC 断开，程序会暂时回退到最新修改的 rollout；IPC 恢复后自动回到当前对话。

用于连续观察的无界面诊断方式：

```powershell
CodexLiveUsage.exe --ipc-watch .\ipc-events.json 15
```

诊断只记录 thread ID、following 状态和窗口矩形，不记录消息正文、提示词或模型回复。

## 输入框下方空间不足

程序会先读取 Codex 输入框外层容器的底边，再根据窗口底部剩余高度依次尝试：

| 字号 | 行高 | 与输入框间距 |
| --- | --- | --- |
| 12px | 13.5px | 2px |
| 11px | 12.5px | 1px |
| 10px | 11.5px | 1px |
| 9px | 10.5px | 0px |

即使输入框下方只剩 23px，12px 档也应保持可见。只有所有档位都无法放入窗口时才隐藏。

如果用量行消失，请先检查：

```powershell
Get-Process -Name CodexLiveUsage
Get-ScheduledTask -TaskName 'Codex Live Usage Companion'
```

然后运行诊断模式，确认 `window.Bounds`、`composer` 底边和实际可用高度。

## 用量行消失或伴随进程退出

计划任务 `Codex Live Usage Companion` 同时包含登录触发和每分钟恢复触发。
进程异常退出后，下一次触发会重新启动；如果进程仍在运行，单实例互斥锁会让
新实例立即退出，不会产生重复窗口。

先检查任务、上次运行结果和进程：

```powershell
Get-ScheduledTask -TaskName 'Codex Live Usage Companion'
Get-ScheduledTaskInfo -TaskName 'Codex Live Usage Companion'
Get-Process -Name CodexLiveUsage
```

程序会把未处理异常写入：

```text
%LOCALAPPDATA%\CodexLiveUsage\error.log
```

日志超过 1 MiB 后改名为 `error.log.old`。日志可能包含异常调用栈和本机路径，
但不记录提示词、模型回复或其他对话正文。提交问题前请先检查并删除个人信息。

已观察到的崩溃签名是：

```text
PresentationCore
System.OutOfMemoryException
```

`0.1.1` 已缓存字体、画刷、文本段和测量结果，并捕获未处理异常，以减少刷新时
反复重建 WPF 对象和便于后续定位。WPF 内存异常可能还有其他触发条件，因此仍需
结合新生成的 `error.log` 判断。

需要立即恢复时可以运行：

```powershell
Start-ScheduledTask -TaskName 'Codex Live Usage Companion'
```
