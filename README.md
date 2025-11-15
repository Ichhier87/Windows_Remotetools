# Windows Remote Tools - Tray Application (C#)

Eine kompilierte Windows-Anwendung in C#/.NET, die im System-Tray läuft und über WebSocket-Befehle ferngesteuert werden kann. Nach dem Kompilieren ist der Code unveränderlich und als eigenständige .exe ausführbar.

## Features

- **System Tray Icon**: Läuft unauffällig im Hintergrund mit Tray-Icon
- **WebSocket-Steuerung**: Empfängt Befehle über WebSocket-Verbindung
- **Vollbild-Overlay**: Kann Nachrichten über den gesamten Bildschirm anzeigen
- **Lautstärke-Kontrolle**: Steuert die System-Lautstärke
- **Maximale Lautstärke**: Kann eine maximale Lautstärke definieren und durchsetzen
- **Automatische Überwachung**: Überwacht kontinuierlich die Lautstärke
- **Kompiliert**: Kompiliert zu einer eigenständigen .exe-Datei

## Voraussetzungen

- Windows 10/11
- .NET 8.0 SDK (für Entwicklung/Kompilierung)
- Visual Studio 2022 oder Visual Studio Code (optional)

## Installation

### Option 1: Mit .NET SDK kompilieren

1. Repository klonen oder herunterladen
2. Im Projektverzeichnis öffnen
3. Projekt kompilieren:
   ```bash
   dotnet build WindowsRemoteTools.sln -c Release
   ```

4. Die kompilierte .exe finden Sie unter:
   ```
   WindowsRemoteTools/bin/Release/net8.0-windows/WindowsRemoteTools.exe
   ```

### Option 2: Selbständige Executable erstellen (ohne .NET Runtime erforderlich)

Für eine komplett eigenständige .exe, die keine .NET Installation benötigt:

```bash
dotnet publish WindowsRemoteTools/WindowsRemoteTools.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Die .exe finden Sie dann unter:
```
WindowsRemoteTools/bin/Release/net8.0-windows/win-x64/publish/WindowsRemoteTools.exe
```

## Verwendung

### Anwendung starten

Doppelklick auf `WindowsRemoteTools.exe` oder über die Kommandozeile:

```bash
WindowsRemoteTools.exe
```

Die Anwendung startet im System Tray. Ein blaues Icon erscheint in der Taskleiste.

### Tray-Menü

Rechtsklick auf das Tray-Icon öffnet das Menü mit folgenden Optionen:

- **Status**: Zeigt den WebSocket-Verbindungsstatus
- **WebSocket**: WebSocket-Verbindung steuern
- **Volume**: Lautstärke-Einstellungen
  - Aktuelle Lautstärke anzeigen
  - Maximale Lautstärke setzen (50%, 75%, 100%)
  - Stummschaltung
  - Lautstärke-Überwachung aktivieren
- **Overlay**: Test-Overlays anzeigen
  - Test Overlay
  - Blocking Screen
  - Overlay ausblenden
- **Volume Monitoring**: Aktiviert/deaktiviert die kontinuierliche Lautstärkeüberwachung
- **Exit**: Anwendung beenden

### WebSocket-Befehle

Die Anwendung verbindet sich standardmäßig mit `ws://localhost:8765`.

#### Beispiel-Server starten

Für Tests können Sie den mitgelieferten Beispiel-Server verwenden:

```bash
dotnet run --project ExampleServer/ExampleServer.csproj
```

Oder kompiliert:

```bash
dotnet build ExampleServer/ExampleServer.csproj -c Release
ExampleServer/bin/Release/net8.0/ExampleServer.exe
```

Der Server bietet ein interaktives Menü zum Senden von Befehlen.

#### Befehlsformat

Befehle werden als JSON gesendet:

```json
{
    "command": "command_name",
    "params": {
        "param1": "value1"
    }
}
```

### Verfügbare Befehle

#### Overlay-Befehle

**show_overlay** - Vollbild-Overlay anzeigen
```json
{
    "command": "show_overlay",
    "params": {
        "message": "Ihre Nachricht",
        "bg_color": "Black",
        "text_color": "White",
        "opacity": 0.9
    }
}
```

**hide_overlay** - Overlay ausblenden
```json
{
    "command": "hide_overlay",
    "params": {}
}
```

**show_blocking_screen** - Sperrbildschirm anzeigen
```json
{
    "command": "show_blocking_screen",
    "params": {
        "message": "Bildschirm gesperrt"
    }
}
```

**show_notification** - Benachrichtigung anzeigen
```json
{
    "command": "show_notification",
    "params": {
        "message": "Wichtige Nachricht"
    }
}
```

**show_warning** - Warnung anzeigen
```json
{
    "command": "show_warning",
    "params": {
        "message": "Warnung!"
    }
}
```

**show_error** - Fehler anzeigen
```json
{
    "command": "show_error",
    "params": {
        "message": "Ein Fehler ist aufgetreten"
    }
}
```

#### Lautstärke-Befehle

**set_volume** - Lautstärke setzen (0-100%)
```json
{
    "command": "set_volume",
    "params": {
        "volume": 50
    }
}
```

**get_volume** - Aktuelle Lautstärke abfragen
```json
{
    "command": "get_volume",
    "params": {}
}
```

**set_mute** - Stummschalten
```json
{
    "command": "set_mute",
    "params": {
        "mute": true
    }
}
```

**toggle_mute** - Stummschaltung umschalten
```json
{
    "command": "toggle_mute",
    "params": {}
}
```

