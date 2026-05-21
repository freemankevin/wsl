#requires -Version 5.1
#requires -RunAsAdministrator
<#
.SYNOPSIS
    Build script for WSL Manager WPF application and installer.
.DESCRIPTION
    Compiles the solution, publishes the WPF app, and builds the installer.
    Supports both Inno Setup (.exe) and WiX (.msi) installers.
    Note: WiX MSI build requires a compatible environment (Visual Studio + WiX v4).
.NOTES
    Requires: .NET 8 SDK, Inno Setup 6
    Optional: WiX Toolset v4 (for MSI build)
    Install Inno Setup: https://jrsoftware.org/isdl.php
#>

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Src = Join-Path $Root "src"
$Solution = Join-Path $Root "WSLManager.sln"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  WSL Manager Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Verify .NET SDK
Write-Host "`n[1/5] Checking .NET SDK..." -ForegroundColor Yellow
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error ".NET SDK not found. Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
}
$sdkVersion = dotnet --version
Write-Host "  Found .NET SDK: $sdkVersion" -ForegroundColor Green

# Verify Inno Setup
Write-Host "`n[2/5] Checking Inno Setup..." -ForegroundColor Yellow
$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    $isccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $isccPath) {
        $iscc = Get-Command $isccPath -ErrorAction SilentlyContinue
    }
}
if (-not $iscc) {
    Write-Warning "Inno Setup not found. .exe installer will not be built."
    Write-Warning "Download from: https://jrsoftware.org/isdl.php"
} else {
    Write-Host "  Inno Setup ready: $($iscc.Source)" -ForegroundColor Green
}

# Verify WiX (optional)
Write-Host "`n[3/5] Checking WiX Toolset (optional)..." -ForegroundColor Yellow
$wix = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wix) {
    $wixPath = "$env:USERPROFILE\.dotnet\tools\wix.exe"
    if (Test-Path $wixPath) {
        $wix = Get-Command $wixPath -ErrorAction SilentlyContinue
    }
}
if (-not $wix) {
    Write-Warning "WiX not found. .msi installer will not be built."
} else {
    Write-Host "  WiX ready: $($wix.Source)" -ForegroundColor Green
}

# Restore
Write-Host "`n[4/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $Solution
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed" }
Write-Host "  Restore complete" -ForegroundColor Green

# Build
Write-Host "`n[5/5] Building solution (Release)..." -ForegroundColor Yellow
dotnet build $Solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed" }
Write-Host "  Build complete" -ForegroundColor Green

# Publish WPF app
Write-Host "`nPublishing WPF app..." -ForegroundColor Yellow
$publishDir = Join-Path $Root "artifacts\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $Src "WSLManager\WSLManager.csproj") `
    -c Release `
    --no-build `
    -o $publishDir `
    -p:PublishSingleFile=true `
    --self-contained false

if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed" }
Write-Host "  Publish complete" -ForegroundColor Green

# Build Inno Setup .exe installer
$exeOutput = Join-Path $Root "artifacts\WSLManager-1.0.0-Setup.exe"
if ($iscc) {
    Write-Host "`nBuilding .exe installer (Inno Setup)..." -ForegroundColor Yellow
    & $iscc.Source (Join-Path $Src "WSLManager.Setup\WSLManager.iss")
    if ($LASTEXITCODE -ne 0) { Write-Error ".exe installer build failed" }
    Write-Host "  .exe installer ready" -ForegroundColor Green
} else {
    Write-Warning "Skipping .exe installer (Inno Setup not found)"
}

# Build WiX .msi installer (optional)
$msiOutput = Join-Path $Root "artifacts\WSLManager-1.0.0.msi"
if ($wix) {
    Write-Host "`nBuilding .msi installer (WiX)..." -ForegroundColor Yellow
    $wixExt = "$env:USERPROFILE\.nuget\packages\wixtoolset.ui.wixext\4.0.2\wixext4\WixToolset.UI.wixext.dll"
    if (Test-Path $wixExt) {
        & $wix.Source build (Join-Path $Src "WSLManager.Setup\Product.wxs") `
            -ext $wixExt `
            -d "WSLManager.TargetDir=$publishDir\" `
            -o $msiOutput
        if ($LASTEXITCODE -ne 0) {
            Write-Warning ".msi build failed (this is often due to environment compatibility issues with WiX v4 + .NET 8)"
        } else {
            Write-Host "  .msi installer ready" -ForegroundColor Green
        }
    } else {
        Write-Warning "WiX UI extension not found. Skipping .msi build."
    }
} else {
    Write-Warning "Skipping .msi installer (WiX not found)"
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  BUILD SUCCESSFUL" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
if (Test-Path $exeOutput) {
    Write-Host "  Setup (.exe): $exeOutput" -ForegroundColor White
}
if (Test-Path $msiOutput) {
    Write-Host "  MSI (.msi):   $msiOutput" -ForegroundColor White
}
Write-Host "  Exe: $publishDir\WSLManager.exe" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
