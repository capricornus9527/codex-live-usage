function Test-WindowsDesktopRuntime9 {
    param(
        [AllowNull()]
        [string[]]$RuntimeLines
    )

    if (-not $RuntimeLines) {
        return $false
    }

    return ($RuntimeLines -join "`n") -match '(?m)^Microsoft\.WindowsDesktop\.App 9\.'
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory)]
        [string]$Source,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Source directory does not exist: $Source"
    }

    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    Get-ChildItem -LiteralPath $Source -Force |
        Copy-Item -Destination $Destination -Recurse -Force
}
