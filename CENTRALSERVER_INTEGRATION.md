# Windows Remote Tools - CentralServer Integration

Diese Anleitung beschreibt, wie Windows Remote Tools mit dem CentralServer verbunden wird, um als Device registriert und ferngesteuert zu werden.

## Übersicht

Windows Remote Tools kann sich nun als Device beim CentralServer registrieren und:
- Einem Benutzer zugeordnet werden
- Ferngesteuert werden (Lautstärke, Overlays, etc.)
- Über die CentralServer-Weboberfläche verwaltet werden
- Als Windows-Service im Hintergrund laufen

## Voraussetzungen

1. **CentralServer** läuft auf einem Server (Standard: Port 9090 mit HTTPS/WSS)
2. **Windows Remote Tools** auf dem Client-PC installiert
3. Benutzerkonto auf dem CentralServer (optional, aber empfohlen)

## Konfiguration

### 1. Beispiel config.json erstellen

Erstelle eine `config.json` im gleichen Verzeichnis wie die .exe:

```json
{
  "WsHost": "your-server.example.com",
  "WsPort": 9090,
  "WsUseSSL": true,
  "DeviceName": "MyWindowsPC",
  "DeviceId": "auto-generated",
  "Username": "your-username",
  "Password": "your-password",
  "MaxVolume": 75,
  "VolumeCheckInterval": 1.0,
  "EnforceMaxVolume": true,
  "IgnoreSslErrors": true
}
```

### 2. Konfigurationsparameter

