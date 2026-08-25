#requires -Version 5.1
<#
.SYNOPSIS
    Runs all .NET test projects in the repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$testProjects = Get-ChildItem -Path $root -Recurse -Filter '*.Tests.csproj' -File
if (-not $testProjects) {
    Write-Warning 'No test projects found.'
    exit 0
}

foreach ($project in $testProjects) {
    Write-Host "Testing $($project.FullName)..."
    dotnet test $project.FullName -c Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed for $($project.Name)." }
}

Write-Host 'All tests passed.'