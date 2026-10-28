$ErrorActionPreference = 'Stop'
$pluginRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $pluginRoot 'scripts\common.ps1')

$pluginVersion = (Get-Content -LiteralPath (Join-Path $pluginRoot '.codex-plugin\plugin.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json).version
$marketplaceVersion = (Get-Content -LiteralPath (Join-Path $pluginRoot 'plugin.json') -Raw -Encoding UTF8 |
    ConvertFrom-Json).version
if ($pluginVersion -ne $marketplaceVersion) {
    throw "Plugin manifest versions do not match: $pluginVersion vs $marketplaceVersion"
}

$validRuntime = @(
    'Microsoft.NETCore.App 9.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]',
    'Microsoft.WindowsDesktop.App 9.0.0 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]'
)
$missingRuntime = @(
    'Microsoft.NETCore.App 9.0.0 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]',
    'Microsoft.WindowsDesktop.App 8.0.10 [C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App]'
)

if (-not (Test-WindowsDesktopRuntime9 $validRuntime)) {
    throw 'Expected the Windows Desktop 9 runtime to be accepted.'
}

if (Test-WindowsDesktopRuntime9 $missingRuntime) {
    throw 'Expected a runtime list without Windows Desktop 9 to be rejected.'
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('codex-live-usage-install-test-' + [Guid]::NewGuid().ToString('N'))
try {
    $source = Join-Path $temporaryRoot 'source'
    $destination = Join-Path $temporaryRoot 'destination'
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $source 'app.exe') -Value 'fixture'
    Set-Content -LiteralPath (Join-Path $source 'runtime.dll') -Value 'runtime'

    Copy-DirectoryContents -Source $source -Destination $destination

    if (-not (Test-Path -LiteralPath (Join-Path $destination 'app.exe') -PathType Leaf)) {
        throw 'Expected the directory contents to be copied.'
    }
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Output 'PASS install runtime detection'
