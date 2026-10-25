# Codex Live Usage Companion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and install a Windows companion that displays Codex++-style token usage under the latest Codex assistant reply without Codex++, MCP UI injection, CDP, or network access.

**Architecture:** A boundary-focused .NET 9 core library parses incremental rollout JSONL and computes token metrics. A WPF `net9.0-windows` companion locates the Codex main window with Win32, locates the latest assistant reply with UI Automation, and renders one transparent badge in the original Codex++ style. A current-user login task starts the companion automatically.

**Tech Stack:** C# 13, .NET 9, WPF, Win32 interop, Windows UI Automation, PowerShell 5.1/7, no third-party NuGet packages.

**Spec:** `docs/design.md`

## Global Constraints

- Windows 10/11 x64 only.
- Target `net9.0-windows`; do not install Electron or extra runtime packages.
- No network requests, API-key access, CDP, DOM injection, or Codex file modification.
- Read only the latest `rollout-*.jsonl`; never persist conversation text.
- Badge outer window is fully transparent and click-through.
- Badge body preserves `padding: 5px 9px`, `border-radius: 7px`, teal alpha border/background, `12px/1.35` font, and `opacity: 0.9`.
- Badge displays one line under the latest assistant action bar; hide when no valid metric or anchor exists.
- The repository root is `codex-plugins/codex-live-usage`.
- Commit after every completed task; never commit build output, local settings, real conversations, or user-specific absolute paths.

---

### Task 1: Core Metrics Parser

**Files:**
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage.Core/CodexLiveUsage.Core.csproj`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage.Core/MetricsSnapshot.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage.Core/RolloutMetricsParser.cs`
- Create: `codex-plugins/codex-live-usage/tests/CodexLiveUsage.Tests/CodexLiveUsage.Tests.csproj`
- Create: `codex-plugins/codex-live-usage/tests/CodexLiveUsage.Tests/Program.cs`

**Interfaces:**
- Produces: `RolloutMetricsParser.Parse(IEnumerable<string> lines) -> MetricsSnapshot?`
- Produces: `MetricsSnapshot` with `ContextUsed`, `ContextLimit`, `ContextPercent`, `CacheHitPercent`, `LastInput`, `LastOutput`, `LastTotal`, `OutputTokensPerSecond`, and `SessionTotal`.

- [x] **Step 1: Write failing parser tests**

```csharp
var snapshot = RolloutMetricsParser.Parse(new[]
{
    """{"timestamp":"2026-10-25T00:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"cached_input_tokens":50,"output_tokens":10,"total_tokens":110},"total_token_usage":{"input_tokens":100,"cached_input_tokens":50,"output_tokens":10,"total_tokens":110},"model_context_window":1000}}}""",
    """{"timestamp":"2026-10-25T00:00:02Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":300,"cached_input_tokens":200,"output_tokens":30,"total_tokens":330},"total_token_usage":{"input_tokens":400,"cached_input_tokens":250,"output_tokens":40,"total_tokens":440},"model_context_window":1000}}}""",
});

Assert.Equal(330d, snapshot!.ContextUsed);
Assert.Equal(1000d, snapshot.ContextLimit);
Assert.Equal(33d, snapshot.ContextPercent);
Assert.Equal(66.7d, snapshot.CacheHitPercent);
Assert.Equal(15d, snapshot.OutputTokensPerSecond);
Assert.Equal(440d, snapshot.SessionTotal);
```

- [x] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet run --project .\codex-plugins\codex-live-usage\tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj
```

Expected: compilation fails because `RolloutMetricsParser` and `MetricsSnapshot` do not exist.

- [x] **Step 3: Implement the core parser**

Parse only `event_msg` / `token_count` objects. Require finite non-negative `last_token_usage.total_tokens`; ignore malformed lines and unknown fields. Use cumulative output deltas between the two latest valid timestamps for speed.

- [x] **Step 4: Add edge-case tests**

```csharp
Assert.Null(RolloutMetricsParser.Parse(new[] { "{not-json" }));
Assert.Null(RolloutMetricsParser.Parse(Array.Empty<string>()));
Assert.Equal(0d, snapshot.ContextPercent); // null context limit
Assert.Equal(100d, snapshot.CacheHitPercent); // clamp cached/input above 100
```

- [x] **Step 5: Run all tests and verify GREEN**

Run the Task 1 command. Expected: all assertions pass and process exits `0`.

### Task 2: Incremental Rollout Reader

**Files:**
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage.Core/RolloutMetricsMonitor.cs`
- Modify: `codex-plugins/codex-live-usage/tests/CodexLiveUsage.Tests/Program.cs`

**Interfaces:**
- Produces: `RolloutMetricsMonitor.Poll() -> MetricsSnapshot?`
- Produces: `new RolloutMetricsMonitor(string codexHome)`.

