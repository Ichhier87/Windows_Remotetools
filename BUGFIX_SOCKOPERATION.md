# Bugfix: SockOperation Befehle für Windows-Devices

## Problem
Beim Zugriff auf verschiedene CentralServer-Endpunkte (z.B. `/WebSocks/RecordSettings`) für Windows-Devices hängt die Anfrage und lädt nicht weiter.

## Ursache
Der CentralServer sendet SockOperation-Befehle (REC, AUDIO_SETTINGS, DISPLAY, etc.), die für Android-Devices entwickelt wurden. Windows Remote Tools kannte diese Befehle nicht und sendete keine Response zurück. Der Server wartete auf eine Response (via Callback), die nie kam, wodurch die Anfrage timeout lief.

## Lösung
Windows Remote Tools wurde erweitert, um alle CentralServer SockOperation-Befehle zu verstehen und entsprechende Responses zurückzusenden.

## Implementierte SockOperations

### 1. **REC (Recording)**
Audio-Recording ist für Windows nicht implementiert, aber der Client sendet trotzdem gültige Responses:

```csharp
// Load Settings
{
    "UUID": "...",
    "record_filename": "recording.wav",
    "record_duration": "60",
    "record_partLength": "10",
    "record_doUpload": false,
    "supported": false
}

// Save/Start/Stop
{
    "UUID": "...",
    "status": "not_supported",
    "message": "Audio recording is not implemented for Windows devices"
}
```

### 2. **AUDIO_SETTINGS**
Lautstärke-Steuerung über SockOperation:

```csharp
// Get Volume Info
{
    "UUID": "...",
    "volume": 75,
    "muted": false
}

// Set Volume
{
    "UUID": "...",
    "volume": 75
}

// Set Max Volume
{
    "UUID": "...",
    "maxVolume": 100
}
```

### 3. **DISPLAY**
Display-Steuerung über SockOperation:

```csharp
// Set Brightness
{
    "UUID": "...",
    "brightness": 80
}

// Display Off/Close Window
{
    "UUID": "...",
    "status": "ok"
}
```

### 4. **Nicht unterstützte Operationen**
Folgende SockOperations sind für Windows nicht implementiert und senden eine Standard-Response:

- **AUDIO_PLAYBACK** - Audio-Datei-Wiedergabe
- **FILES** - Datei-Operations
- **APPS** - App-Management
- **GPS** - GPS/Location
- **UPLOAD_BASE64** - File-Upload

```csharp
{
    "UUID": "...",
    "status": "not_supported",
    "message": "Operation 'X' is not supported on Windows devices"
}
```

## Änderungen im Detail

### WebSocketClient.cs

**Neue Methoden:**

1. **HandleRecordOperation()** - Zeile 285-324
   - Behandelt REC-Befehle (load_settings, save_settings, start, stop, status)
   - Sendet Default-Werte für Recording-Settings
   - Markiert Recording als "not supported"

2. **HandleAudioSettingsOperation()** - Zeile 326-372
   - Behandelt AUDIO_SETTINGS-Befehle
   - Unterstützt: get_volumeinfo, set_volume, set_maxvolume
   - Nutzt VolumeController für tatsächliche Steuerung

3. **HandleDisplayOperation()** - Zeile 374-417
   - Behandelt DISPLAY-Befehle
   - Unterstützt: setBrightness, aus, closeWindow
   - Nutzt DisplayController und OverlayWindow

4. **SendNotSupportedResponse()** - Zeile 419-430
   - Sendet Standard-Response für nicht unterstützte Operationen
   - Verhindert Timeouts im CentralServer

**Erweiterte HandleServerMessage()** - Zeile 216-283
- Erkennt jetzt SockOperation message types (REC, AUDIO_SETTINGS, DISPLAY, etc.)
- Routet zu entsprechenden Handler-Methoden
- Sendet "not supported" für nicht implementierte Operationen

## Befehlsformat

### Von CentralServer zu Windows Remote Tools

