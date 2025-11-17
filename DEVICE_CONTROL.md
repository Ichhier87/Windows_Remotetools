# Device Control - Lautstärke und Display-Steuerung

Diese Dokumentation beschreibt die Lautstärke- und Display-Steuerungsfunktionen für Windows-PCs und Android-Geräte über den CentralServer.

## Übersicht

Das System unterstützt nun zwei Device-Typen:
- **Windows** - Windows-PCs mit vollständiger Display- und Lautstärke-Steuerung
- **Android** - Android-Geräte (Standard)

Der CentralServer erkennt automatisch den Device-Typ und zeigt entsprechende Steuerungsmöglichkeiten an.

## Device-Typ-Erkennung

### Automatische Erkennung

Bei der Verbindung sendet jedes Device seinen Typ:

**Windows-PC:**
```json
{
  "IDENT": "MyWindowsPC",
  "deviceType": "windows",
  "platform": "Win32NT",
  "osVersion": "Microsoft Windows NT 10.0.19045.0",
  "volume": 75,
  "displayon": "true"
}
```

**Android-Device:**
```json
{
  "IDENT": "MyAndroidPhone",
  "deviceType": "android",
  "akku": 85,
  "volume": 50,
  "displayon": "true"
}
```

### Datenbankstruktur

Der CentralServer speichert den Device-Typ in der Datenbank:

```sql
CREATE TABLE devices (
  id INTEGER PRIMARY KEY,
  deviceid TEXT,
  name TEXT,
  ip TEXT,
  device_type TEXT DEFAULT 'android',
  platform TEXT,
  os_version TEXT,
  last_update INTEGER
)
```

## Lautstärke-Steuerung

### Windows-PC

Windows Remote Tools unterstützt vollständige Lautstärke-Steuerung:

#### Befehle

```json
// Lautstärke setzen (0-100)
{
  "command": "set_volume",
  "params": { "volume": 50, "UUID": "request-id" }
}

// Lautstärke abrufen
{
  "command": "get_volume",
  "params": { "UUID": "request-id" }
}

// Stummschalten
{
  "command": "set_mute",
  "params": { "mute": true }
}

// Stummschaltung umschalten
{
  "command": "toggle_mute",
  "params": {}
}

// Maximale Lautstärke setzen
{
  "command": "set_max_volume",
  "params": { "max_volume": 75, "UUID": "request-id" }
}

// Lautstärke-Überwachung aktivieren
{
  "command": "enable_volume_enforcement",
  "params": { "enable": true }
}
```

#### Implementierung

Die Lautstärke-Steuerung verwendet die Windows CoreAudio API über NAudio:

```csharp
// VolumeController.cs
public void SetVolumePercent(int percent)
{
    var volume = Math.Max(0, Math.Min(100, percent));
    _device.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100.0f;
}

public int GetVolumePercent()
{
    return (int)Math.Round(_device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
}
```

### Android-Device

Für Android-Devices gelten die standard SockOperation-Befehle:

```javascript
// AudioSettingSockOp
op.SetVolume(value, type)  // type: "ring", "media", "alarm", "notification"
op.GetVolumeInfo(type)
op.SetMaxVolume(value, type)
op.SetDoNotDisturb(value)
```

## Display-Steuerung

### Windows-PC

Windows Remote Tools bietet Display-Steuerung über WMI und Windows API:

#### Befehle

```json
// Helligkeit setzen (0-100)
{
  "command": "setBrightness",
  "params": { "value": 80, "UUID": "request-id" }
}

// Helligkeit abrufen
{
  "command": "getBrightness",
  "params": { "UUID": "request-id" }
}

// Display ausschalten
{
  "command": "display_off",
  "params": {}
}

// Display einschalten
{
  "command": "display_on",
  "params": {}
}
```

#### Implementierung

Die Display-Steuerung nutzt WMI für Helligkeit und Windows Messages für Power:

```csharp
// DisplayController.cs
public bool SetBrightness(int brightness)
{
    using (var mClass = new System.Management.ManagementClass("WmiMonitorBrightnessMethods"))
    {
        mClass.Scope = new System.Management.ManagementScope(@"\\.\root\wmi");
        using (var instances = mClass.GetInstances())
        {
            foreach (System.Management.ManagementObject instance in instances)
            {
                var args = new object[] { 1, brightness };
                instance.InvokeMethod("WmiSetBrightness", args);
                return true;
            }
        }
    }
}

public void TurnDisplayOff()
{
    SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, MONITOR_OFF);
}
```

**Hinweis:** Helligkeit-Steuerung funktioniert nur auf Laptops/Notebooks mit WMI-Support.

### Android-Device

Für Android-Devices gelten die standard SockOperation-Befehle:

```javascript
// DisplaySockOp
op.SetBrightness(value)   // 0-255
op.SetNightMode(value)    // true/false
op.DisplayAus()           // Display ausschalten
op.CloseWindow()          // Overlay schließen
```

## CentralServer API

### Volume-Endpunkte

```javascript
// POST /WebSocks/Volume
// Lautstärke setzen
{
  "id": "device-name",
  "data": {
    "value": 50,
    "type": "media"  // Nur für Android
  }
}

// GET /WebSocks/AudioSettings?id=device-name&type=media
// Lautstärke abrufen
```

