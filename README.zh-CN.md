# Codex 常驻用量

[English](README.md) | 简体中文

[![CI](https://github.com/capricornus9527/codex-live-usage/actions/workflows/ci.yml/badge.svg)](https://github.com/capricornus9527/codex-live-usage/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

一个本地 Windows 伴随工具，在 Codex 输入框下方显示固定、透明的一行 token 用量。

> 非 OpenAI 官方项目，与 OpenAI 没有隶属、赞助或背书关系。

![用量行效果](docs/images/usage-line.png)

## 功能

- 固定在 Codex 输入框下方居中显示，不随对话滚动或回复高度移动。
- 完全透明背景，没有卡片、边框、阴影或分隔符。
- 中文标签使用微软雅黑，数字和拉丁字母使用 Times New Roman。
- 字号 12px、不透明度 100%，使用接近 Codex 思考状态的灰色。
- 每项采用 `标签：数值`，项目之间使用 8 个空格。
- 显示上下文用量和上限、缓存命中率、本轮输入输出、输出速度。
- 切换到空闲对话后立即显示该对话自己的数据，输出速度先归零。
- 根据窗口底部剩余空间，在 12/11/10/9px 字号之间自动压缩。
- 自动跟随 Codex 窗口移动、最小化和恢复。
- 本地只读，不联网、不读取 API Key，不上传对话内容。

效果示例：

```text
上下文：110.9K/1.0M (10.6%)        缓存命中率：92%        输入：108K        输出：146        输出速度：33.5 tok/s
```

## 实现方式

- Win32 查找并跟随 Codex Desktop 主窗口。
- Windows UI Automation 定位输入框外层容器。
- WPF 透明度窗口绘制一行可点击穿透的文字。
- 增量读取 `rollout-*.jsonl` 中的数字字段。
- 通过 Codex 本机 IPC 跟踪当前选中的对话。

不依赖 Codex++，不使用 CDP、DOM 注入、代理或网络服务。

## 环境要求

- Windows 10 或 Windows 11 x64。
- 官方 Codex Desktop。
- Microsoft Windows Desktop Runtime 9.x。

## 安装

在仓库根目录运行：

```powershell
git clone https://github.com/capricornus9527/codex-live-usage.git
cd codex-live-usage
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

也可以双击 `Install.cmd`。

安装脚本会：

1. 运行自动测试并发布 Release。
2. 将程序复制到 `%LOCALAPPDATA%\CodexLiveUsage\app`。
3. 注册当前用户登录任务 `Codex Live Usage Companion`。
4. 立即启动伴随程序。

不需要管理员权限。

## 使用

打开 Codex 并进入有 token 数据的工作任务。用量行会自动显示在输入框下方。Codex 最小化、关闭、找不到输入框或底部空间不足时，用量行自动隐藏。

## 卸载

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

也可以双击 `Uninstall.cmd`。

卸载会移除登录任务、伴随进程、本地安装副本和运行状态，不会修改 Codex 会话、凭据、配置或其他插件。

## 数据与隐私

程序只读取最新 rollout 文件中的 token 数字字段：

```text
$CODEX_HOME\sessions
```

如果未设置 `CODEX_HOME`，使用：

```text
%USERPROFILE%\.codex\sessions
```

对话正文不会被保存、记录或发送。诊断模式只写入数字、指标和窗口矩形：

```powershell
CodexLiveUsage.exe --diagnostics .\diagnostics.json
```

## 开发

运行完整测试并发布：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

直接运行测试：

```powershell
dotnet run --project .\tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\InstallScript.Tests.ps1
```

## 兼容性

Codex Desktop 的 rollout JSONL、本机 IPC 和 UI Automation 结构属于内部实现，未来版本可能变化。当前测试覆盖解析、会话路由、增量读取、定位、字体分段和窗口匹配，但对新版 Codex 仍应执行实际界面验证。

如果 Codex 本机 IPC 暂时不可用，程序会回退到最近修改的 rollout；IPC 恢复后会自动回到当前选中的对话。

## 许可证

MIT，详见 [LICENSE](LICENSE) 和 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
