# Remotetools_Notifications Plugin

Ein Plugin für Windows Remote Tools, das die letzten 100 Benachrichtigungen speichert und abrufen kann.

## Funktionen

- **Benachrichtigungen hinzufügen**: Speichert Benachrichtigungen mit Level (info, warning, error) und Zeitstempel
- **Benachrichtigungen abrufen**: Holt alle gespeicherten Benachrichtigungen (max. 100)
- **Anzahl abrufen**: Gibt die Anzahl der gespeicherten Benachrichtigungen zurück
- **Benachrichtigungen löschen**: Löscht alle gespeicherten Benachrichtigungen
- **Nach Level filtern**: Holt nur Benachrichtigungen eines bestimmten Levels

## Architektur

### Server-seitig (Node.js/CentralServer)

1. **SockOperation**: `NotificationsSockOp` in `CentralServer/SockOperation.js`
2. **API-Endpunkte**: Registriert in `CentralServer/WebSock.js`
3. **Berechtigungen**: Automatisch über `ensureAccessAndExecute()` - Benutzer benötigen Zugriff auf das jeweilige Device

### Client-seitig (C#/Windows)

1. **Handler**: `HandlePluginOperation()` und `HandleNotificationPlugin()` in `WebSocketClient.cs`
2. **Speicherung**: `NotificationStorage` Klasse - Thread-safe, max. 100 Einträge (FIFO)

## API-Endpunkte

### 1. Benachrichtigung hinzufügen
```
POST /WebSocks/Notifications/Add
```

**Request Body:**
```json
{
  "id": "PC-001",
  "level": "info|warning|error",
  "message": "Nachrichtentext"
}
```

**Response:**
```json
{
  "UUID": "...",
  "status": "success",
  "count": 42
}
```

### 2. Benachrichtigungen abrufen
```
GET /WebSocks/Notifications/Get?id=PC-001
```

**Response:**
```json
{
  "UUID": "...",
  "notifications": [
    {
      "Level": "info",
      "Message": "Test notification",
      "Timestamp": 1700000000000,
      "DateTime": "2023-11-14T12:00:00Z"
    }
  ],
  "count": 1
}
```

### 3. Anzahl abrufen
```
GET /WebSocks/Notifications/Count?id=PC-001
```

**Response:**
```json
{
  "UUID": "...",
  "count": 42
}
```

### 4. Benachrichtigungen löschen
```
POST /WebSocks/Notifications/Clear
```

**Request Body:**
```json
{
  "id": "PC-001"
}
```

**Response:**
```json
{
  "UUID": "...",
  "status": "success",
  "count": 0
}
```

### 5. Nach Level filtern
```
GET /WebSocks/Notifications/GetByLevel?id=PC-001&level=error
```

**Response:**
```json
{
  "UUID": "...",
  "notifications": [...],
  "count": 5,
  "level": "error"
}
```

## Berechtigungssystem

Das Plugin nutzt das bestehende Berechtigungssystem von Remote Tools:

1. **Authentifizierung**: Benutzer muss eingeloggt sein (`Site.AddLoggedInSite`)
2. **Device-Zugriff**: Über `ensureAccessAndExecute()` geprüft
3. **Berechtigungsprüfung**:
   - Admin-Benutzer haben vollen Zugriff
   - Normale Benutzer benötigen Zugriff auf das spezifische Device über `user_device_access` Tabelle

### Berechtigungen in der Datenbank

```sql
-- Benutzer benötigt Eintrag in user_device_access
INSERT INTO user_device_access (device_name, username)
VALUES ('PC-001', 'max.mustermann');
```

## Verwendungsbeispiel (JavaScript/Node.js)

```javascript
const SockOperation = require('./SockOperation');

// Benachrichtigung hinzufügen
async function addNotification(deviceId, level, message) {
    const op = new SockOperation.NotificationsSockOp(deviceId);
    op.AddNotification(level, message);
    op.SetCallback((data) => {
        console.log('Notification added:', data);
    });
    op.send();
}

// Benachrichtigungen abrufen
async function getNotifications(deviceId) {
    const op = new SockOperation.NotificationsSockOp(deviceId);
    op.GetNotifications();
    op.SetCallback((data) => {
        console.log('Notifications:', data.notifications);
    });
    op.send();
}

// Verwendung
addNotification('PC-001', 'info', 'System gestartet');
getNotifications('PC-001');
```

