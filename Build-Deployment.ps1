# Windows Remote Tools - Deployment Package Builder
# Erstellt ein Installations-Paket fuer andere Maschinen

param(
    [string]$OutputPath = "C:\Temp\WindowsRemoteTools-Deployment"
)

Write-Host "=== Windows Remote Tools - Deployment Builder ===" -ForegroundColor Cyan
Write-Host ""

# 1. Kompiliere beide Projekte
Write-Host "[1/6] Kompiliere Projekte..." -ForegroundColor Yellow

Push-Location "$PSScriptRoot\WindowsRemoteTools"
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "X Service-Kompilierung fehlgeschlagen!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location

Push-Location "$PSScriptRoot\WindowsRemoteToolsUI"
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "X UI-Kompilierung fehlgeschlagen!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location

Write-Host "OK Kompilierung erfolgreich" -ForegroundColor Green

# 2. Erstelle Deployment-Verzeichnis
Write-Host "`n[2/6] Erstelle Deployment-Verzeichnis..." -ForegroundColor Yellow

if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
New-Item -Path "$OutputPath\Files" -ItemType Directory -Force | Out-Null

Write-Host "OK Verzeichnis erstellt: $OutputPath" -ForegroundColor Green

# 3. Kopiere Binaries
Write-Host "`n[3/6] Kopiere Dateien..." -ForegroundColor Yellow

# Service-Dateien
Copy-Item "$PSScriptRoot\WindowsRemoteTools\bin\Release\net8.0-windows\*" -Destination "$OutputPath\Files\" -Recurse -Force

# UI-Dateien (nur die spezifischen UI-Dateien, Rest ist schon dabei)
Copy-Item "$PSScriptRoot\WindowsRemoteToolsUI\bin\Release\net8.0-windows\WindowsRemoteToolsUI.*" -Destination "$OutputPath\Files\" -Force

# WebView2 managed DLLs (für Vokabeltrainer-Overlay)
Copy-Item "$PSScriptRoot\WindowsRemoteToolsUI\bin\Release\net8.0-windows\Microsoft.Web.WebView2.*.dll" -Destination "$OutputPath\Files\" -Force -ErrorAction SilentlyContinue

# WebView2 nativer Loader: muss arch-spezifisch unter runtimes\win-x64\native\ liegen
$nativeDir = "$OutputPath\Files\runtimes\win-x64\native"
New-Item -Path $nativeDir -ItemType Directory -Force | Out-Null
Copy-Item "$PSScriptRoot\WindowsRemoteToolsUI\bin\Release\net8.0-windows\runtimes\win-x64\native\WebView2Loader.dll" -Destination $nativeDir -Force -ErrorAction SilentlyContinue

# Entferne unnoetige Dateien
Remove-Item "$OutputPath\Files\*.pdb" -Force -ErrorAction SilentlyContinue
Remove-Item "$OutputPath\Files\ref" -Recurse -Force -ErrorAction SilentlyContinue

$fileCount = (Get-ChildItem "$OutputPath\Files" -File).Count
Write-Host "OK $fileCount Dateien kopiert" -ForegroundColor Green

# 4. Erstelle Standard-Config
Write-Host "`n[4/6] Erstelle Standard-Config..." -ForegroundColor Yellow

$defaultConfig = @{
    WebSocketUrl = "ws://CENTRAL-SERVER-IP:8080"
    DeviceName = "COMPUTERNAME"
    Username = "username"
    Password = "password"
    MaxVolume = 75
    EnforceMaxVolume = $true
    AutoReconnect = $true
    ReconnectDelay = 5000
} | ConvertTo-Json

$defaultConfig | Out-File "$OutputPath\Files\config.json" -Encoding UTF8
Write-Host "OK config.json erstellt" -ForegroundColor Green

# 5. Erstelle Installations-Skript
Write-Host "`n[5/6] Erstelle Installations-Skript..." -ForegroundColor Yellow

# Create Install.ps1 directly
$installContent = @'
# Windows Remote Tools - Installer
# MUSS ALS ADMINISTRATOR AUSGEFUEHRT WERDEN!

[CmdletBinding()]
param(
    [string]$InstallPath = "C:\Program Files\WindowsRemoteTools",
    [string]$ServiceName = "WindowsRemoteToolsService"
)

$ErrorActionPreference = "Stop"

# Pruefe Admin-Rechte
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "FEHLER: Dieses Skript muss als Administrator ausgefuehrt werden!" -ForegroundColor Red
    Write-Host "Rechtsklick auf PowerShell -> Als Administrator ausfuehren" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Windows Remote Tools - Installation   " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Stoppe existierenden Service
