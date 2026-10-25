# Contributing

## Development

Requirements:

- Windows 10 or Windows 11 x64
- .NET 9 SDK
- Codex Desktop for end-to-end verification

Run the automated checks:

```powershell
dotnet run --project .\tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\InstallScript.Tests.ps1
dotnet build .\src\CodexLiveUsage\CodexLiveUsage.csproj -c Release
```

## Privacy

Codex rollout JSONL files can contain conversation content. Never commit real
rollout files, screenshots containing private chats, credentials, personal
paths, or diagnostic logs. Tests must use synthetic fixtures only.

## Pull requests

- Keep the widget read-only and local-first.
- Do not add CDP, DOM injection, telemetry, or network requests without a
  specific design discussion.
- Add tests for parser, placement, lifecycle, and failure behavior changes.
- Include only code and assets that can be redistributed under compatible
  licenses.
