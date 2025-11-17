# Windows Remote Tools - Installationsanleitung

## Schnellstart

### 1. Kompilieren

```bash
# Service kompilieren
cd C:\Programming\Windows_Remotetools\WindowsRemoteTools
dotnet build -c Release

# UI kompilieren
cd C:\Programming\Windows_Remotetools\WindowsRemoteToolsUI
dotnet build -c Release
```

### 2. Dateien kopieren

Erstelle Installations-Verzeichnis:

```powershell
mkdir "C:\Program Files\WindowsRemoteTools"
```

Kopiere ALLE Dateien aus beiden bin-Verzeichnissen:

```powershell
# Service-Dateien
copy "C:\Programming\Windows_Remotetools\WindowsRemoteTools\bin\Release\net8.0-windows\*" "C:\Program Files\WindowsRemoteTools\"

# UI-Dateien (überschreibt nichts, fügt nur WindowsRemoteToolsUI.exe hinzu)
copy "C:\Programming\Windows_Remotetools\WindowsRemoteToolsUI\bin\Release\net8.0-windows\WindowsRemoteToolsUI.*" "C:\Program Files\WindowsRemoteTools\"
```

### 3. config.json erstellen

Erstelle `C:\Program Files\WindowsRemoteTools\config.json`:

```json
{
  "WebSocketUrl": "wss://your-server.com:8080",
  "DeviceName": "PC-Name",
  "Username": "your-username",
  "Password": "your-password",
  "MaxVolume": 75,
  "EnforceMaxVolume": true,
  "AutoReconnect": true,
  "ReconnectDelay": 5000
}
```

### 4. Service installieren

**Als Administrator in PowerShell:**

```powershell
# Service installieren
sc.exe create WindowsRemoteToolsService `
  binPath= "C:\Program Files\WindowsRemoteTools\WindowsRemoteTools.exe --service" `
  start= auto `
  DisplayName= "Windows Remote Tools Service"

# Service starten
sc.exe start WindowsRemoteToolsService

# Status prüfen
sc.exe query WindowsRemoteToolsService
```

### 5. Überprüfen

**Event Log prüfen:**

```powershell
Get-EventLog -LogName Application -Source WindowsRemoteToolsService -Newest 10
```

**Erwartete Einträge:**
- "Windows Remote Tools Service is starting..."
- "UI process watchdog started"
- "Windows Remote Tools Service started successfully"
- "UI process started"

**Prozesse prüfen:**

```powershell
Get-Process | Where-Object { $_.Name -like "*WindowsRemote*" }
```

**Erwartete Prozesse:**
- WindowsRemoteTools.exe (Service)
- WindowsRemoteToolsUI.exe (UI im System Tray)

## Testen

### 1. UI beenden

```powershell
Stop-Process -Name WindowsRemoteToolsUI
```

**Ergebnis**: UI startet innerhalb von 5 Sekunden automatisch neu.

### 2. CentralServer-Verbindung testen

Öffne den CentralServer:

```
http://localhost:8080/devices
```

Der Windows-PC sollte mit Desktop-Icon erscheinen.

### 3. Overlay testen

Vom CentralServer aus:

```javascript
// Im Browser-Console
fetch('/WebSocks/command', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    id: 'WIN-PCNAME',
    command: 'show_notification',
    parameters: { message: 'Test Notification' }
  })
})
```

**Ergebnis**: Overlay erscheint auf dem Windows-PC.

## Deinstallation

```powershell
# Service stoppen
sc.exe stop WindowsRemoteToolsService

# Service deinstallieren
sc.exe delete WindowsRemoteToolsService

# Optional: Dateien löschen
Remove-Item -Path "C:\Program Files\WindowsRemoteTools" -Recurse -Force
```

## Fehlerbehebung

### Problem: UI startet nicht

**Diagnose:**

```powershell
# Event Log prüfen
Get-EventLog -LogName Application -Source WindowsRemoteToolsService -Newest 10

# Manuell testen
cd "C:\Program Files\WindowsRemoteTools"
.\WindowsRemoteToolsUI.exe
```

**Mögliche Ursachen:**
- WindowsRemoteToolsUI.exe fehlt
- Newtonsoft.Json.dll fehlt
- Keine Benutzer-Session aktiv

### Problem: Named Pipe Fehler

**Diagnose:**

Starte beide manuell in separaten Konsolen:

```powershell
# Konsole 1 (Service-Test)
cd "C:\Program Files\WindowsRemoteTools"
.\WindowsRemoteTools.exe --console

# Konsole 2 (UI-Test)
cd "C:\Program Files\WindowsRemoteTools"
.\WindowsRemoteToolsUI.exe
```

**Erwartete Ausgabe:**
- Service: "ServicePipeServer: Waiting for client connection..."
- Service: "ServicePipeServer: Client connected!"
- UI: "UIPipeClient: Connecting to service..."
- UI: "UIPipeClient: Connected to service!"

### Problem: WebSocket-Verbindung schlägt fehl

**Diagnose:**

```powershell
# config.json prüfen
Get-Content "C:\Program Files\WindowsRemoteTools\config.json"

# Netzwerk testen
Test-NetConnection -ComputerName your-server.com -Port 8080
```

**Mögliche Ursachen:**
- Firewall blockiert Port 8080
- Server nicht erreichbar
- Falsche URL in config.json

### Problem: Volume wird nicht begrenzt

**Diagnose:**

```powershell
# NAudio-Bibliothek prüfen
Get-ChildItem "C:\Program Files\WindowsRemoteTools" | Where-Object { $_.Name -like "*NAudio*" }
```

**Mögliche Ursachen:**
- NAudio.dll fehlt
- EnforceMaxVolume ist false in config.json

## Erweiterte Konfiguration

### 1. Service-Account ändern

Standardmäßig läuft der Service als SYSTEM. Um ihn als spezifischer Benutzer laufen zu lassen:

```powershell
sc.exe config WindowsRemoteToolsService obj= ".\Username" password= "Password"
```

**ACHTUNG**: Service braucht Admin-Rechte für Volume-Steuerung!

### 2. Autostart-Verzögerung

Um Systemressourcen zu schonen:

```powershell
sc.exe config WindowsRemoteToolsService start= delayed-auto
```

### 3. Recovery-Optionen

Bei Absturz automatisch neu starten:

```powershell
sc.exe failure WindowsRemoteToolsService reset= 86400 actions= restart/60000/restart/120000/restart/300000
```

## Logs

### Event Log

```powershell
# Alle Einträge anzeigen
Get-EventLog -LogName Application -Source WindowsRemoteToolsService

# Nur Fehler
Get-EventLog -LogName Application -Source WindowsRemoteToolsService -EntryType Error
```

### Console-Logs (zum Debuggen)

```powershell
# Service im Console-Modus starten
.\WindowsRemoteTools.exe --console
```

## Nächste Schritte

1. **BIOS-Passwort setzen** - Verhindert Boot in abgesicherten Modus
2. **Benutzerrechte einschränken** - Reduziere Rechte der Tochter
3. **CentralServer konfigurieren** - Für Remote-Steuerung
4. **Backup erstellen** - Von config.json und Installation

## Support

Bei weiteren Fragen:
- Prüfe WATCHDOG_ARCHITECTURE.md für technische Details
- Prüfe Event Log für Fehlermeldungen
- Teste manuell mit --console Modus
