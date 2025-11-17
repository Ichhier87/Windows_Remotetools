# Bugfix: ALL_SETTINGS Fehler behoben

## Problem
Beim Klick auf ein Windows-Device im CentralServer erschien die Fehlermeldung:
**"Geräteeinstellungen konnten nicht geladen werden."**

## Ursache
1. Windows-Devices sendeten kein `akku` Feld bei der Registrierung
2. CentralServer registrierte Devices ohne `akku` nur als "basic device"
3. Dabei ging die `device_type` Information verloren
4. ALL_SETTINGS Response hatte falsches Format

## Lösung

### 1. Windows Remote Tools (`WebSocketClient.cs`)

**Identification-Nachricht erweitert:**
```csharp
{
    "IDENT": "MyWindowsPC",
    "deviceId": "...",
    "deviceType": "windows",
    "platform": "Win32NT",
    "osVersion": "...",
    "volume": 75,
    "displayon": "true",
    "akku": -1,              // NEU: -1 = keine Batterie (Desktop-PC)
    "serverrunning": "true"  // NEU
}
```

**ALL_SETTINGS Response korrigiert:**
```csharp
// Vorher (falsch):
{
    "type": "ALL_SETTINGS_RESPONSE",
    "UUID": "...",
    "data": { ... }
}

// Nachher (richtig, Android-kompatibel):
{
    "UUID": "...",
    "volume": 75,
    "muted": false,
    "maxVolume": 100,
    "brightness": 80,
    ...
}
```

### 2. CentralServer (`WebSock.js`)

**RegisterBasicDevice erweitert:**
```javascript
// Vorher:
static async RegisterBasicDevice(deviceName, deviceIP, deviceId)

// Nachher:
static async RegisterBasicDevice(deviceName, deviceIP, deviceId,
    deviceType = 'android', platform = null, osVersion = null)
```

Jetzt werden `device_type`, `platform` und `os_version` auch bei basic devices gespeichert.

## Änderungen im Detail

### Windows Remote Tools

**Datei:** `WebSocketClient.cs`

1. **SendIdentification()** - Zeile 105-145
   - Hinzugefügt: `["akku"] = -1`
   - Hinzugefügt: `["serverrunning"] = "true"`

2. **SendDeviceSettings()** - Zeile 263-301
   - Response-Format geändert: Daten direkt auf root-level statt in "data" object
   - Besseres Logging für Debugging

### CentralServer

**Datei:** `WebSock.js`

1. **RegisterBasicDevice()** - Zeile 809-830
   - Parameter erweitert: `deviceType`, `platform`, `osVersion`
   - SQL erweitert: Speichert jetzt alle Device-Infos
   - Logging verbessert

2. **RegisterDevice()** - Zeile 795-802
   - Übergibt Device-Typ an RegisterBasicDevice
   - Verbesserte Logging-Ausgabe

## Batterie-Status für Windows-Devices

Windows-PCs haben normalerweise keine Batterie (Desktop-PCs). Der Wert wird wie folgt interpretiert:

| Wert | Bedeutung |
|------|-----------|
| `-1` | Keine Batterie (Desktop-PC) |
| `0` | Batterie leer |
| `1-100` | Batterie-Ladung in % |

Android-Devices senden weiterhin echte Batterie-Werte (0-100).

## Testing

### 1. Datenbank-Migration

Die bestehende Datenbank wird automatisch erweitert. Keine manuellen Änderungen nötig.

### 2. Windows Remote Tools neu starten

```cmd
cd C:\Programming\Windows_Remotetools\WindowsRemoteTools\bin\Release\net8.0-windows

# Stoppen falls läuft
# Neu starten
WindowsRemoteTools.exe --console
```

### 3. CentralServer neu starten

```bash
cd C:\Programming\CentralServer

# Stoppen (Ctrl+C)
# Neu starten
npm start
```

### 4. Device-Registrierung prüfen

Im CentralServer Console sollte erscheinen:
```
Device registered with full info: MyWindowsPC (192.168.1.100) [Type: windows]
```

### 5. WebSocks-UI testen

1. Browser öffnen: `https://localhost:9090/WebSocks`
2. Auf Windows-Device klicken
3. **Erwartetes Ergebnis:**
   - Geräteeinstellungen werden geladen
   - Lautstärke wird angezeigt
   - Helligkeit wird angezeigt (falls unterstützt)
   - Keine Fehlermeldung

## Erwartete Console-Ausgaben

### Windows Remote Tools

```
Connecting to wss://localhost:9090...
WebSocket connected!
Sent identification: MyWindowsPC (...) [Type: windows]
Received server message: USER_AUTH_OK
Successfully authenticated as user: myusername
Received command: ALL_SETTINGS
Sent device settings response for UUID: ...
```

### CentralServer

```
Device registered with full info: MyWindowsPC (192.168.1.100) [Type: windows]
```

## Bekannte Limitierungen

1. **Helligkeit auf Desktop-PCs:**
   - Funktioniert nur auf Laptops/Notebooks
   - Desktop-PCs zeigen Fehler in der Console (normal)

2. **Batterie-Anzeige:**
   - Windows-Devices zeigen `-1` (keine Batterie)
   - UI sollte dies berücksichtigen (z.B. "N/A" anzeigen)

## Zukünftige Verbesserungen

1. **CentralServer UI anpassen:**
   - Batterie-Anzeige für `-1` als "N/A" oder Icon ausblenden
   - Windows-spezifische Icons/Badges
   - Desktop vs. Laptop Erkennung

2. **Echte Batterie-Erkennung für Windows-Laptops:**
   ```csharp
   // Mögliche Erweiterung
   var batteryStatus = SystemInformation.PowerStatus;
   if (batteryStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBatttery)
   {
       identMessage["akku"] = (int)(batteryStatus.BatteryLifePercent * 100);
   }
   ```

## Commit-Info

**Geänderte Dateien:**
- `WindowsRemoteTools/WebSocketClient.cs`
- `CentralServer/WebSock.js`

**Bug:** Device-Einstellungen konnten nicht geladen werden
**Fix:** Windows-Devices senden jetzt `akku: -1` und korrekte ALL_SETTINGS Response