Write-Host "[1/8] Pruefe existierende Installation..." -ForegroundColor Yellow
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "      Existierender Service gefunden - wird gestoppt..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Write-Host "      OK Alter Service entfernt" -ForegroundColor Green
}

# Stoppe alle Prozesse
Get-Process | Where-Object { $_.Name -like "*WindowsRemote*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "OK Bereit fuer Installation" -ForegroundColor Green

# 2. Erstelle Installations-Verzeichnis
Write-Host "`n[2/8] Erstelle Installations-Verzeichnis..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Write-Host "      Verzeichnis existiert bereits - wird geleert..." -ForegroundColor Yellow
    Remove-Item "$InstallPath\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
}
Write-Host "OK Verzeichnis: $InstallPath" -ForegroundColor Green

# 3. Kopiere Dateien
Write-Host "`n[3/8] Kopiere Programm-Dateien..." -ForegroundColor Yellow
$sourceFiles = Join-Path $PSScriptRoot "Files"
if (-not (Test-Path $sourceFiles)) {
    Write-Host "X FEHLER: Files-Verzeichnis nicht gefunden!" -ForegroundColor Red
    Write-Host "   Erwartet: $sourceFiles" -ForegroundColor Yellow
    pause
    exit 1
}

Copy-Item "$sourceFiles\*" -Destination $InstallPath -Recurse -Force
$fileCount = (Get-ChildItem $InstallPath -File).Count
Write-Host "OK $fileCount Dateien kopiert" -ForegroundColor Green

# 4. Config-Editor oeffnen
Write-Host "`n[4/8] Konfiguration..." -ForegroundColor Yellow
Write-Host "      WICHTIG: Passe die Konfiguration an!" -ForegroundColor Red
Write-Host "      - WebSocketUrl: Adresse deines CentralServers" -ForegroundColor Yellow
Write-Host "      - DeviceName: Name dieses PCs" -ForegroundColor Yellow
Write-Host "      - Username/Password: Zugangsdaten" -ForegroundColor Yellow
Write-Host "      - MaxVolume: Maximale Lautstaerke (0-100)" -ForegroundColor Yellow
Write-Host ""

notepad "$InstallPath\config.json"
$response = Read-Host "Wurde config.json gespeichert? (J/N)"
if ($response -ne "J" -and $response -ne "j") {
    Write-Host "X Installation abgebrochen" -ForegroundColor Red
    pause
    exit 1
}
Write-Host "OK Konfiguration gespeichert" -ForegroundColor Green

# 5. Service installieren
Write-Host "`n[5/8] Installiere Windows-Dienst..." -ForegroundColor Yellow
$exePath = Join-Path $InstallPath "WindowsRemoteTools.exe"
$binPath = "`"$exePath`" --service"

sc.exe create $ServiceName binPath= $binPath start= auto DisplayName= "Windows Remote Tools Service" obj= "LocalSystem" | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "X Service-Installation fehlgeschlagen!" -ForegroundColor Red
    pause
    exit 1
}

sc.exe description $ServiceName "Ueberwacht und steuert Windows-Einstellungen remote. Startet automatisch die UI-Komponente neu." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null

Write-Host "OK Service installiert" -ForegroundColor Green

# 6. Service starten
Write-Host "`n[6/8] Starte Service..." -ForegroundColor Yellow
sc.exe start $ServiceName | Out-Null
Start-Sleep -Seconds 5

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "OK Service laeuft" -ForegroundColor Green
} else {
    Write-Host "! Service-Status: $($service.Status)" -ForegroundColor Yellow
}

# 7. Validierung
Write-Host "`n[7/8] Validiere Installation..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

$processes = Get-Process | Where-Object { $_.Name -like "*WindowsRemote*" }
Write-Host "      Laufende Prozesse:" -ForegroundColor Cyan
$processes | Format-Table Id, ProcessName, SessionId -AutoSize

$serviceProc = $processes | Where-Object { $_.Name -eq "WindowsRemoteTools" }
$uiProc = $processes | Where-Object { $_.Name -eq "WindowsRemoteToolsUI" }

if ($serviceProc -and $uiProc) {
    Write-Host "OK Beide Prozesse laufen" -ForegroundColor Green

    $currentSession = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    if ($uiProc.SessionId -eq $currentSession) {
        Write-Host "OK UI laeuft in der richtigen Session ($currentSession)" -ForegroundColor Green
    } else {
        Write-Host "! UI laeuft in Session $($uiProc.SessionId), erwartet: $currentSession" -ForegroundColor Yellow
    }
} else {
    Write-Host "! Nicht alle Prozesse laufen!" -ForegroundColor Yellow
    Write-Host "   Service: $($serviceProc -ne $null)" -ForegroundColor Yellow
    Write-Host "   UI: $($uiProc -ne $null)" -ForegroundColor Yellow
}

