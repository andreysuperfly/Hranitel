# Build script for Hranitel
param(
    [switch]$Publish,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"

Write-Host "Building Hranitel..." -ForegroundColor Cyan
dotnet build -c Release

if (-not $?) { exit 1 }

if ($Publish) {
    Write-Host "Publishing..." -ForegroundColor Cyan
    dotnet publish -c Release -r win-x64 --self-contained false -o "bin\Release\net8.0-windows\publish"
}

if ($Installer) {
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) {
        $iscc = "C:\Program Files\Inno Setup 6\ISCC.exe"
    }
    if (Test-Path $iscc) {
        Write-Host "Building installer..." -ForegroundColor Cyan
        if (-not $Publish) {
            dotnet publish -c Release -r win-x64 --self-contained false -o "bin\Release\net8.0-windows\publish"
        }
        & $iscc installer.iss
    } else {
        Write-Host "Inno Setup not found. Install from https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    }
}

Write-Host "Done." -ForegroundColor Green
