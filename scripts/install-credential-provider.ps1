#requires -Version 5.1
#requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs (or removes) the WinLock credential provider so the iPhone can
    unlock this laptop.
.DESCRIPTION
    Copies WinLock.CredentialProvider.dll to System32 and registers it with
    LogonUI. Windows' own password / PIN / Hello sign-in stay available, so you
    can always sign in normally.

    Disable without uninstalling (e.g. from another admin account):
      .\scripts\install-credential-provider.ps1 -Disable
    Remove completely:
      .\scripts\install-credential-provider.ps1 -Uninstall
.PARAMETER DllPath
    Path to a built DLL. Defaults to the Release build output.
#>
[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [Parameter(ParameterSetName = 'Install')]
    [string]$DllPath,

    [Parameter(ParameterSetName = 'Uninstall', Mandatory)]
    [switch]$Uninstall,

    [Parameter(ParameterSetName = 'Disable', Mandatory)]
    [switch]$Disable
)

$ErrorActionPreference = 'Stop'

# Must match CLSID_WinLockProvider in windows\WinLock.CredentialProvider\common.h
$clsid = '{B8C6F5E2-4A5D-4C1B-9E0F-3D2A7C8B9E41}'
$name = 'WinLock iPhone Unlock'
$dllName = 'WinLock.CredentialProvider.dll'
$target = Join-Path $env:WINDIR "System32\$dllName"
$providerKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\$clsid"
$clsidKey = "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid"

if ($Disable) {
    if (Test-Path $providerKey) {
        New-ItemProperty -Path $providerKey -Name 'Disabled' -Value 1 -PropertyType DWord -Force | Out-Null
        Write-Host 'WinLock credential provider disabled (re-run without -Disable to enable).'
    } else {
        Write-Host 'WinLock credential provider is not installed.'
    }
    return
}

if ($Uninstall) {
    if (Test-Path $providerKey) { Remove-Item $providerKey -Recurse -Force }
    if (Test-Path $clsidKey) { Remove-Item $clsidKey -Recurse -Force }
    if (Test-Path $target) {
        try {
            Remove-Item $target -Force
        } catch {
            # LogonUI may still have it loaded; it is unregistered, so it will
            # not load again. Rename it out of the way; delete after a reboot.
            $stale = "$target.old"
            if (Test-Path $stale) { Remove-Item $stale -Force -ErrorAction SilentlyContinue }
            Rename-Item $target $stale -Force
            Write-Warning "DLL in use; renamed to $stale. Delete it after the next restart."
        }
    }
    Write-Host 'WinLock credential provider removed.'
    return
}

if (-not $DllPath) {
    $root = Split-Path -Parent $PSScriptRoot
    $DllPath = Join-Path $root "windows\WinLock.CredentialProvider\bin\Release\$dllName"
}
if (-not (Test-Path $DllPath)) {
    throw "DLL not found at '$DllPath'. Run .\scripts\build-credential-provider.ps1 first."
}

if (Test-Path $target) {
    try {
        Copy-Item $DllPath $target -Force
    } catch {
        # Replace a loaded copy: rename the old one, then copy the new one in.
        $stale = "$target.old"
        if (Test-Path $stale) { Remove-Item $stale -Force -ErrorAction SilentlyContinue }
        Rename-Item $target $stale -Force
        Copy-Item $DllPath $target -Force
        Write-Warning "Previous DLL was in use; it was renamed to $stale (delete it after a restart)."
    }
} else {
    Copy-Item $DllPath $target -Force
}

New-Item -Path $clsidKey -Force | Out-Null
Set-ItemProperty -Path $clsidKey -Name '(default)' -Value $name
New-Item -Path "$clsidKey\InprocServer32" -Force | Out-Null
Set-ItemProperty -Path "$clsidKey\InprocServer32" -Name '(default)' -Value $dllName
Set-ItemProperty -Path "$clsidKey\InprocServer32" -Name 'ThreadingModel' -Value 'Apartment'

New-Item -Path $providerKey -Force | Out-Null
Set-ItemProperty -Path $providerKey -Name '(default)' -Value $name
Remove-ItemProperty -Path $providerKey -Name 'Disabled' -ErrorAction SilentlyContinue

Write-Host "Installed $name ($clsid)."
Write-Host 'Test it: start WinLock.Service, lock with Win+L, then tap Unlock in the iPhone app.'
Write-Host 'Your normal password / PIN sign-in is unchanged.'
