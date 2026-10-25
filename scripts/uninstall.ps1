$ErrorActionPreference = 'Stop'
$taskName = 'Codex Live Usage Companion'
$dataRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'CodexLiveUsage'))
$appRoot = [IO.Path]::GetFullPath((Join-Path $dataRoot 'app'))
$statePath = Join-Path $dataRoot 'install-state.json'

if (Test-Path -LiteralPath $statePath) {
    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.pid) {
            $process = Get-Process -Id ([int]$state.pid) -ErrorAction SilentlyContinue
            if ($process -and $process.Path -and $process.Path -ieq $state.appPath) {
                Stop-Process -Id $process.Id -Force
                [void]$process.WaitForExit(5000)
            }
        }
    } catch {
        Write-Warning 'Install state could not be read; only matching installed processes will be stopped.'
    }
}

Get-Process -Name CodexLiveUsage -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        if ($_.Path -and $_.Path.StartsWith($appRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $_.Id -Force
            [void]$_.WaitForExit(5000)
        }
    } catch {
        Write-Warning "Could not inspect process $($_.Id)."
    }
}

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

if (Test-Path -LiteralPath $dataRoot) {
    $expectedParent = [IO.Path]::GetFullPath($env:LOCALAPPDATA)
    if (-not $dataRoot.StartsWith($expectedParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove a data path outside LOCALAPPDATA.'
    }
    Remove-Item -LiteralPath $dataRoot -Recurse -Force
}

$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts'))
if (Test-Path -LiteralPath $artifactRoot) {
    $pluginRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if ($artifactRoot.StartsWith($pluginRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $artifactRoot -Recurse -Force
    }
}

Write-Output 'Codex Live Usage companion removed.'