### Display-Endpunkte

```javascript
// POST /WebSocks/Brightness
// Helligkeit setzen
{
  "id": "device-name",
  "data": {
    "value": 80
  }
}

// POST /WebSocks/DisplayAus
// Display ausschalten
{
  "id": "device-name"
}
```

### Device-Type-Aware Befehle

Der CentralServer kann Device-Typ-spezifische Befehle senden:

```javascript
// Beispiel: Automatische Befehlsauswahl
const deviceInfo = await getDeviceInfo(deviceName);

if (deviceInfo.device_type === 'windows') {
    // Windows-spezifische Befehle
    sendCommand({
        command: 'setBrightness',
        params: { value: 80 }
    });
} else {
    // Android-spezifische Befehle (SockOperation)
    const op = new DisplaySockOp(deviceName);
    op.SetBrightness(204); // 0-255 für Android
    op.send();
}
```

## Unterschiede zwischen Windows und Android

| Feature | Windows | Android |
|---------|---------|---------|
| **Lautstärke-Bereich** | 0-100% | 0-100% |
| **Lautstärke-Typen** | Master (System) | Ring, Media, Alarm, Notification |
| **Helligkeit-Bereich** | 0-100% | 0-255 |
| **Helligkeit-API** | WMI (Laptops only) | Android System API |
| **Display Power** | Windows Messages | Android Power Manager |
| **Night Mode** | Nicht unterstützt | Unterstützt |
| **Do Not Disturb** | Nicht unterstützt | Unterstützt |

## Verwendung im CentralServer

### Device-Liste mit Typ anzeigen

Die Device-Liste enthält jetzt den Device-Typ:

```javascript
const devices = await Devices.Database.GetDevices();

devices.forEach(device => {
    console.log(`Device: ${device.name}`);
    console.log(`Type: ${device.device_type}`);
    console.log(`Platform: ${device.platform}`);
    console.log(`OS: ${device.os_version}`);
});
```

### UI-Anpassung (Empfohlen)

In der WebSocks-Oberfläche sollte der Device-Typ berücksichtigt werden:

```javascript
// views/websocks.ejs (Beispiel)
<% devices.forEach(device => { %>
    <div class="device">
        <h3><%= device.name %></h3>
        <span class="badge"><%= device.device_type %></span>

        <% if (device.device_type === 'windows') { %>
            <!-- Windows-spezifische Steuerung -->
            <div class="brightness-control">
                <label>Helligkeit (0-100%)</label>
                <input type="range" min="0" max="100" />
            </div>
        <% } else { %>
            <!-- Android-spezifische Steuerung -->
            <div class="brightness-control">
                <label>Helligkeit (0-255)</label>
                <input type="range" min="0" max="255" />
            </div>
        <% } %>
    </div>
<% }); %>
```

## Beispiel-Integration

### CentralServer → Windows-PC

```javascript
const WebSock = require('./WebSock');
const instance = WebSock.GetInstance();

// Helligkeit auf 80% setzen
const socket = instance.getSocketForDevice('MyWindowsPC');
socket.send(JSON.stringify({
    command: 'setBrightness',
    params: {
        value: 80,
        UUID: WebSock.generateUUID()
    }
}));

// Lautstärke auf 50% setzen
socket.send(JSON.stringify({
    command: 'set_volume',
    params: {
        volume: 50,
        UUID: WebSock.generateUUID()
    }
}));
```

### CentralServer → Android-Device

```javascript
const SockOperation = require('./SockOperation');

// Helligkeit auf 204/255 setzen (ca. 80%)
const displayOp = new SockOperation.DisplaySockOp('MyAndroidPhone');
displayOp.SetBrightness(204);
displayOp.send();

// Lautstärke auf 50% setzen (Media)
const audioOp = new SockOperation.AudioSettingSockOp('MyAndroidPhone');
audioOp.SetVolume(50, 'media');
audioOp.send();
```

## Fehlerbehandlung

### Windows-PC

**Helligkeit nicht unterstützt:**
```
Error setting brightness: ...
Brightness control may not be supported on this system
```

Dies tritt auf Desktop-PCs ohne WMI-Helligkeitssupport auf.

**Lösung:** Helligkeit-Steuerung ist nur auf Laptops/Notebooks verfügbar.

### Android-Device

Siehe bestehende SockOperation-Dokumentation.

## Testing

### Windows Remote Tools testen

```cmd
# Console-Modus starten
WindowsRemoteTools.exe --console

# Helligkeit testen (wenn unterstützt)
# Über CentralServer-UI oder API

# Lautstärke testen
# Über CentralServer-UI oder API
```

### CentralServer testen

```bash
# CentralServer starten
cd C:\Programming\CentralServer
npm start

# Browser öffnen
https://localhost:9090/WebSocks

# Device sollte mit Typ angezeigt werden
# Windows-Devices zeigen "windows" Badge
# Android-Devices zeigen "android" Badge
```

## Weitere Informationen

- **CentralServer Integration:** `CENTRALSERVER_INTEGRATION.md`
- **Service Setup:** `SERVICE_SETUP.md`
- **Main README:** `README.md`
