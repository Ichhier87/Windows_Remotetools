#Requires -Version 5.1
<#
.SYNOPSIS
    Baut Windows Remote Tools und erstellt anschließend das MSI-Installationspaket.

.DESCRIPTION
    1. Beide C#-Projekte werden in Release gebaut
    2. Das WiX-Projekt wird gebaut
    3. Das fertige MSI landet in .\Installer\bin\Release\

.PARAMETER SkipBuild
    Überspringt den .NET-Build (nützlich wenn bereits gebaut wurde)

.PARAMETER Version
    Versions-Nummer für das MSI (Standard: 1.0.0)

.EXAMPLE
    .\Build-Installer.ps1
    .\Build-Installer.ps1 -Version "1.2.3"
    .\Build-Installer.ps1 -SkipBuild
#>
param(
    [switch]$SkipBuild,
    [string]$Version = "1.0.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$SolutionFile = Join-Path $Root "WindowsRemoteTools.sln"
$InstallerProject = Join-Path $Root "Installer\Installer.wixproj"
$OutputDir = Join-Path $Root "Installer\bin\Release"

# ─────────────────────────────────────────────────
# Hilfsfunktionen
# ─────────────────────────────────────────────────
function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "  ► $msg" -ForegroundColor Cyan
}

function Assert-Command([string]$cmd) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Host "  ✗ '$cmd' nicht gefunden. Bitte installieren:" -ForegroundColor Red
        switch ($cmd) {
            "dotnet" { Write-Host "    https://dotnet.microsoft.com/download/dotnet/8.0" }
        }
        exit 1
    }
}

# ─────────────────────────────────────────────────
# Voraussetzungen prüfen
# ─────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  Windows Remote Tools – Installer Build   " -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor DarkCyan

Assert-Command "dotnet"

# WiX-SDK wird automatisch durch das .wixproj NuGet-Paket geladen,
# kein separater WiX-Install nötig.

# ─────────────────────────────────────────────────
# Schritt 1: .NET Release-Build
# ─────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Step ".NET-Projekte in Release bauen..."

    & dotnet build $SolutionFile -c Release --nologo /p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ .NET Build fehlgeschlagen (Exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "  ✓ .NET Build erfolgreich" -ForegroundColor Green
} else {
    Write-Host "  ► .NET-Build übersprungen (-SkipBuild)" -ForegroundColor Yellow
}

# ─────────────────────────────────────────────────
# Prüfen ob Build-Output vorhanden
# ─────────────────────────────────────────────────
$ServiceExe = Join-Path $Root "WindowsRemoteTools\bin\Release\net8.0-windows\WindowsRemoteTools.exe"
$UIExe = Join-Path $Root "WindowsRemoteToolsUI\bin\Release\net8.0-windows\WindowsRemoteToolsUI.exe"

foreach ($binary in @($ServiceExe, $UIExe)) {
    if (-not (Test-Path $binary)) {
        Write-Host "  ✗ Fehlende Datei: $binary" -ForegroundColor Red
        Write-Host "    Bitte zuerst 'dotnet build WindowsRemoteTools.sln -c Release' ausführen." -ForegroundColor Red
        exit 1
    }
}

# ─────────────────────────────────────────────────
# Schritt 2: WiX-Installer bauen
# ─────────────────────────────────────────────────
Write-Step "WiX-Installer bauen (Version $Version)..."

& dotnet build $InstallerProject -c Release --nologo `
    /p:Version=$Version `
    /p:OutputPath=$(Join-Path $Root "Installer\bin\Release\")

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ WiX Build fehlgeschlagen (Exit $LASTEXITCODE)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Häufige Ursachen:" -ForegroundColor Yellow
    Write-Host "    • Fehlende Datei im Build-Output (neue NuGet-Abhängigkeit?)" -ForegroundColor Yellow
    Write-Host "    • WiX-Version veraltet → Installer\Installer.wixproj anpassen" -ForegroundColor Yellow
    exit $LASTEXITCODE
}

# ─────────────────────────────────────────────────
# Ergebnis
# ─────────────────────────────────────────────────
$Msi = Get-ChildItem -Path $OutputDir -Filter "*.msi" -Recurse |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor DarkGreen
Write-Host "  ✓ FERTIG" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════" -ForegroundColor DarkGreen

if ($Msi) {
    Write-Host ""
    Write-Host "  MSI-Datei:" -ForegroundColor White
    Write-Host "  $($Msi.FullName)" -ForegroundColor Cyan
    Write-Host "  Größe: $([math]::Round($Msi.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host ""

    $open = Read-Host "  MSI-Datei im Explorer anzeigen? [j/N]"
    if ($open -match "^[jJyY]") {
        Start-Process explorer.exe -ArgumentList "/select,`"$($Msi.FullName)`""
    }
} else {
    Write-Host "  Kein MSI im Ausgabeordner gefunden: $OutputDir" -ForegroundColor Yellow
}
