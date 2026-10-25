---
name: codex-live-usage
description: Install, start, inspect, or uninstall the local Codex Live Usage Windows companion.
---

# Codex Live Usage

Use this skill only when the user asks to install, start, inspect, or uninstall
the Codex Live Usage companion.

The companion is a local Win32/WPF process. It is not an MCP app, does not call
the model, and does not modify Codex files.

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
Get-Process -Name CodexLiveUsage
```

Report the task state, process count, and whether the Codex window is visible.
Do not read or quote rollout conversation content.

## Uninstall

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

The uninstaller removes only the login task and `%LOCALAPPDATA%\CodexLiveUsage`.
It must not touch Codex sessions, credentials, configuration, or other plugins.
