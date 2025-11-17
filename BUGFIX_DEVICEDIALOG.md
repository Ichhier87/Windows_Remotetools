# Bugfix: DeviceDialog JavaScript-Fehler behoben

## Problem
Beim Öffnen des Device-Dialogs im CentralServer erschien ein JavaScript-Fehler:
```
TypeError: Cannot read properties of undefined (reading 'level')
at LoadAll (DeviceSite.js:198:31)
```

## Ursache
Die ALL_SETTINGS Response von Windows Remote Tools sendete ein flaches JSON-Format:
```json
{
  "UUID": "...",
  "volume": 75,
  "muted": false,
  "maxVolume": 100,
  ...
}
```

Aber DeviceSite.js erwartete das verschachtelte Android-Format:
```javascript
result.akku.level           // Zeile 198
result.fileSystem.used      // Zeile 199
result.display.IS_ON        // Zeile 200
result.sound.CURRENT        // Zeile 207
```

## Lösung
Windows Remote Tools sendet jetzt das vollständige Android-kompatible Format mit allen erwarteten Feldern.

## Neue ALL_SETTINGS Response

### Vollständiges Format

```json
{
  "UUID": "unique-request-id",

  "akku": {
    "level": -1,          // -1 = keine Batterie (Desktop-PC)
    "charging": false,
    "temperature": 0
  },

  "fileSystem": {
    "used": 0,            // Nicht implementiert für Windows
    "fullsize": 0,
    "available": 0
  },

  "display": {
    "IS_ON": true,
    "MAGIC_WINDOW_VISIBLE": false,
    "CURRENT_BRIGHTNESS": 80
  },

  "sound": {
    "CURRENT": 75,        // Aktuelle Lautstärke
    "CURRENT_MAX": 100,   // Maximale Lautstärke
    "MIN": 0              // Minimale Lautstärke
  },

  "playback": false,      // Audio-Playback nicht implementiert
  "DoNotDisturb": true,   // Gemappt auf EnforceMaxVolume
  "klingelton": "default",

  "deviceType": "windows",
  "platform": "Win32NT",
  "osVersion": "Microsoft Windows NT 10.0.19045.0",
  "deviceName": "WIN-7LK33F2LO6R",
  "deviceId": "aabbccddeeff"
}
```

## Feld-Mapping

### Batterie (`akku`)
| Feld | Windows | Android |
|------|---------|---------|
| `level` | `-1` (keine Batterie) | `0-100` |
| `charging` | `false` | `true/false` |
| `temperature` | `0` | Temperatur in °C |

**Hinweis:** Desktop-PCs haben keine Batterie, daher `level: -1`. Die UI sollte dies entsprechend darstellen (z.B. "N/A" oder Batterie-Icon ausblenden).

### File System (`fileSystem`)
| Feld | Windows | Android |
|------|---------|---------|
| `used` | `0` | Verwendeter Speicher in MB |
| `fullsize` | `0` | Gesamtspeicher in MB |
| `available` | `0` | Verfügbarer Speicher in MB |

**Hinweis:** File System Monitoring ist für Windows nicht implementiert. Werte sind `0`.

### Display (`display`)
| Feld | Windows | Android |
|------|---------|---------|
| `IS_ON` | `true/false` | `true/false` |
| `MAGIC_WINDOW_VISIBLE` | `false` | `true/false` |
| `CURRENT_BRIGHTNESS` | `0-100` | `0-255` |

**Hinweis:**
- Helligkeit-Bereich unterschiedlich: Windows `0-100`, Android `0-255`
- Magic Window ist ein Android-Feature, bei Windows immer `false`

### Sound (`sound`)
| Feld | Windows | Android |
|------|---------|---------|
| `CURRENT` | Master Volume `0-100` | Stream Volume `0-100` |
| `CURRENT_MAX` | Max Volume Limit | Max Stream Volume |
| `MIN` | `0` | Min Stream Volume |

**Hinweis:** Windows hat nur Master Volume, während Android mehrere Audio-Streams hat (Ring, Media, Alarm, etc.).

### Andere Felder
| Feld | Windows | Android |
|------|---------|---------|
| `playback` | `false` | `true/false` |
| `DoNotDisturb` | Gemappt auf `EnforceMaxVolume` | DND-Status |
| `klingelton` | `"default"` | Ringtone-Name |

## UI-Verhalten

### Batterie-Anzeige
Die UI sollte prüfen, ob `akku.level === -1` und entsprechend reagieren:
```javascript
if (result.akku.level === -1) {
    // Desktop-PC ohne Batterie
    updateBattery("N/A")  // oder Icon ausblenden
} else {
    updateBattery(result.akku.level)
}
```

### Speicherplatz-Anzeige
Da File System Info nicht verfügbar ist (`0 MB / 0 MB`), könnte die UI anzeigen:
```javascript
if (result.fileSystem.fullsize === 0) {
    SpeicherplatzItem().innerHTML = "Nicht verfügbar"
} else {
    SpeicherplatzItem().innerHTML = result.fileSystem.used + "MB/" + result.fileSystem.fullsize + "MB"
}
```

