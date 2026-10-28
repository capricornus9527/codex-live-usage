# Codex Live Usage

English | [简体中文](README.zh-CN.md)

[![CI](https://github.com/capricornus9527/codex-live-usage/actions/workflows/ci.yml/badge.svg)](https://github.com/capricornus9527/codex-live-usage/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A local Windows companion that keeps Codex token usage in a fixed, transparent
line directly below the composer.

> Unofficial community project. Not affiliated with or endorsed by OpenAI.

The overlay keeps the Codex++ metric names while using a different technical
route:

- Win32 locates and follows the Codex Desktop window.
- Windows UI Automation locates the composer container's bottom edge.
- A transparent WPF window renders one click-through text line.
- Local rollout JSONL files provide numeric token metrics.
- Local Codex IPC tracks the currently selected conversation.

No Codex++, CDP, DOM injection, proxy, API key, or network request is used.

![Codex Live Usage line](docs/images/usage-line.png)

## Features

- Fixed position directly below the Codex composer.
- Fully transparent background with no border, card, or separator symbols.
- Centered below the composer with eight spaces between metrics.
- Rendered as one fully opaque 12 px line using Codex's muted-gray text color.
- Chinese labels use Microsoft YaHei; numbers and Latin text use Times New Roman.
- Adaptively reduces to 11, 10, or 9 px when the window leaves less vertical space.
- Context used/limit and percentage.
- Cache hit rate.
- Latest-turn input and output tokens.
- Output speed in tokens per second.
- Switches to the selected conversation's own rollout data, even when it is idle.
- Shows `0 tok/s` immediately after a conversation switch until new output arrives.
- Automatic login startup, Codex window following, and an every-minute recovery
  check protected by a single-instance mutex.
- Reuses WPF fonts, brushes, text runs, and measured sizes during value updates.
- Writes unhandled exceptions to a local rotating error log for diagnosis.
- Hides when Codex is minimized, closes, has no composer anchor, or lacks
  bottom space for the line.
- Read-only local operation with no telemetry.

## Requirements

- Windows 10 or Windows 11 x64.
- Codex Desktop.
- Microsoft Windows Desktop Runtime 9.x for source installs.

The companion does not require Codex++ and does not modify Codex installation
files or configuration.

## Install

From the repository root:

```powershell
git clone https://github.com/capricornus9527/codex-live-usage.git
cd codex-live-usage
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

Or run `Install.cmd`.

The installer:

1. Runs the automated tests and publishes the app.
2. Copies the app to `%LOCALAPPDATA%\CodexLiveUsage\app`.
3. Registers the per-user `Codex Live Usage Companion` task with login and
   every-minute recovery triggers.
4. Starts the companion immediately.

No administrator permission is required.

## Use

Open Codex Desktop and select a conversation with token data. The line appears
directly below the composer and follows the Codex window automatically.

Example:

```text
上下文：110.9K/1.0M (10.6%)        缓存命中率：92%        输入：108K        输出：146        输出速度：33.5 tok/s
```

The line hides rather than showing invented zero values when no valid metric
or composer anchor is available.

## Uninstall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

Or run `Uninstall.cmd`.

Uninstall removes the login task, companion process, local app copy, and runtime
state. It never removes Codex sessions, credentials, configuration, or other
plugins.

## Data and privacy

The companion reads only numeric fields from the latest
`rollout-*.jsonl` under:

```text
$CODEX_HOME\sessions
```

If `CODEX_HOME` is unset, it uses `%USERPROFILE%\.codex\sessions`.

Conversation content is never stored, logged, or transmitted. The local
`\\.\pipe\codex-ipc` connection is used only to identify the selected
conversation ID. The optional diagnostics mode writes numeric metrics, thread
IDs, and window rectangles:

```powershell
CodexLiveUsage.exe --diagnostics .\diagnostics.json
```

Unhandled exceptions are written to
`%LOCALAPPDATA%\CodexLiveUsage\error.log` so a vanished companion can be
diagnosed. The file rotates to `error.log.old` after 1 MiB. Exception logs may
contain stack traces, but conversation content is not included.

## Development

Run tests and publish:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Build without rerunning tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -SkipTests
```

Direct test command:

```powershell
dotnet run --project .\tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\InstallScript.Tests.ps1
```

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) before
submitting changes.

See [CHANGELOG.md](CHANGELOG.md) for version history.

See [docs/troubleshooting.md](docs/troubleshooting.md) for startup recovery,
current-conversation IPC, crash-log, and adaptive-layout diagnostics.

## Verification

- 20 automated C# parser, routing, placement, formatting, font-role,
  zero-state, and window-matching tests.
- PowerShell install validation for .NET 9 detection and directory publishing.
- Release build completed with zero warnings and zero errors.
- Real Codex window follow, minimize/restore hiding, and click-through styles
  were verified on Windows 11 with the Microsoft Store build.
- Killing the companion manually was followed by automatic restart on the next
  scheduled recovery check while leaving one process and one visible line.

## Compatibility

Codex Desktop's rollout JSONL schema and UI Automation structure are internal
implementation details and can change. The tests cover parsing, placement,
window matching, and font segmentation, but the end-to-end integration must be
checked after significant Codex updates.

If Codex IPC is temporarily unavailable, the companion falls back to the most
recently modified rollout until the selected-conversation route reconnects.

## License

MIT. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
