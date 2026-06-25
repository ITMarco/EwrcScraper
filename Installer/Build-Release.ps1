# Build-Release.ps1
# Produces the two release artifacts for the current version:
#   1. EwrcScraper-v<ver>-win-x64.zip   — small framework-dependent build (needs .NET 10 runtime)
#   2. EwrcScraper-v<ver>-Setup.exe     — installer (self-contained, no runtime needed)
#
# The big standalone zip is intentionally NOT produced anymore.
# Requires: Inno Setup 6 (https://jrsoftware.org/isinfo.php)

$ErrorActionPreference = "Stop"
$root    = Split-Path $PSScriptRoot -Parent
$csproj  = Join-Path $root "CSharpScraper"
$proj    = Join-Path $csproj "EwrcScraper.csproj"
$pubDir  = Join-Path $csproj "publish"
$outDir  = Join-Path $PSScriptRoot "Output"

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 niet gevonden. Download via https://jrsoftware.org/isinfo.php" }

# Read version from version.json
$version = (Get-Content (Join-Path $csproj "version.json") | ConvertFrom-Json).version
Write-Host "Release bouwen voor versie $version" -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 1. Framework-dependent single-file -> zip
Write-Host "[1/3] Framework-afhankelijke build..." -ForegroundColor Cyan
Remove-Item "$pubDir\fd" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $proj -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o "$pubDir\fd" | Out-Null
$fdZip = Join-Path $outDir "EwrcScraper-v$version-win-x64.zip"
Remove-Item $fdZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$pubDir\fd\*" -DestinationPath $fdZip -CompressionLevel Optimal

# 2. Self-contained standalone (consumed by the installer, not shipped as a zip)
Write-Host "[2/3] Standalone build (voor installer)..." -ForegroundColor Cyan
Remove-Item "$pubDir\standalone" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:DebugType=none -o "$pubDir\standalone" | Out-Null

# 3. Installer
Write-Host "[3/3] Installer bouwen..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$version" "$PSScriptRoot\EwrcScraper.iss" | Out-Null

Write-Host ""
Write-Host "Klaar. Upload deze twee bestanden naar de GitHub release:" -ForegroundColor Green
Get-ChildItem $outDir -Filter "*v$version*" |
    Select-Object Name, @{N="MB";E={[math]::Round($_.Length/1MB,1)}} | Format-Table -AutoSize
