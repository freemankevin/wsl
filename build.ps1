#requires -Version 5.1
#requires -RunAsAdministrator
<#
.SYNOPSIS
    Build script for WSL Manager WPF application and .msi installer.
.DESCRIPTION
    Compiles the solution, publishes the WPF app, and builds the WiX MSI installer.
.NOTES
    Requires: .NET 8 SDK, WiX Toolset v4 (dotnet tool)
    Install WiX: dotnet tool install --global wix
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

# Verify WiX
Write-Host "`n[2/5] Checking WiX Toolset..." -ForegroundColor Yellow
$wix = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wix) {
    Write-Host "  WiX not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global wix
}
Write-Host "  WiX ready" -ForegroundColor Green

# Restore
Write-Host "`n[3/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $Solution
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed" }
Write-Host "  Restore complete" -ForegroundColor Green

# Build
Write-Host "`n[4/5] Building solution (Release)..." -ForegroundColor Yellow
dotnet build $Solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed" }
Write-Host "  Build complete" -ForegroundColor Green

# Publish WPF app (single file, ready for packaging)
Write-Host "`n[5/5] Building .msi installer..." -ForegroundColor Yellow
$publishDir = Join-Path $Root "artifacts\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

# Publish the WPF app to a folder for WiX to pick up
dotnet publish (Join-Path $Src "WSLManager\WSLManager.csproj") `
    -c Release `
    --no-build `
    -o $publishDir `
    -p:PublishSingleFile=true `
    --self-contained false

if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed" }

# Build MSI with WiX
$setupProj = Join-Path $Src "WSLManager.Setup\Product.wxs"
$msiOutput = Join-Path $Root "artifacts\WSLManager-1.0.0.msi"

wix build $setupProj `
    -p:Configuration=Release `
    -d WSLManager.TargetDir="$publishDir\" `
    -o $msiOutput

if ($LASTEXITCODE -ne 0) { Write-Error "MSI build failed" }

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  BUILD SUCCESSFUL" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MSI: $msiOutput" -ForegroundColor White
Write-Host "  Exe: $publishDir\WSLManager.exe" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