## Verwendungsbeispiel (HTTP/REST)

```bash
# Benachrichtigung hinzufügen
curl -X POST https://your-server/WebSocks/Notifications/Add \
  -H "Content-Type: application/json" \
  -d '{
    "id": "PC-001",
    "level": "warning",
    "message": "CPU-Auslastung hoch"
  }'

# Benachrichtigungen abrufen
curl -X GET "https://your-server/WebSocks/Notifications/Get?id=PC-001"

# Anzahl abrufen
curl -X GET "https://your-server/WebSocks/Notifications/Count?id=PC-001"

# Benachrichtigungen löschen
curl -X POST https://your-server/WebSocks/Notifications/Clear \
  -H "Content-Type: application/json" \
  -d '{"id": "PC-001"}'

# Nach Level filtern
curl -X GET "https://your-server/WebSocks/Notifications/GetByLevel?id=PC-001&level=error"
```

## Technische Details

### Speicherung

- **Server**: Keine persistente Speicherung (nur zur Weiterleitung an Client)
- **Client**: In-Memory-Speicherung in `NotificationStorage` Klasse
- **Kapazität**: Maximal 100 Benachrichtigungen (älteste werden automatisch entfernt)
- **Sortierung**: Neueste zuerst (FIFO)
- **Thread-Safety**: Ja, über `lock()` Mechanismus

### Notification Levels

- `info`: Informative Nachrichten
- `warning`: Warnungen
- `error`: Fehler

### Datenstruktur (C#)

```csharp
public class Notification
{
    public string Level { get; set; }
    public string Message { get; set; }
    public long Timestamp { get; set; }  // Unix timestamp in milliseconds
    public DateTime DateTime { get; }     // Konvertiert aus Timestamp
}
```

## Integration in bestehende Systeme

### 1. Automatische Benachrichtigungen

Sie können automatisch Benachrichtigungen hinzufügen, wenn bestimmte Ereignisse auftreten:

```javascript
// In WebSock.js - bei Audio-Operation
static async SetVolume (req, res) {
    const deviceId = req.body.id
    await WebSock.ensureAccessAndExecute(req, res, deviceId, async () => {
        var op = new SockOperation.AudioSettingSockOp(deviceId)
        op.SetVolume(req.body.data.value, req.body.data.type)
        op.SetCallback(WebSock.MoveDataToRes.bind(this, res))
        op.send()

        // Automatische Benachrichtigung
        var notifOp = new SockOperation.NotificationsSockOp(deviceId)
        notifOp.AddNotification('info', `Volume changed to ${req.body.data.value}%`)
        notifOp.send()
    })
}
```

### 2. Monitoring Dashboard

Erstellen Sie ein Dashboard, das regelmäßig Benachrichtigungen abruft:

```javascript
setInterval(async () => {
    const devices = await getConnectedDevices();
    for (const device of devices) {
        const notifications = await getNotifications(device.id);
        displayNotifications(device, notifications);
    }
}, 5000); // Alle 5 Sekunden
```

## Testing

### Test-Szenarien

1. **Hinzufügen**: 105 Benachrichtigungen hinzufügen → Nur 100 werden gespeichert
2. **Abrufen**: Alle Benachrichtigungen abrufen → Neueste zuerst
3. **Filtern**: Nach Level filtern → Nur matching Benachrichtigungen
4. **Löschen**: Alle löschen → Count = 0
5. **Berechtigungen**: Zugriff ohne Device-Berechtigung → HTTP 403

## Erweiterungsmöglichkeiten

- **Persistente Speicherung**: SQLite-Datenbank für dauerhafte Speicherung
- **Notification-Forwarding**: Automatisches Weiterleiten an externe Systeme (Email, Slack, etc.)
- **Prioritäten**: Zusätzliche Priority-Levels (critical, low, etc.)
- **Filterung**: Erweiterte Filter (Zeitraum, Suchtext, etc.)
- **Push-Benachrichtigungen**: Aktives Pushen neuer Notifications an verbundene Clients

## Support

Bei Fragen oder Problemen:
1. Überprüfen Sie die Server-Logs (CentralServer)
2. Überprüfen Sie die Client-Logs (WindowsRemoteTools Console-Output)
3. Testen Sie die API-Endpunkte direkt mit curl/Postman
4. Überprüfen Sie die Berechtigungen in der Datenbank
