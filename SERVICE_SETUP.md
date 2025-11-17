# Windows Remote Tools - Service Mode Setup

Windows Remote Tools kann jetzt in drei verschiedenen Modi ausgeführt werden:

## Betriebsmodi

### 1. GUI-Modus (Standard)
Startet die Anwendung mit System Tray Icon und grafischer Oberfläche.

```cmd
WindowsRemoteTools.exe
```

### 2. Console-Modus (Headless)
Startet die Anwendung im Konsolen-Modus ohne GUI. Ideal für Tests oder temporäre Ausführung.

```cmd
WindowsRemoteTools.exe --console
```

Beenden: Drücke `Q` oder `Ctrl+C`

### 3. Windows Service-Modus
Startet die Anwendung als Windows-Dienst im Hintergrund.

```cmd
WindowsRemoteTools.exe --service
```

## Service Installation

### Voraussetzungen
- Windows 10/11 oder Windows Server
- Administratorrechte
- .NET 8.0 Runtime

### Installationsschritte

1. **Projekt builden (Release-Modus)**
   ```cmd
   cd C:\Programming\Windows_Remotetools
   dotnet build WindowsRemoteTools.sln -c Release
   ```

2. **Service installieren**
   - Rechtsklick auf `install-service.bat`
   - "Als Administrator ausführen" wählen

   Alternativ über Command-Line (als Administrator):
   ```cmd
   sc create WindowsRemoteToolsService binPath= "C:\Programming\Windows_Remotetools\WindowsRemoteTools\bin\Release\net8.0-windows\WindowsRemoteTools.exe --service" start= auto DisplayName= "Windows Remote Tools Service"
   sc start WindowsRemoteToolsService
   ```

### Service-Verwaltung

**Status prüfen:**
```cmd
sc query WindowsRemoteToolsService
```

**Service starten:**
```cmd
sc start WindowsRemoteToolsService
```

**Service stoppen:**
```cmd
sc stop WindowsRemoteToolsService
```

**Service deinstallieren:**
- Rechtsklick auf `uninstall-service.bat`
- "Als Administrator ausführen" wählen

Alternativ:
```cmd
sc stop WindowsRemoteToolsService
sc delete WindowsRemoteToolsService
```

### Event Log

Der Service schreibt Logs in das Windows Event Log:
- Event Viewer öffnen (`eventvwr.msc`)
- Windows-Protokolle > Anwendung
- Nach Quelle "WindowsRemoteToolsService" filtern

## Konfiguration

Die Konfiguration wird aus der `config.json` im Anwendungsverzeichnis geladen.

### Konfigurationsdatei erstellen

Kopiere `config.example.json` nach `config.json` und passe die Werte an:

```cmd
copy config.example.json config.json
```

### Beispiel-Konfiguration (CentralServer):

```json
{
  "WsHost": "localhost",
  "WsPort": 9090,
  "WsUseSSL": true,
  "DeviceName": "MyWindowsPC",
  "Username": "myusername",
  "Password": "mypassword",
  "MaxVolume": 75,
  "VolumeCheckInterval": 1.0,
  "EnforceMaxVolume": true,
  "IgnoreSslErrors": true
}
```

### Wichtige Parameter:

- **WsHost**: Hostname/IP des CentralServers
- **WsPort**: WebSocket-Port (Standard: 9090 für CentralServer)
- **WsUseSSL**: true für wss://, false für ws://
- **DeviceName**: Name des Geräts (wird im CentralServer angezeigt)
- **Username/Password**: Credentials für CentralServer (optional)
- **IgnoreSslErrors**: true für selbstsignierte Zertifikate

Siehe auch: **CENTRALSERVER_INTEGRATION.md** für Details zur CentralServer-Integration

## Automatischer Start

Der Service ist standardmäßig auf "Automatisch" konfiguriert und startet automatisch mit Windows.

Um dies zu ändern:
```cmd
sc config WindowsRemoteToolsService start= demand  REM Manueller Start
sc config WindowsRemoteToolsService start= auto    REM Automatischer Start
sc config WindowsRemoteToolsService start= disabled REM Deaktiviert
```

## Fehlerbehebung

### Service startet nicht
1. Prüfe das Event Log auf Fehlermeldungen
2. Stelle sicher, dass .NET 8.0 Runtime installiert ist
3. Prüfe, ob der Pfad zur .exe korrekt ist
4. Stelle sicher, dass die Konfigurationsdatei existiert und gültig ist

### Service läuft, aber funktioniert nicht
1. Prüfe die WebSocket-Verbindung in der Konfiguration
2. Stelle sicher, dass der Service die notwendigen Berechtigungen hat
3. Prüfe das Event Log für Laufzeitfehler

### Overlay funktioniert nicht im Service-Modus
Dies ist eine Windows-Einschränkung. Services laufen im Session 0 und können keine GUI-Elemente auf dem Desktop anzeigen. Für Overlay-Funktionalität verwende den GUI- oder Console-Modus.

## Unterschiede zwischen den Modi

| Feature | GUI-Modus | Console-Modus | Service-Modus |
|---------|-----------|---------------|---------------|
| System Tray Icon | ✓ | ✗ | ✗ |
| WebSocket Client | ✓ | ✓ | ✓ |
| Volume Control | ✓ | ✓ | ✓ |
| Overlay (eingeschränkt) | ✓ | ✓ | ✗* |
| Automatischer Start | Autostart-Ordner | ✗ | ✓ |
| Benutzerinteraktion | ✓ | Konsole | Event Log |

*Overlays funktionieren im Service-Modus nicht, da Services in Session 0 laufen.

## Weitere Informationen

- Hauptdokumentation: `README.md`
- Beispiel-Server: `ExampleServer/`
