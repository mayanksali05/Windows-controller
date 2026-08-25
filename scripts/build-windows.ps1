#requires -Version 5.1
<#
.SYNOPSIS
    Restores and builds the Windows WinLock projects.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'windows\WinLock.sln'

if (-not (Test-Path $solution)) {
    Write-Warning "Solution not found at '$solution'. Projects have not been scaffolded yet."
    exit 0
}

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Write-Host 'Build complete.'