# 8. Abschluss
Write-Host "`n[8/8] Installation abgeschlossen!" -ForegroundColor Green
Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "    INSTALLATION ERFOLGREICH!           " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Service:" -ForegroundColor Cyan
Write-Host "  Name:       $ServiceName" -ForegroundColor White
Write-Host "  Status:     $($service.Status)" -ForegroundColor White
Write-Host "  Autostart:  Ja" -ForegroundColor White
Write-Host ""
Write-Host "Installation:" -ForegroundColor Cyan
Write-Host "  Pfad:       $InstallPath" -ForegroundColor White
Write-Host "  Config:     $InstallPath\config.json" -ForegroundColor White
Write-Host ""
Write-Host "System Tray:" -ForegroundColor Cyan
Write-Host "  Ein blaues Icon sollte im System Tray sichtbar sein" -ForegroundColor White
Write-Host ""
Write-Host "Naechste Schritte:" -ForegroundColor Yellow
Write-Host "  1. Pruefe System Tray Icon" -ForegroundColor White
Write-Host "  2. Teste Watchdog: Stop-Process -Name WindowsRemoteToolsUI" -ForegroundColor White
Write-Host "  3. Verbinde mit CentralServer" -ForegroundColor White
Write-Host ""

# Event Log
Write-Host "Event Log (neueste Eintraege):" -ForegroundColor Cyan
Get-EventLog -LogName Application -Source $ServiceName -Newest 5 -ErrorAction SilentlyContinue | Format-Table TimeGenerated, EntryType, Message -Wrap

Write-Host ""
pause
'@

$installContent | Out-File "$OutputPath\Install.ps1" -Encoding UTF8
Write-Host "OK Install.ps1 erstellt" -ForegroundColor Green

# 6. Erstelle Deinstallations-Skript
Write-Host "`n[6/6] Erstelle Deinstallations-Skript..." -ForegroundColor Yellow

$uninstallContent = @'
# Windows Remote Tools - Uninstaller
# MUSS ALS ADMINISTRATOR AUSGEFUEHRT WERDEN!

param(
    [string]$InstallPath = "C:\Program Files\WindowsRemoteTools",
    [string]$ServiceName = "WindowsRemoteToolsService"
)

# Pruefe Admin-Rechte
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "FEHLER: Dieses Skript muss als Administrator ausgefuehrt werden!" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "=== Windows Remote Tools - Deinstallation ===" -ForegroundColor Cyan
Write-Host ""

# Bestaetigung
$confirm = Read-Host "Moechten Sie Windows Remote Tools wirklich deinstallieren? (J/N)"
if ($confirm -ne "J" -and $confirm -ne "j") {
    Write-Host "Deinstallation abgebrochen." -ForegroundColor Yellow
    pause
    exit 0
}

# 1. Service stoppen
Write-Host "`n[1/3] Stoppe Service..." -ForegroundColor Yellow
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Write-Host "OK Service entfernt" -ForegroundColor Green
} else {
    Write-Host "OK Service nicht gefunden (uebersprungen)" -ForegroundColor Green
}

# 2. Prozesse beenden
Write-Host "`n[2/3] Beende Prozesse..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.Name -like "*WindowsRemote*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "OK Prozesse beendet" -ForegroundColor Green

# 3. Dateien loeschen
Write-Host "`n[3/3] Loesche Dateien..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Remove-Item $InstallPath -Recurse -Force
    Write-Host "OK Dateien geloescht: $InstallPath" -ForegroundColor Green
} else {
    Write-Host "OK Verzeichnis nicht gefunden (uebersprungen)" -ForegroundColor Green
}

Write-Host "`n=== Deinstallation abgeschlossen ===" -ForegroundColor Green
Write-Host ""
pause
'@

$uninstallContent | Out-File "$OutputPath\Uninstall.ps1" -Encoding UTF8
Write-Host "OK Uninstall.ps1 erstellt" -ForegroundColor Green

# 7. Erstelle README
$readmeContent = @"
# Windows Remote Tools - Installations-Paket

## Installation

### Voraussetzungen
- Windows 10/11 (64-bit)
- Administrator-Rechte
- .NET 8.0 Runtime (wird automatisch verwendet, wenn vorhanden)

### Schritt-fuer-Schritt

