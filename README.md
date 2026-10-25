# Codex Live Usage

A local Windows companion that keeps Codex token usage visible below the latest
assistant response.

> Unofficial community project. Not affiliated with or endorsed by OpenAI.

The overlay reproduces the compact Codex++ token badge style while using a
different technical route:

- Win32 locates and follows the Codex Desktop window.
- Windows UI Automation locates the latest assistant response and composer.
- A transparent WPF window renders one click-through badge.
- Local rollout JSONL files provide numeric token metrics.

No Codex++, CDP, DOM injection, proxy, API key, or network request is used.

## Features

- Original compact badge style: teal border, subtle teal background, 7 px
  radius, 12 px single-line text, and 90% opacity.
- Context used/limit and percentage.
- Cache hit rate.
- Latest-turn input and output tokens.
- Output speed in tokens per second.
- Session total tokens.
- Automatic login startup and Codex window following.
- Hides when Codex is minimized, closes, or has no valid assistant anchor.
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

Or run `Install.cmd`.

The installer:

1. Runs the automated tests and publishes the app.
2. Copies the app to `%LOCALAPPDATA%\CodexLiveUsage\app`.
3. Registers the per-user login task `Codex Live Usage Companion`.
4. Starts the companion immediately.

No administrator permission is required.

## Use

Open Codex Desktop and select a conversation with token data. The badge appears
below the latest assistant response. It follows the Codex window automatically.

Example:

```text
上下文 110.9K/1.0M (10.6%) · 缓存命中率 92% · 输入 108K · 输出 146 · 输出速度 33.5 tok/s · 会话累计 1.16M
```

The badge hides rather than showing invented zero values when no valid metric
or assistant anchor is available.

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

Conversation content is never stored, logged, or transmitted. The optional
diagnostics mode writes only numeric metrics and window rectangles:

```powershell
CodexLiveUsage.exe --diagnostics .\diagnostics.json
```

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
```

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md) before
submitting changes.

## Compatibility

Codex Desktop's rollout JSONL schema and UI Automation structure are internal
implementation details and can change. The tests cover parsing, placement,
window matching, and UI-status filtering, but the end-to-end integration must be
checked after significant Codex updates.

The first version uses the most recently modified rollout. Switching to an idle
conversation that has not written new token events may not immediately update
the displayed session.

## License

MIT. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
