#requires -Version 5.1
<#
.SYNOPSIS
    Runs the WinLock service in console (development) mode.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$serviceDir = Join-Path $root 'windows\WinLock.Service'

if (-not (Test-Path (Join-Path $serviceDir 'WinLock.Service.csproj'))) {
    Write-Warning "WinLock.Service project not found at '$serviceDir'."
    exit 1
}

dotnet run --project $serviceDir -- --development