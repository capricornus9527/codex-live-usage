# Changelog

All notable changes to this project will be documented in this file.

## 0.1.1 - 2026-10-28

### Fixed

- Reduced long-running WPF resource growth by caching fonts, brushes, inline
  runs, and measured line sizes instead of rebuilding the visual tree on every
  refresh.
- Added unhandled-exception logging to
  `%LOCALAPPDATA%\CodexLiveUsage\error.log` without recording conversation
  content.
- Added an every-minute scheduled-task recovery trigger. The existing
  single-instance mutex prevents duplicate companion processes.
- Fixed upgrades after an automatic restart by stopping only companion
  processes whose executable path is inside the installed app directory, even
  when `install-state.json` still contains the previous PID.
- Documented the observed `PresentationCore` /
  `System.OutOfMemoryException` crash signature and recovery procedure.

## 0.1.0 - 2026-10-25

### Added

- Transparent single-line usage display fixed below the Codex composer.
- Context used/limit, cache hit rate, input/output tokens, and output speed.
- Current-conversation tracking through local Codex IPC, including idle
  conversation switches and an immediate `0 tok/s` state.
- Adaptive 12/11/10/9 px text sizes based on available composer space.
- Chinese labels with Microsoft YaHei and numeric values with Times New Roman.
- Automatic login startup and Codex window following.
- Click-through, no-activation overlay behavior.
- Personal Codex plugin manifest and skill.
- Automated parser, geometry, font segmentation, and installer tests.
- README in English and Simplified Chinese, MIT license, security guide, and
  third-party notices.
