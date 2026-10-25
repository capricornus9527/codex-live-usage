param(
    [switch]$NoStart,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$taskName = 'Codex Live Usage Companion'
$pluginRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dataRoot = Join-Path $env:LOCALAPPDATA 'CodexLiveUsage'
$appRoot = Join-Path $dataRoot 'app'
$statePath = Join-Path $dataRoot 'install-state.json'
$publish = Join-Path $pluginRoot 'artifacts\publish'
$sourceExe = Join-Path $publish 'CodexLiveUsage.exe'
$installedExe = Join-Path $appRoot 'CodexLiveUsage.exe'
. (Join-Path $PSScriptRoot 'common.ps1')

if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'Codex Live Usage requires 64-bit Windows.'
}

$runtime = @(& dotnet --list-runtimes)
if ($LASTEXITCODE -ne 0 -or -not (Test-WindowsDesktopRuntime9 $runtime)) {
    throw 'Microsoft Windows Desktop Runtime 9.x is required.'
}

if (-not $NoBuild -and -not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    & (Join-Path $PSScriptRoot 'build.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'Build failed.'
    }
}

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw 'Published executable is missing. Run build.ps1 first.'
}

if (Test-Path -LiteralPath $statePath) {
    try {
        $previous = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($previous.pid) {
            $process = Get-Process -Id ([int]$previous.pid) -ErrorAction SilentlyContinue
            if ($process -and $process.Path -and $process.Path -ieq $previous.appPath) {
                Stop-Process -Id $process.Id -Force
                [void]$process.WaitForExit(5000)
            }
        }
    } catch {
        Write-Warning 'Previous install state could not be used to stop the old process.'
    }
}

if (Test-Path -LiteralPath $appRoot) {
    $resolvedAppRoot = [IO.Path]::GetFullPath($appRoot)
    if (-not $resolvedAppRoot.StartsWith($dataRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clear an app path outside the CodexLiveUsage data directory.'
    }
    Remove-Item -LiteralPath $resolvedAppRoot -Recurse -Force
}

Copy-DirectoryContents -Source $publish -Destination $appRoot
if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
    throw 'The installed executable was not copied successfully.'
}

$userId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $installedExe -Argument '--background' -WorkingDirectory $appRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $userId
$principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -StartWhenAvailable

Register-ScheduledTask `
    -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Principal $principal `
    -Settings $settings `
    -Description 'Shows local Codex token usage below the latest assistant reply.' `
    -Force | Out-Null

if (-not $NoStart) {
    Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
    $process = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $process = Get-Process -Name CodexLiveUsage -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -ieq $installedExe } |
            Select-Object -First 1
        if ($process) {
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $process) {
        throw 'The companion task started, but CodexLiveUsage.exe did not remain running.'
    }
}

@{
    version = '0.1.0'
    taskName = $taskName
    appPath = $installedExe
    pid = if ($process) { $process.Id } else { $null }
    installedAt = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8

$codexCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\app\ChatGPT.exe'),
    (Join-Path $env:ProgramFiles 'OpenAI\Codex\app\ChatGPT.exe')
)
if (Get-Command Get-AppxPackage -ErrorAction SilentlyContinue) {
    $codexPackage = Get-AppxPackage -Name 'OpenAI.Codex' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($codexPackage -and $codexPackage.InstallLocation) {
        $codexCandidates += Join-Path $codexPackage.InstallLocation 'app\ChatGPT.exe'
    }
}
if (-not ($codexCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })) {
    Write-Warning 'Codex Desktop was not found in a known install path. The companion will wait and still try to detect it at runtime.'
}

Write-Output "Installed $taskName"
Write-Output "Executable: $installedExe"
if ($process) {
    Write-Output "PID: $($process.Id)"
}