| Parameter | Beschreibung | Standard |
|-----------|--------------|----------|
| `WsHost` | Hostname/IP des CentralServers | `localhost` |
| `WsPort` | WebSocket-Port des Servers | `9090` |
| `WsUseSSL` | SSL/TLS verwenden (wss://) | `true` |
| `DeviceName` | Name des Geräts im CentralServer | Computername |
| `DeviceId` | Eindeutige ID (MAC-Adresse) | Auto |
| `Username` | Benutzername für CentralServer | `null` |
| `Password` | Passwort für CentralServer | `null` |
| `MaxVolume` | Maximale Lautstärke (%) | `100` |
| `VolumeCheckInterval` | Prüfintervall in Sekunden | `1.0` |
| `EnforceMaxVolume` | Max. Lautstärke erzwingen | `true` |
| `IgnoreSslErrors` | Selbstsignierte Zertifikate akzeptieren | `true` |

### 3. Authentifizierung

#### Mit Benutzerkonto (empfohlen)

Setze `Username` und `Password` in der config.json:

```json
{
  "Username": "myusername",
  "Password": "mypassword"
}
```

Das Device wird automatisch dem Benutzer zugeordnet.

#### Ohne Benutzerkonto

Lasse `Username` und `Password` leer oder entferne sie:

```json
{
  "Username": null,
  "Password": null
}
```

Der CentralServer fordert später eine Benutzerzuordnung an.

## Device-Registrierung

### Automatische Registrierung

Beim Start sendet Windows Remote Tools automatisch eine Identifikationsnachricht:

```json
{
  "IDENT": "MyWindowsPC",
  "deviceId": "aabbccddeeff",
  "IP": "192.168.1.100",
  "PORT": 9090,
  "USER": "myusername",
  "PWD": "mypassword"
}
```

### Server-Antworten

Der Server kann mit folgenden Nachrichten antworten:

1. **REQUEST_USER**: Server fordert Authentifizierung an
   - Konfiguriere Username/Password in config.json

2. **AUTH_FAILED**: Authentifizierung fehlgeschlagen
   - Prüfe Username und Password

3. **USER_AUTH_OK**: Erfolgreich authentifiziert
   - Device ist jetzt dem Benutzer zugeordnet

4. **USER_AUTH_FAILED**: Benutzer-Authentifizierung fehlgeschlagen
   - Prüfe Credentials

## Unterstützte Befehle

Der CentralServer kann folgende Befehle an das Device senden:

### Volume-Befehle

```json
// Lautstärke setzen
{ "command": "set_volume", "params": { "volume": 50 } }

// Lautstärke abfragen
{ "command": "get_volume", "params": { "UUID": "request-id" } }

// Stummschalten
{ "command": "set_mute", "params": { "mute": true } }

// Stummschaltung umschalten
{ "command": "toggle_mute" }

// Maximale Lautstärke setzen
{ "command": "set_max_volume", "params": { "max_volume": 75 } }

// Lautstärke-Überwachung aktivieren
{ "command": "enable_volume_enforcement", "params": { "enable": true } }
```

### Display-Befehle

```json
// Overlay anzeigen
{
  "command": "show_overlay",
  "params": {
    "message": "Nachricht",
    "bg_color": "black",
    "text_color": "white",
    "opacity": 0.9
  }
}

// Bildschirm sperren
{ "command": "show_blocking_screen", "params": { "message": "Gesperrt" } }

// Overlay ausblenden
{ "command": "hide_overlay" }

// Notification anzeigen
{ "command": "show_notification", "params": { "message": "Info" } }

// Warnung anzeigen
{ "command": "show_warning", "params": { "message": "Warnung" } }

// Fehler anzeigen
{ "command": "show_error", "params": { "message": "Fehler" } }
```

### System-Befehle

```json
// Ping
{ "command": "ping", "params": { "UUID": "request-id" } }

// Alle Einstellungen abrufen
{ "type": "ALL_SETTINGS", "UUID": "request-id" }
```

## Als Windows-Service ausführen

### Installation

1. **Build in Release-Modus:**
   ```cmd
   dotnet build -c Release
   ```

2. **config.json erstellen** im Build-Verzeichnis:
   ```
   WindowsRemoteTools\bin\Release\net8.0-windows\config.json
   ```

3. **Service installieren** (als Administrator):
   ```cmd
   cd C:\Programming\Windows_Remotetools
   install-service.bat
   ```

### Verwaltung

```cmd
# Status prüfen
sc query WindowsRemoteToolsService

# Starten
sc start WindowsRemoteToolsService

# Stoppen
sc stop WindowsRemoteToolsService

# Deinstallieren
uninstall-service.bat
```

## CentralServer-Weboberfläche

Nach erfolgreicher Registrierung erscheint das Device im CentralServer unter:

```
https://your-server.example.com/WebSocks
```

Von dort aus kannst du:
- Device-Status sehen (Online/Offline)
- Lautstärke steuern
- Overlays senden
- Device-Einstellungen verwalten

## Fehlerbehebung

### Device verbindet nicht

1. **Prüfe Server-Erreichbarkeit:**
   ```cmd
   ping your-server.example.com
   ```

2. **Prüfe WebSocket-Port:**
   - Standard: 9090
   - Firewall-Regel prüfen

3. **Prüfe SSL-Zertifikat:**
   - Bei selbstsignierten Zertifikaten: `"IgnoreSslErrors": true`

### Authentifizierung fehlgeschlagen

1. **Prüfe Username/Password** in config.json
2. **Prüfe Benutzerkonto** auf dem CentralServer
3. **Console-Ausgabe prüfen** für Fehlermeldungen

### Device nicht in Weboberfläche sichtbar

1. **Prüfe Verbindungsstatus** in der Console
2. **Prüfe Device-Name** in config.json
3. **Prüfe Benutzerzuordnung** in der CentralServer-Datenbank

### Befehle kommen nicht an

1. **Prüfe WebSocket-Verbindung** (Console-Ausgabe)
2. **Prüfe Command-Format** im CentralServer
3. **Prüfe Device-Logs** im Event Viewer (bei Service)

## Beispiel-Konfigurationen

### Lokaler Test (CentralServer auf localhost)

```json
{
  "WsHost": "localhost",
  "WsPort": 9090,
  "WsUseSSL": true,
  "DeviceName": "TestPC",
  "Username": "testuser",
  "Password": "testpass",
  "IgnoreSslErrors": true
}
```

### Produktiv-Setup (Remote-Server)

```json
{
  "WsHost": "control.example.com",
  "WsPort": 9090,
  "WsUseSSL": true,
  "DeviceName": "Office-PC-01",
  "Username": "admin",
  "Password": "secure-password-here",
  "MaxVolume": 75,
  "EnforceMaxVolume": true,
  "IgnoreSslErrors": false
}
```

### Mehrere Devices

Jedes Device benötigt:
- Eindeutigen `DeviceName`
- Eigene config.json
- Optional: Eigenen Benutzer auf dem Server

## Integration mit bestehendem ExampleServer

Der ExampleServer unter `C:\Programming\Windows_Remotetools\ExampleServer` ist ein einfaches Beispiel.
Für die CentralServer-Integration verwende den vollständigen CentralServer unter `C:\Programming\CentralServer`.

### CentralServer starten

```cmd
cd C:\Programming\CentralServer
npm install
npm start
```

Der Server läuft dann auf: `https://localhost:9090`

## Weitere Informationen

- **Service-Dokumentation:** `SERVICE_SETUP.md`
- **CentralServer-Dokumentation:** `C:\Programming\CentralServer\docs`
- **Haupt-README:** `README.md`