- [x] **Step 1: Write a failing incremental-read test**

Create a temporary `sessions/2026/10/25/rollout-test.jsonl`, append one valid token event, call `Poll()`, append a second event, call `Poll()`, then assert the latest values and computed speed. Append half a JSON line, assert the previous snapshot remains unchanged, append the remaining line with `\n`, and assert the new snapshot appears.

- [x] **Step 2: Run tests and verify RED**

Expected: compilation fails because `RolloutMetricsMonitor` does not exist.

- [x] **Step 3: Implement incremental reading**

Find the newest root `rollout-*.jsonl` by last-write time. Cache the file path, length, and pending partial line. Read only appended bytes. Reset state when the file shrinks or changes identity. Parse complete lines only.

- [x] **Step 4: Add rotation and malformed-line tests**

Write a newer rollout file and assert `Poll()` switches files. Replace the file with a shorter valid file and assert state resets rather than reusing the old offset.

- [x] **Step 5: Run tests and verify GREEN**

Run the Task 1 command. Expected: all parser, partial-line, and rotation tests pass.

### Task 3: Overlay Geometry

**Files:**
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage.Core/OverlayPlacement.cs`
- Modify: `codex-plugins/codex-live-usage/tests/CodexLiveUsage.Tests/Program.cs`

**Interfaces:**
- Produces: `OverlayPlacement.BelowAnchor(PixelRect host, PixelRect anchor, PixelSize badge, double dpiScale, int gap) -> PixelRect`
- Produces: `OverlayPlacement.FallbackAboveComposer(PixelRect host, PixelRect composer, PixelSize badge, double dpiScale, int gap) -> PixelRect`

- [ ] **Step 1: Write failing placement tests**

```csharp
var placed = OverlayPlacement.BelowAnchor(
    new PixelRect(100, 100, 1200, 900),
    new PixelRect(300, 620, 760, 40),
    new PixelSize(500, 28),
    1.5,
    6);
Assert.Equal(300, placed.Left);
Assert.Equal(666, placed.Top);
Assert.True(placed.Right <= 1300);
Assert.True(placed.Bottom <= 1000);
```

- [ ] **Step 2: Run tests and verify RED**

Expected: missing `OverlayPlacement`, `PixelRect`, and `PixelSize`.

- [ ] **Step 3: Implement placement**

Preserve the anchor's left edge. Add the physical-pixel gap below the action bar. Clamp horizontally and vertically inside the host. For fallback, align left with the composer text area and place above its top edge. Never return a negative visible width.

- [ ] **Step 4: Add DPI and clamping tests**

Assert that a badge wider than the host is clamped to the host width, and that a badge near the bottom moves above the composer rather than leaving the window.

- [ ] **Step 5: Run tests and verify GREEN**

Run the Task 1 command. Expected: placement assertions pass.

### Task 4: WPF Badge And Codex Tracking

**Files:**
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/CodexLiveUsage.csproj`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/Program.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/AppController.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/UsageBadgeWindow.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/Windows/NativeMethods.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/Windows/CodexWindowLocator.cs`
- Create: `codex-plugins/codex-live-usage/src/CodexLiveUsage/Windows/AssistantAnchorFinder.cs`

**Interfaces:**
- Consumes: `RolloutMetricsMonitor.Poll()` and `OverlayPlacement`.
- Produces: single-instance `CodexLiveUsage.exe --background`.
- Produces: transparent click-through WPF badge with a `TextBlock` rendered inside the Codex++-style border.

- [ ] **Step 1: Build the WPF project and verify RED**

Run:

```powershell
dotnet build .\codex-plugins\codex-live-usage\src\CodexLiveUsage\CodexLiveUsage.csproj -c Release
```

Expected: initial project references missing or application classes not yet implemented.

- [ ] **Step 2: Implement WPF visual**

Use `AllowsTransparency=true`, `WindowStyle=None`, `Background=Transparent`, `ShowInTaskbar=false`, and `ShowActivated=false`. Render a `Border` with `CornerRadius=7`, `Padding=(9,5,9,5)`, `Background=#1420B8A6`, `BorderBrush=#4D14B8A6`, and one `TextBlock` at `12px/1.35`.

- [ ] **Step 3: Implement Win32 follow and no-activation**

Find the top-level Codex main window by process path containing `OpenAI\Codex` and title `Codex` or `ChatGPT`. Apply `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`, set the Codex HWND as owner, and position the badge with physical-to-DIP conversion.

- [ ] **Step 4: Implement UI Automation anchoring**

Find the last visible action button named one of `复制`, `喜欢`, `不喜欢`, `从此处开始分叉`, `Copy`, `Good response`, `Bad response`, or `Branch from here`. Walk to its closest group/container, use its bounds as the anchor. If unavailable, locate the composer edit control above the taskbar. If neither exists, hide the badge.