### Helligkeit-Slider
Beachte den unterschiedlichen Wertebereich:
```javascript
if (deviceType === 'windows') {
    // Brightness 0-100
    BrightnessSlider.max = 100
} else {
    // Android: Brightness 0-255
    BrightnessSlider.max = 255
}
```

## Änderungen im Detail

### WebSocketClient.cs - SendDeviceSettings()

**Zeile 432-501:**

```csharp
private async Task SendDeviceSettings(string? uuid)
{
    var currentVolume = _volumeController.GetVolumePercent();
    var currentBrightness = _displayController.GetBrightness();
    var isDisplayOn = _displayController.IsDisplayOn();

    var response = new JObject
    {
        ["UUID"] = uuid,
        ["akku"] = new JObject { ... },
        ["fileSystem"] = new JObject { ... },
        ["display"] = new JObject { ... },
        ["sound"] = new JObject { ... },
        ["playback"] = false,
        ["DoNotDisturb"] = _config.EnforceMaxVolume,
        ["klingelton"] = "default",
        // Additional Windows info
        ["deviceType"] = _config.DeviceType,
        ...
    };

    await SendMessage(response);
}
```

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

### 3. Device-Dialog öffnen

Im Browser:
```
http://localhost:8080/WebSocks
```

Dann auf ein Windows-Device klicken.

**Erwartetes Ergebnis:**
- ✅ Kein JavaScript-Fehler mehr
- ✅ Device-Dialog öffnet sich vollständig
- ✅ Lautstärke wird korrekt angezeigt
- ✅ Helligkeit wird angezeigt
- ✅ Display-Status wird angezeigt
- ⚠️ Batterie zeigt "N/A" oder ähnlich (da -1)
- ⚠️ Speicherplatz zeigt "0 MB / 0 MB" (nicht implementiert)

## Console-Ausgaben

### Windows Remote Tools

```
Received server message: ALL_SETTINGS
Sent device settings response for UUID: 9b419f1a-2685-409d-b8a8-b34b7af3cd48
```

### Browser Console (DeviceSite.js)

```javascript
{
  UUID: "9b419f1a-2685-409d-b8a8-b34b7af3cd48",
  akku: { level: -1, charging: false, temperature: 0 },
  fileSystem: { used: 0, fullsize: 0, available: 0 },
  display: { IS_ON: true, MAGIC_WINDOW_VISIBLE: false, CURRENT_BRIGHTNESS: 80 },
  sound: { CURRENT: 8, CURRENT_MAX: 100, MIN: 0 },
  playback: false,
  DoNotDisturb: true,
  klingelton: "default",
  deviceType: "windows",
  ...
}
```

**Kein Fehler mehr!** ✅

## Bekannte UI-Einschränkungen

Da einige Features nicht für Windows verfügbar sind, sollten folgende UI-Elemente angepasst werden:

1. **Batterie-Icon/Anzeige:**
   - Bei `level: -1` ausblenden oder "N/A" anzeigen

2. **Speicherplatz:**
   - Bei `fullsize: 0` "Nicht verfügbar" anzeigen

3. **Audio Playback:**
   - Bei Windows immer `false`, Button könnte deaktiviert sein

4. **Magic Window:**
   - Nur Android-Feature, für Windows irrelevant

5. **Klingelton:**
   - Bei Windows nicht relevant, UI könnte ausgeblendet werden

## Zukünftige Verbesserungen

### 1. Echte Batterie-Erkennung für Laptops

```csharp
using System.Windows.Forms;

var powerStatus = SystemInformation.PowerStatus;
if (powerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery)
{
    akku["level"] = (int)(powerStatus.BatteryLifePercent * 100);
    akku["charging"] = powerStatus.PowerLineStatus == PowerLineStatus.Online;
}
```

### 2. File System Info

```csharp
using System.IO;

var drive = DriveInfo.GetDrives()
    .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

if (drive != null)
{
    fileSystem["fullsize"] = drive.TotalSize / 1024 / 1024;
    fileSystem["available"] = drive.AvailableFreeSpace / 1024 / 1024;
    fileSystem["used"] = (drive.TotalSize - drive.AvailableFreeSpace) / 1024 / 1024;
}
```

### 3. Device-Type-Aware UI

Die CentralServer UI könnte Device-Typ-spezifische Elemente anzeigen/ausblenden:

```javascript
if (result.deviceType === 'windows') {
    // Windows-spezifische UI
    hideElement('#klingelton-section')
    hideElement('#playback-section')

    if (result.akku.level === -1) {
        hideElement('#battery-section')
    }
} else {
    // Android-spezifische UI
    showAllElements()
}
```

## Commit-Info

**Geänderte Dateien:**
- `WindowsRemoteTools/WebSocketClient.cs`

**Fix:** ALL_SETTINGS Response jetzt im Android-kompatiblen Format
**Issue:** JavaScript-Fehler "Cannot read properties of undefined (reading 'level')" behoben