1. **Kopiere diesen gesamten Ordner** auf den Ziel-PC

2. **Rechtsklick auf PowerShell** -> "Als Administrator ausfuehren"

3. **Fuehre Install.ps1 aus:**
   ``````powershell
   cd "C:\Pfad\Zu\WindowsRemoteTools-Deployment"
   .\Install.ps1
   ``````

4. **Passe config.json an** (oeffnet sich automatisch):
   - WebSocketUrl: Adresse deines CentralServers (z.B. "ws://192.168.1.100:8080")
   - DeviceName: Name fuer diesen PC (z.B. "Tochter-PC")
   - Username: Benutzername fuer CentralServer
   - Password: Passwort fuer CentralServer
   - MaxVolume: Maximale Lautstaerke (0-100)

5. **Speichere config.json** und bestaetige im Installer

6. **Fertig!** Der Service startet automatisch.

## Ueberpruefung

Nach der Installation:

1. **System Tray Icon** - Sollte ein blaues Icon sichtbar sein
2. **Prozesse pruefen:**
   ``````powershell
   Get-Process | Where-Object { `$_.Name -like "*WindowsRemote*" }
   ``````
   Sollte 2 Prozesse zeigen:
   - WindowsRemoteTools (Service)
   - WindowsRemoteToolsUI (UI)

3. **Watchdog testen:**
   ``````powershell
   Stop-Process -Name WindowsRemoteToolsUI
   # Warte 5 Sekunden - UI sollte automatisch neu starten
   ``````

## Deinstallation

``````powershell
.\Uninstall.ps1
``````

## Troubleshooting

### Service startet nicht
``````powershell
# Event Log pruefen
Get-EventLog -LogName Application -Source WindowsRemoteToolsService -Newest 10
``````

### UI nicht sichtbar
``````powershell
# Sessions pruefen
Get-Process | Where-Object { `$_.Name -like "*WindowsRemote*" } | Format-Table Id, ProcessName, SessionId
``````
UI sollte in deiner Session laufen (nicht Session 0).

### Config-Fehler
Pruefe C:\Program Files\WindowsRemoteTools\config.json:
- Muss gueltiges JSON sein
- Keine Kommas am Ende
- WebSocketUrl muss "ws://" oder "wss://" haben

## Support

Bei Problemen:
1. Event Log pruefen (siehe oben)
2. Service neu starten: Restart-Service WindowsRemoteToolsService
3. Installation wiederholen (erst Uninstall.ps1, dann Install.ps1)

## Dateien

- Install.ps1 - Installations-Skript
- Uninstall.ps1 - Deinstallations-Skript
- Files\ - Programm-Dateien
  - WindowsRemoteTools.exe - Service
  - WindowsRemoteToolsUI.exe - UI
  - config.json - Konfiguration
  - *.dll - Abhaengigkeiten

## Automatischer Start

Der Service ist auf "Automatisch" konfiguriert und startet bei jedem Systemstart.

Die UI wird automatisch vom Service in der Benutzer-Session gestartet.

## Sicherheit

- Service laeuft als SYSTEM
- Nur Administratoren koennen den Service stoppen
- UI kann von Benutzern beendet werden, startet aber automatisch neu (Watchdog)
"@

$readmeContent | Out-File "$OutputPath\README.md" -Encoding UTF8
Write-Host "OK README.md erstellt" -ForegroundColor Green

# Zusammenfassung
Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "  DEPLOYMENT-PAKET ERFOLGREICH ERSTELLT! " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Ausgabe-Verzeichnis:" -ForegroundColor Cyan
Write-Host "  $OutputPath" -ForegroundColor White
Write-Host ""
Write-Host "Inhalt:" -ForegroundColor Cyan
Write-Host "  Install.ps1       - Installations-Skript" -ForegroundColor White
Write-Host "  Uninstall.ps1     - Deinstallations-Skript" -ForegroundColor White
Write-Host "  README.md         - Dokumentation" -ForegroundColor White
Write-Host "  Files\            - Programm-Dateien ($fileCount Dateien)" -ForegroundColor White
Write-Host ""
Write-Host "Naechste Schritte:" -ForegroundColor Yellow
Write-Host "  1. Kopiere den gesamten Ordner auf den Ziel-PC" -ForegroundColor White
Write-Host "  2. Fuehre Install.ps1 als Administrator aus" -ForegroundColor White
Write-Host "  3. Passe config.json an" -ForegroundColor White
Write-Host ""
Write-Host "Deployment-Paket ist bereit fuer die Installation!" -ForegroundColor Green
Write-Host ""
