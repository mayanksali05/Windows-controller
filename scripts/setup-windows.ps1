#requires -Version 5.1
<#
.SYNOPSIS
    Prepares the Windows host for the WinLock companion service: config,
    development certificate, and LAN-scoped firewall rule.
#>
[CmdletBinding()]
param(
    [int]$Port = 8765
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$serviceDir = Join-Path $root 'windows\WinLock.Service'

if (-not (Test-Path (Join-Path $serviceDir 'WinLock.Service.csproj'))) {
    Write-Warning "WinLock.Service project not found at '$serviceDir'. Nothing to set up yet."
    exit 0
}

Write-Host "Preparing WinLock development environment (port $Port)..."

# 1. Development certificate for HTTPS
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
    $_.Subject -eq 'CN=WinLock-Development' -and $_.NotAfter -gt (Get-Date)
} | Select-Object -First 1

if (-not $cert) {
    Write-Host 'Creating development certificate (CN=WinLock-Development)...'
    $cert = New-SelfSignedCertificate -DnsName 'WinLock-Development' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyExportPolicy 'Exportable' `
        -KeyAlgorithm 'RSA' `
        -KeyLength 2048 `
        -HashAlgorithm 'SHA256' `
        -NotAfter (Get-Date).AddYears(2)
}
Write-Host "Development certificate thumbprint: $($cert.Thumbprint)"

# 2. Firewall rule (LAN-scoped)
$ruleName = 'WinLock Service (LAN)'
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Creating firewall rule '$ruleName' for port $Port..."
    try {
        New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
            -Protocol TCP -LocalPort $Port `
            -RemoteAddress 'LocalSubnet' -Profile Private,Domain -ErrorAction Stop | Out-Null
    } catch {
        Write-Warning "Could not create the firewall rule (requires an elevated PowerShell)."
        Write-Warning "Run setup-windows.ps1 from an elevated shell to allow LAN access, or create the rule manually for TCP $Port (LocalSubnet)."
    }
} else {
    Write-Host "Firewall rule '$ruleName' already exists."
}

Write-Host 'Setup complete.'
Write-Host 'Next: .\scripts\build-windows.ps1'