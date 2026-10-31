param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$pluginRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = [IO.Path]::GetFullPath((Join-Path $pluginRoot 'artifacts'))
$publish = Join-Path $artifacts 'publish'

if (-not $artifacts.StartsWith($pluginRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifact directory escaped the plugin root.'
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (-not $SkipTests) {
    & dotnet run `
        --project (Join-Path $pluginRoot 'tests\CodexLiveUsage.Tests\CodexLiveUsage.Tests.csproj') `
        -c Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Automated tests failed.'
    }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (
        Join-Path $pluginRoot 'tests\InstallScript.Tests.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'PowerShell install-script tests failed.'
    }
}

if (Test-Path -LiteralPath $publish) {
    $resolvedPublish = [IO.Path]::GetFullPath($publish)
    if (-not $resolvedPublish.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove a publish path outside artifacts.'
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}

& dotnet publish `
    (Join-Path $pluginRoot 'src\CodexLiveUsage\CodexLiveUsage.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

$executable = Join-Path $publish 'CodexLiveUsage.exe'
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'Published executable is missing.'
}

Write-Output "Built $executable"
if (-not $SkipTests) {
    $layoutTest = Start-Process -FilePath $executable -ArgumentList '--layout-tests' -WindowStyle Hidden -Wait -PassThru
    if ($layoutTest.ExitCode -ne 0) { throw 'WPF layout regression tests failed.' }
    Write-Output 'PASS WPF natural-width, metric priority, cache, and resize regression tests'
}
