---
name: codex-live-usage
description: Install, start, inspect, or uninstall the local Codex Live Usage Windows companion.
---

# Codex Live Usage

Use this skill only when the user asks to install, start, inspect, or uninstall
the Codex Live Usage companion.

The companion is a local Win32/WPF process that renders a transparent line below
the Codex composer. It is not an MCP app, does not call the model, and does not
modify Codex files. A per-user scheduled task starts it at login and checks once
per minute so a crashed process can recover.

## Install

From the plugin root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

After installation, verify:

```powershell
Get-ScheduledTask -TaskName 'Codex Live Usage Companion'
Get-Process -Name CodexLiveUsage
```

## Status

Run:

```powershell
Get-ScheduledTask -TaskName 'Codex Live Usage Companion'
Get-ScheduledTaskInfo -TaskName 'Codex Live Usage Companion'
Get-Process -Name CodexLiveUsage
```

Report the task state, last result, process count, and whether the Codex window
is visible. If the process exited unexpectedly, check:

```text
%LOCALAPPDATA%\CodexLiveUsage\error.log
```

The log is created on unhandled errors and rotated to `error.log.old` after
1 MiB. It may contain exception stacks, but the companion does not record
conversation content.

Do not read or quote rollout conversation content.

## Uninstall

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

The uninstaller removes only its scheduled task and
`%LOCALAPPDATA%\CodexLiveUsage`. It must not touch Codex sessions, credentials,
configuration, or other plugins.