**SockOperation Format:**
```json
{
    "type": "REC",
    "UUID": "unique-request-id",
    "OP": "load_settings",
    "id": "device-name"
}
```

**Command Format (bisherig):**
```json
{
    "command": "set_volume",
    "params": {
        "volume": 50,
        "UUID": "unique-request-id"
    }
}
```

Beide Formate werden jetzt unterstützt!

## Testing

### 1. Windows Remote Tools neu starten

```cmd
cd C:\Programming\Windows_Remotetools\WindowsRemoteTools\bin\Release\net8.0-windows
WindowsRemoteTools.exe --console
```

### 2. CentralServer neu starten

```bash
cd C:\Programming\CentralServer
npm start
```

### 3. RecordSettings testen

Im Browser:
```
http://localhost:8080/WebSocks/RecordSettings?id=WIN-7LK33F2LO6R
```

**Erwartetes Ergebnis:**
- Seite lädt vollständig (kein Timeout)
- Recording-Settings werden angezeigt (mit Default-Werten)
- Ggf. Hinweis, dass Recording nicht unterstützt wird

### 4. Volume Settings testen

Im Browser:
```
http://localhost:8080/WebSocks/AudioSettings?id=WIN-7LK33F2LO6R&type=media
```

**Erwartetes Ergebnis:**
- Aktuelle Lautstärke wird angezeigt
- Mute-Status wird angezeigt

### 5. Display Settings testen

Im Browser über Device-UI:
- Brightness Slider sollte funktionieren
- Display Off Button sollte funktionieren

## Console-Ausgaben

### Windows Remote Tools

```
Received server message: REC
Received REC operation: load_settings

Received server message: AUDIO_SETTINGS
Received AUDIO_SETTINGS operation: get_volumeinfo

Received server message: DISPLAY
Received DISPLAY operation: setBrightness
```

### CentralServer

Keine spezielle Ausgabe, aber der Request sollte erfolgreich abgeschlossen werden (Status 200).

## Unterschiede Android vs. Windows

| Operation | Android | Windows |
|-----------|---------|---------|
| **REC** | Vollständig unterstützt | Dummy-Response (not supported) |
| **AUDIO_SETTINGS** | Multi-Stream (Ring/Media/Alarm) | Master Volume only |
| **DISPLAY** | Brightness 0-255 + Night Mode | Brightness 0-100, kein Night Mode |
| **FILES** | Vollständig unterstützt | Not supported |
| **APPS** | Vollständig unterstützt | Not supported |
| **GPS** | Vollständig unterstützt | Not supported |
| **AUDIO_PLAYBACK** | Vollständig unterstützt | Not supported (use Overlay) |

## Zukünftige Erweiterungen

### 1. Audio Recording für Windows
Könnte mit NAudio implementiert werden:
```csharp
using NAudio.Wave;

var waveIn = new WaveInEvent();
var writer = new WaveFileWriter("recording.wav", waveIn.WaveFormat);
// ... recording implementation
```

### 2. File Operations
Könnte mit System.IO implementiert werden:
```csharp
// List files
var files = Directory.GetFiles(path);

// Upload/Download files
File.WriteAllBytes(path, data);
```

### 3. App Management
Prozess-Verwaltung:
```csharp
// List processes
Process.GetProcesses();

// Start app
Process.Start(appPath);
```

## Bekannte Limitierungen

1. **Recording:**
   - Nicht implementiert für Windows
   - UI zeigt Default-Werte
   - Start/Stop-Buttons funktionieren nicht

2. **Audio Playback:**
   - Keine direkte Sound-File-Wiedergabe
   - Alternativ: Overlay mit Text-Nachricht

3. **GPS:**
   - Windows-PCs haben normalerweise kein GPS
   - Könnte mit Windows Location API implementiert werden

## Commit-Info

**Geänderte Dateien:**
- `WindowsRemoteTools/WebSocketClient.cs`

**Feature:** SockOperation-Support für Windows-Devices
**Fix:** RecordSettings und andere Endpoints laden jetzt korrekt