**set_max_volume** - Maximale Lautstärke festlegen
```json
{
    "command": "set_max_volume",
    "params": {
        "max_volume": 75
    }
}
```

**enable_volume_enforcement** - Lautstärke-Durchsetzung aktivieren/deaktivieren
```json
{
    "command": "enable_volume_enforcement",
    "params": {
        "enable": true
    }
}
```

**ping** - Verbindung testen
```json
{
    "command": "ping",
    "params": {}
}
```

## Konfiguration

Die Konfiguration wird in `config.json` gespeichert und kann manuell bearbeitet werden:

```json
{
    "WsHost": "localhost",
    "WsPort": 8765,
    "MaxVolume": 100,
    "VolumeCheckInterval": 1.0,
    "EnforceMaxVolume": true
}
```

### Konfigurationsoptionen

- **WsHost**: WebSocket-Server-Hostname
- **WsPort**: WebSocket-Server-Port
- **MaxVolume**: Maximale Lautstärke in Prozent (0-100)
- **VolumeCheckInterval**: Überprüfungsintervall in Sekunden
- **EnforceMaxVolume**: Lautstärke-Limit durchsetzen (true/false)

## Projektstruktur

```
Windows_Remotetools/
├── WindowsRemoteTools.sln          # Visual Studio Solution
├── WindowsRemoteTools/             # Hauptprojekt
│   ├── WindowsRemoteTools.csproj
│   ├── Program.cs                  # Einstiegspunkt
│   ├── TrayApp.cs                  # Tray-Icon-Anwendung
│   ├── ConfigManager.cs            # Konfigurationsverwaltung
│   ├── VolumeController.cs         # Lautstärkekontrolle
│   ├── OverlayWindow.cs            # Vollbild-Overlay
│   └── WebSocketClient.cs          # WebSocket-Client
├── ExampleServer/                  # Beispiel-Server
│   ├── ExampleServer.csproj
│   └── Program.cs                  # Server mit interaktivem Menü
├── config.json                     # Konfigurationsdatei (wird erstellt)
└── README.md                       # Diese Datei
```

## Verwendete Technologien

- **.NET 8.0**: Moderne .NET-Plattform
- **WinForms**: Für Tray-Icon und Overlay-Fenster
- **NAudio**: Für Windows Audio API Zugriff
- **Newtonsoft.Json**: Für JSON-Verarbeitung
- **System.Net.WebSockets**: Native WebSocket-Unterstützung

## Anwendungsbeispiele

### 1. Bildschirm sperren über WebSocket (C#)

```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri("ws://localhost:8765"), CancellationToken.None);

        var command = "{\"command\":\"show_blocking_screen\",\"params\":{\"message\":\"Gesperrt!\"}}";
        var bytes = Encoding.UTF8.GetBytes(command);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
```

### 2. Eigenen WebSocket-Server erstellen

Sie können Ihren eigenen Server in jeder Sprache erstellen, die WebSockets unterstützt (Python, Node.js, Go, etc.).

**Beispiel in Python:**

```python
import asyncio
import websockets
import json

async def send_command(websocket):
    command = {
        "command": "set_max_volume",
        "params": {"max_volume": 50}
    }
    await websocket.send(json.dumps(command))

async def main():
    async with websockets.connect("ws://localhost:8765") as websocket:
        await send_command(websocket)

asyncio.run(main())
```

## Overlay schließen

Alle Overlays können geschlossen werden durch:
- **ESC-Taste** drücken
- **Klick** auf das Overlay
- WebSocket-Befehl `hide_overlay`

## Sicherheitshinweise

- Die Anwendung sollte nur in vertrauenswürdigen Netzwerken verwendet werden
- Der WebSocket-Server sollte mit Authentifizierung gesichert werden
- Für Produktivumgebungen sollte eine verschlüsselte Verbindung (WSS) verwendet werden
- Die kompilierte .exe kann mit Code-Signing signiert werden für zusätzliche Sicherheit

## Entwicklung

### Projekt in Visual Studio öffnen

1. Visual Studio 2022 öffnen
2. `WindowsRemoteTools.sln` öffnen
3. F5 drücken zum Debuggen

### Projekt in VS Code öffnen

1. VS Code öffnen
2. Ordner öffnen
3. C# Extension installieren
4. F5 drücken zum Debuggen

### Abhängigkeiten

Die NuGet-Pakete werden automatisch beim Build wiederhergestellt:
- NAudio 2.2.1
- Newtonsoft.Json 13.0.3

## Troubleshooting

### Anwendung startet nicht
- Stellen Sie sicher, dass .NET 8.0 Runtime installiert ist (bei nicht-self-contained Builds)
- Unter Windows müssen ggf. Administratorrechte für Audio-Kontrolle erteilt werden

### WebSocket verbindet nicht
- Überprüfen Sie, ob der Server läuft
- Prüfen Sie die Konfiguration in `config.json`
- Firewall-Einstellungen überprüfen

### Lautstärke-Steuerung funktioniert nicht
- Die Anwendung benötigt Zugriff auf Windows Audio-API
- Unter Windows 10/11 sollte dies standardmäßig funktionieren
- Testen Sie mit Administratorrechten

### Build-Fehler
- Stellen Sie sicher, dass .NET 8.0 SDK installiert ist
- Führen Sie `dotnet restore` aus
- Löschen Sie die `bin/` und `obj/` Ordner und kompilieren Sie neu

## Lizenz

Dieses Projekt steht zur freien Verfügung.

## Autor

Erstellt mit Claude Code
