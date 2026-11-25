# Builds publish\PCOpti.exe and then installer\Output\PCOpti-Setup-v<version>.exe
# Usage (from the repo root):  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Version 1.0.0
param(
    [string]$Version = "1.0.0"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with:  winget install JRSoftware.InnoSetup" }

Write-Host "Publishing PCOpti $Version..."
dotnet publish src/PCOpti/PCOpti.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Building installer..."
& $iscc "/DAppVersion=$Version" "installer\PCOpti.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Get-Item "installer\Output\PCOpti-Setup-v$Version.exe"