- [ ] **Step 5: Implement polling and text**

Poll window geometry every 250 ms and rollout data every 1000 ms. Render:

```text
上下文 330/1.0K (33%) · 缓存命中率 66.7% · 输入 300 · 输出 30 · 输出速度 15 tok/s · 会话累计 440
```

Hide the badge when no complete metrics exist. Keep the previous text while a partial JSON line is pending.

- [ ] **Step 6: Build and smoke test**

Run the Release build and launch with `--background`. Verify:

```powershell
Get-Process CodexLiveUsage
```

Expected: exactly one process; no console window, taskbar button, or focus steal.

### Task 5: Plugin Package And Lifecycle Scripts

**Files:**
- Create: `codex-plugins/codex-live-usage/.codex-plugin/plugin.json`
- Create: `codex-plugins/codex-live-usage/skills/codex-live-usage/SKILL.md`
- Create: `codex-plugins/codex-live-usage/scripts/build.ps1`
- Create: `codex-plugins/codex-live-usage/scripts/install.ps1`
- Create: `codex-plugins/codex-live-usage/scripts/uninstall.ps1`
- Create: `codex-plugins/codex-live-usage/README.md`
- Create: `codex-plugins/codex-live-usage/THIRD_PARTY_NOTICES.md`

**Interfaces:**
- Produces: plugin id `codex-live-usage`.
- Produces: login task `Codex Live Usage Companion`.
- Produces: `install.ps1`, `uninstall.ps1`, and a repeatable Release build.

- [ ] **Step 1: Add plugin metadata**

Use `.codex-plugin/plugin.json` with name `codex-live-usage`, version `0.1.0`, display name `Codex 常驻用量`, and the bundled skill. Do not declare an MCP server.

- [ ] **Step 2: Add build and install scripts**

`build.ps1` publishes win-x64 to `artifacts/publish`. `install.ps1` locates that executable, verifies `dotnet --list-runtimes` includes Microsoft.WindowsDesktop.App 9.x, verifies the Codex executable, registers the current-user interactive login task, starts it, and writes `%LOCALAPPDATA%\CodexLiveUsage\install-state.json`.

- [ ] **Step 3: Add safe uninstall**

Read and validate the stored PID before stopping it. Unregister only `Codex Live Usage Companion`. Remove only `%LOCALAPPDATA%\CodexLiveUsage` and the plugin’s build output. Never touch `~/.codex/sessions`, Codex config, API balance whale data, or marketplace files.

- [ ] **Step 4: Add license notices**

Document architectural references and commit hashes from the spec. Do not copy unlicensed code. Include the MIT license text for any copied C# helper.

- [ ] **Step 5: Validate scripts**

Run the PowerShell parser on build/install/uninstall:

```powershell
[void][scriptblock]::Create((Get-Content -Raw .\scripts\install.ps1))
[void][scriptblock]::Create((Get-Content -Raw .\scripts\uninstall.ps1))
```

Expected: no parser errors.

### Task 6: End-To-End Verification

**Files:**
- Modify: `codex-plugins/codex-live-usage/README.md`
- Modify: `docs/superpowers/plans/2026-10-25-codex-live-usage-companion.md` checkboxes

**Interfaces:**
- Consumes: all prior tasks.

- [ ] **Step 1: Run the complete automated suite**

```powershell
dotnet run --project .\codex-plugins\codex-live-usage\tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj -c Release
dotnet build .\codex-plugins\codex-live-usage\src\CodexLiveUsage\CodexLiveUsage.csproj -c Release --no-restore
```

Expected: tests exit `0`; build has zero errors and zero warnings.

- [ ] **Step 2: Install and start**

Run `scripts/install.ps1`. Verify task state is `Running`, process count is one, and no separate taskbar entry appears.

- [ ] **Step 3: Verify real Codex integration**

Keep Codex visible with the current conversation. Capture a screenshot and verify:

- badge appears under the latest assistant action row;
- background outside the badge is transparent;
- teal border, tinted background, 7px radius, and one text line match the old style;
- metrics change after a new token event;
- badge follows Codex move/resize;
- badge hides on minimize and recovers on restore;
- text does not overlap reply content or composer.

- [ ] **Step 4: Verify partial data and missing anchor**

Append half a JSON line to a temporary fixture and confirm the prior text remains. Move to a conversation with no metrics and confirm the badge hides instead of showing zero/waiting text.

- [ ] **Step 5: Verify uninstall**

Run `scripts/uninstall.ps1`. Confirm the scheduled task, process, install state, and build output are gone; confirm Codex session files still exist and are unchanged.

- [ ] **Step 6: Record results**

Update the plan checkboxes and README verification section with the exact commands, pass counts, task state, screenshot path, and any remaining limitations.
