#requires -Version 5.1
<#
.SYNOPSIS
    Builds the WinLock credential provider DLL (needed for iPhone unlock).
.DESCRIPTION
    Requires Visual Studio 2022 (Community is fine) or Build Tools 2022 with the
    "Desktop development with C++" workload. Output:
    windows\WinLock.CredentialProvider\bin\Release\WinLock.CredentialProvider.dll
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'windows\WinLock.CredentialProvider\WinLock.CredentialProvider.vcxproj'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'Visual Studio 2022 was not found. Install it with the "Desktop development with C++" workload.'
}

$msbuild = & $vswhere -latest -products * `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild with the C++ tools was not found. Add the "Desktop development with C++" workload in the Visual Studio Installer.'
}

& $msbuild $project /p:Configuration=$Configuration /p:Platform=x64 /m /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Credential provider build failed.' }

$dll = Join-Path $root "windows\WinLock.CredentialProvider\bin\$Configuration\WinLock.CredentialProvider.dll"
Write-Host "Built: $dll"
Write-Host 'Next (elevated PowerShell): .\scripts\install-credential-provider.ps1'
