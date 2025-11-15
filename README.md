# Windows Remote Tools - Tray Application

Eine Windows-Anwendung, die im System-Tray läuft und über WebSocket-Befehle ferngesteuert werden kann.

## Features

- **System Tray Icon**: Läuft unauffällig im Hintergrund mit Tray-Icon
- **WebSocket-Steuerung**: Empfängt Befehle über WebSocket-Verbindung
- **Vollbild-Overlay**: Kann Nachrichten über den gesamten Bildschirm anzeigen
- **Lautstärke-Kontrolle**: Steuert die System-Lautstärke
- **Maximale Lautstärke**: Kann eine maximale Lautstärke definieren und durchsetzen
- **Automatische Überwachung**: Überwacht kontinuierlich die Lautstärke

## Voraussetzungen

- Windows 10/11
- Python 3.8 oder höher

## Installation

1. Repository klonen oder herunterladen
2. Virtuelle Umgebung erstellen (empfohlen):
   ```bash
   python -m venv venv
   venv\Scripts\activate
   ```

3. Abhängigkeiten installieren:
   ```bash
   pip install -r requirements.txt
   ```

## Verwendung

### Anwendung starten

```bash
python main.py
```

Die Anwendung startet im System Tray. Ein blaues Icon erscheint in der Taskleiste.

### Tray-Menü

Rechtsklick auf das Tray-Icon öffnet das Menü mit folgenden Optionen:

- **Status**: Zeigt den WebSocket-Verbindungsstatus
- **WebSocket**: WebSocket-Verbindung steuern
- **Volume**: Lautstärke-Einstellungen
  - Aktuelle Lautstärke anzeigen
  - Maximale Lautstärke setzen
  - Stummschaltung
  - Lautstärke-Überwachung aktivieren
- **Overlay**: Test-Overlays anzeigen
- **Exit**: Anwendung beenden

### WebSocket-Befehle

Die Anwendung verbindet sich standardmäßig mit `ws://localhost:8765`.

#### Beispiel-Server starten

Für Tests können Sie den mitgelieferten Beispiel-Server verwenden:

```bash
python example_server.py
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
        "bg_color": "black",
        "text_color": "white",
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

## Konfiguration

Die Konfiguration wird in `config.json` gespeichert und kann manuell bearbeitet werden:

```json
{
    "ws_host": "localhost",
    "ws_port": 8765,
    "max_volume": 100,
    "volume_check_interval": 1.0,
    "enforce_max_volume": true
}
```

### Konfigurationsoptionen

- **ws_host**: WebSocket-Server-Hostname
- **ws_port**: WebSocket-Server-Port
- **max_volume**: Maximale Lautstärke in Prozent (0-100)
- **volume_check_interval**: Überprüfungsintervall in Sekunden
- **enforce_max_volume**: Lautstärke-Limit durchsetzen (true/false)

## Projektstruktur

```
Windows_Remotetools/
├── main.py                 # Haupteinstiegspunkt
├── tray_app.py            # Tray-Anwendungslogik
├── config.py              # Konfigurations-Handler
├── volume_controller.py   # Lautstärke-Steuerung
├── overlay_window.py      # Vollbild-Overlay
├── websocket_client.py    # WebSocket-Client
├── example_server.py      # Beispiel WebSocket-Server
├── requirements.txt       # Python-Abhängigkeiten
├── config.json           # Konfigurationsdatei (erstellt beim ersten Start)
└── README.md             # Diese Datei
```

## Anwendungsbeispiele

### 1. Bildschirm sperren über WebSocket

```python
import asyncio
import websockets
import json

async def lock_screen():
    uri = "ws://localhost:8765"
    async with websockets.connect(uri) as websocket:
        command = {
            "command": "show_blocking_screen",
            "params": {
                "message": "Computer gesperrt!\nBitte warten..."
            }
        }
        await websocket.send(json.dumps(command))

asyncio.run(lock_screen())
```

### 2. Maximale Lautstärke auf 50% begrenzen

```python
import asyncio
import websockets
import json

async def limit_volume():
    uri = "ws://localhost:8765"
    async with websockets.connect(uri) as websocket:
        # Maximale Lautstärke setzen
        await websocket.send(json.dumps({
            "command": "set_max_volume",
            "params": {"max_volume": 50}
        }))

        # Durchsetzung aktivieren
        await websocket.send(json.dumps({
            "command": "enable_volume_enforcement",
            "params": {"enable": true}
        }))

asyncio.run(limit_volume())
```

### 3. Benachrichtigung anzeigen

```python
import asyncio
import websockets
import json

async def show_notification():
    uri = "ws://localhost:8765"
    async with websockets.connect(uri) as websocket:
        command = {
            "command": "show_notification",
            "params": {
                "message": "Erinnerung: Besprechung in 5 Minuten!"
            }
        }
        await websocket.send(json.dumps(command))

asyncio.run(show_notification())
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

## Troubleshooting

### Anwendung startet nicht
- Überprüfen Sie, ob alle Abhängigkeiten installiert sind
- Stellen Sie sicher, dass Python 3.8+ verwendet wird
- Unter Windows müssen ggf. Administratorrechte für Audio-Kontrolle erteilt werden

### WebSocket verbindet nicht
- Überprüfen Sie, ob der Server läuft
- Prüfen Sie die Konfiguration in `config.json`
- Firewall-Einstellungen überprüfen

### Lautstärke-Steuerung funktioniert nicht
- Die Anwendung benötigt Zugriff auf Windows Audio-API
- Unter Windows 10/11 sollte dies standardmäßig funktionieren
- Testen Sie mit Administratorrechten

## Lizenz

Dieses Projekt steht zur freien Verfügung.

## Autor

Erstellt mit Claude Code
